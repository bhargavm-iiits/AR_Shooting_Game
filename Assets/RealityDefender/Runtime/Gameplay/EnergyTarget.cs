using UnityEngine;

namespace RealityDefender
{
    /// <summary>
    /// An energy bubble: the Shader Graph energy core inside an iridescent soap-film shell.
    ///
    /// Motion by level:
    ///  - Static: fixed in place; only the film surface swirls and breathes.
    ///  - Orbit: carried by the level's carousel; hovers and spins in its seat.
    ///  - Wander: free flight with inertia, turbulence and mutual avoidance, always kept inside the visible screen area.
    ///    It stretches along its travel direction and squashes across it like a real wobbling bubble.
    ///
    /// Death by level (the hit volume is removed on the hit frame, so scoring stays exact while the animation plays):
    ///  - Rupture: jelly shock along the shot axis, then the film tears open from the impact point as the core implodes.
    ///  - Singularity: crushed into a spinning accretion disc that collapses into a point and detonates.
    ///  - Fission: hit-stop, then it splits into a swarm of mini bubbles that pop one after another.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class EnergyTarget : MonoBehaviour
    {
        /// <summary>Local collider radius; matches the visible film shell at arena scale 1.</summary>
        public const float HitRadius = 0.08f;
        const float ShellRadius = 0.084f;

        public int RoundId { get; private set; }
        public WaveController Owner { get; private set; }
        public bool IsConsumed { get; private set; }
        public TargetMotion Motion { get; private set; } = TargetMotion.Orbit;
        public DeathStyle Death { get; private set; } = DeathStyle.Rupture;
        /// <summary>World-space velocity over the last frame (used for lead aim and deformation).</summary>
        public Vector3 Velocity { get; private set; }

        Vector3 restPosition;
        Quaternion restRotation;
        Vector3 restScale = Vector3.one;
        float phase;
        float spin;
        bool initialized;
        Transform restParent;
        BubbleShell shell;
        Color tint = Color.cyan;
        Vector3 lastPosition;

        // Wander state
        Vector3 wanderVelocity;
        Vector3 wanderGoal;
        float goalUntil;

        // Death state
        float dyingTime = -1f;
        Vector3 shotDirection = Vector3.forward;
        Quaternion deathRotation;
        float deathSpin;
        bool deathBurst;

        public void Initialize(WaveController owner, int roundId) =>
            Initialize(owner, roundId, TargetMotion.Orbit, DeathStyle.Rupture, new Color(0.3f, 0.9f, 1f));

        public void Initialize(WaveController owner, int roundId, TargetMotion motion, DeathStyle death, Color levelTint)
        {
            Owner = owner;
            RoundId = roundId;
            IsConsumed = false;
            Motion = motion;
            Death = death;
            tint = levelTint;
            var sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = false;
            sphere.enabled = true;
            sphere.radius = HitRadius;
            var targetLayer = LayerMask.NameToLayer("Target");
            if (targetLayer >= 0) gameObject.layer = targetLayer;
            if (motion != TargetMotion.Static)
            {
                // Moving colliders belong on a kinematic body rather than being teleported as static colliders.
                if (!TryGetComponent(out Rigidbody body)) body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
            restParent = transform.parent;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            phase = Random.Range(0f, 100f);
            spin = Random.Range(25f, 45f) * (Random.value < .5f ? -1f : 1f);
            if (!shell) shell = BubbleShell.Create(transform, ShellRadius, Color.Lerp(levelTint, Color.white, 0.35f), phase);
            CreateGroundShadow();
            lastPosition = transform.position;
            wanderGoal = transform.position;
            goalUntil = 0f;
            initialized = true;
        }

        void Update()
        {
            if (!initialized) return;
            float dt = Time.deltaTime;
            float t = Time.time + phase;
            if (dyingTime >= 0f)
            {
                UpdateDeath(dt);
                UpdateGroundShadow(Mathf.Clamp01(1f - dyingTime / 0.25f));
                return;
            }
            // Moved off its seat deliberately (test fixtures): leave it where it was put.
            if (transform.parent == restParent)
            {
                switch (Motion)
                {
                    case TargetMotion.Static:
                        transform.localPosition = restPosition;
                        transform.localRotation = restRotation * Quaternion.Euler(0f, t * spin * 0.3f, 0f);
                        transform.localScale = restScale * (1f + Mathf.Sin(t * 2.3f) * 0.012f);
                        break;
                    case TargetMotion.Orbit:
                        Vector3 hover = Vector3.up * (Mathf.Sin(t * 1.7f) * 0.014f + Mathf.Sin(t * 3.1f) * 0.004f);
                        transform.localPosition = restPosition + hover;
                        transform.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 6f, t * spin, Mathf.Cos(t * 1.3f) * 4f);
                        transform.localScale = restScale * (1f + Mathf.Sin(t * 4.2f) * 0.03f + Mathf.Sin(t * 23f) * 0.006f);
                        break;
                    case TargetMotion.Wander:
                        UpdateWander(dt, t);
                        break;
                }
            }
            if (dt > 0f) Velocity = (transform.position - lastPosition) / dt;
            lastPosition = transform.position;
            UpdateGroundShadow(1f);
            if (shell)
            {
                float scale = Owner ? Owner.ArenaScale : 1f;
                float speed = Velocity.magnitude / Mathf.Max(0.1f, scale);
                float wobble = 0.0025f + Mathf.Min(0.012f, speed * 0.012f);
                shell.Apply(wobble, 0f, Vector3.forward, 1f, Motion == TargetMotion.Wander ? 1.6f : 1f);
            }
        }

        void UpdateWander(float dt, float t)
        {
            Camera cam = Owner ? Owner.ActiveCamera : null;
            if (!cam || !cam.isActiveAndEnabled || dt <= 0f) return;
            float s = Owner.ArenaScale;
            Vector3 pos = transform.position;
            float now = Time.time;
            float arrive = 0.06f * s;
            if (now >= goalUntil || (wanderGoal - pos).sqrMagnitude < arrive * arrive)
            {
                wanderGoal = Owner.PickWanderPoint(cam);
                goalUntil = now + Random.Range(1.1f, 2.4f);
            }
            // Re-clamp the goal every frame: in AR the screen moves with the phone.
            Vector3 goal = Owner.ClampToScreen(cam, wanderGoal);
            float maxSpeed = 0.36f * s;
            Vector3 desired = Vector3.ClampMagnitude((goal - pos) * 2.4f, maxSpeed);
            float personal = 0.3f * s;
            foreach (var other in Owner.LivingTargets)
            {
                if (!other || other == this) continue;
                Vector3 away = pos - other.transform.position;
                float d = away.magnitude;
                if (d > 1e-4f && d < personal) desired += away / d * ((personal - d) * 5f);
            }
            desired += new Vector3(Mathf.PerlinNoise(t * 0.7f, 1.3f) - 0.5f,
                                   Mathf.PerlinNoise(2.1f, t * 0.6f) - 0.5f,
                                   Mathf.PerlinNoise(t * 0.5f, 4.7f) - 0.5f) * (0.35f * s);
            wanderVelocity = Vector3.MoveTowards(wanderVelocity, desired, 1.6f * s * dt);
            pos += wanderVelocity * dt;
            // Guarantee: the bubble is inside the visible screen area every frame, even right after a fast phone turn.
            // Strip the velocity component that pushed it out so it slides along the screen edge instead of sticking.
            Vector3 clamped = Owner.ClampToScreen(cam, pos);
            Vector3 correction = clamped - pos;
            if (correction.sqrMagnitude > 1e-8f)
            {
                Vector3 outward = -correction.normalized;
                float pushing = Vector3.Dot(wanderVelocity, outward);
                if (pushing > 0f) wanderVelocity -= outward * pushing;
                pos = clamped;
            }
            transform.position = pos;

            // Stretch along travel and squash across it, like a real bubble pushing through air.
            float speed = wanderVelocity.magnitude / Mathf.Max(0.1f, s);
            float stretch = 1f + Mathf.Min(0.2f, speed * 0.35f);
            if (wanderVelocity.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wanderVelocity), 1f - Mathf.Exp(-dt * 8f));
            float across = 1f / Mathf.Sqrt(stretch);
            float pulse = 1f + Mathf.Sin(t * 4.2f) * 0.02f;
            transform.localScale = Vector3.Scale(restScale, new Vector3(across, across, stretch)) * pulse;
        }

        internal void Consume(bool animate = false) => Consume(animate, transform.position, transform.forward);

        internal void Consume(bool animate, Vector3 point, Vector3 direction)
        {
            if (IsConsumed) return;
            IsConsumed = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (TryGetComponent(out Rigidbody body)) body.detectCollisions = false;
            if (animate && isActiveAndEnabled && Owner)
            {
                dyingTime = 0f;
                shotDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : transform.forward;
                BeginDeath(point);
                return;
            }
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            Destroy(gameObject);
        }

        void BeginDeath(Vector3 point)
        {
            float s = Owner.ArenaScale;
            switch (Death)
            {
                case DeathStyle.Rupture:
                    deathRotation = Quaternion.LookRotation(shotDirection);
                    Owner.SpawnBlast(point, 1f);
                    break;
                case DeathStyle.Singularity:
                    deathRotation = transform.rotation;
                    Owner.SpawnBlast(point, 0.45f);
                    break;
                case DeathStyle.Fission:
                    Owner.HitStop(0.075f, 0.12f);
                    Owner.SpawnBlast(point, 0.55f);
                    int count = 6;
                    float worldRadius = HitRadius * s;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 outward = (Random.onUnitSphere + shotDirection * 0.9f + Vector3.up * 0.25f).normalized;
                        Vector3 start = transform.position + outward * worldRadius * 0.5f;
                        Vector3 velocity = outward * Random.Range(0.9f, 1.7f) * s;
                        float popAt = 0.16f + i * 0.07f + Random.Range(0f, 0.05f);
                        Color fragmentTint = Color.Lerp(tint, Color.white, Random.Range(0.2f, 0.6f));
                        BubbleFragment.Spawn(Owner, RoundId, start, velocity, worldRadius * Random.Range(0.3f, 0.45f), popAt,
                            fragmentTint, transform.parent);
                    }
                    break;
            }
        }

        void UpdateDeath(float dt)
        {
            dyingTime += dt;
            switch (Death)
            {
                case DeathStyle.Rupture: UpdateRupture(); break;
                case DeathStyle.Singularity: UpdateSingularity(dt); break;
                default: UpdateFission(); break;
            }
        }

        void UpdateRupture()
        {
            const float duration = 0.46f;
            // Damped jelly oscillation along the shot axis.
            float jelly = Mathf.Sin(dyingTime * 52f) * Mathf.Exp(-dyingTime * 11f) * 0.28f;
            transform.rotation = deathRotation;
            float core = dyingTime < 0.12f ? 1f : Mathf.Lerp(1f, 0.02f, Smooth((dyingTime - 0.12f) / 0.3f));
            transform.localScale = Vector3.Scale(restScale, new Vector3(1f + jelly * 0.6f, 1f + jelly * 0.6f, 1f - jelly)) * core;
            float pop = Smooth((dyingTime - 0.09f) / 0.26f);
            if (shell)
            {
                // The film expands and tears while the core collapses inside it.
                shell.transform.localScale = Vector3.one * (ShellRadius * Mathf.Lerp(1f, 1.3f, pop) / Mathf.Max(core, 0.02f));
                shell.Apply(0.03f, pop, shell.transform.InverseTransformDirection(-shotDirection), 1f, 2.5f);
            }
            if (dyingTime >= duration) Destroy(gameObject);
        }

        void UpdateSingularity(float dt)
        {
            const float collapseAt = 0.44f, duration = 0.66f;
            float charge = Mathf.Clamp01(dyingTime / collapseAt);
            deathSpin += dt * Mathf.Lerp(200f, 2600f, charge);
            transform.rotation = Quaternion.AngleAxis(deathSpin, Vector3.up) * Quaternion.Euler(18f, 0f, 0f);
            float f = Smooth(charge);
            Vector3 disc = new Vector3(1f + 0.45f * f, 1f - 0.85f * f, 1f + 0.45f * f) * (1f - 0.15f * f);
            float collapse = 1f;
            if (dyingTime >= collapseAt)
            {
                if (!deathBurst)
                {
                    deathBurst = true;
                    Owner.SpawnBlast(transform.position, 1.5f);
                }
                collapse = 1f - Smooth((dyingTime - collapseAt) / (duration - collapseAt));
            }
            transform.localScale = Vector3.Scale(restScale, disc) * Mathf.Max(0.01f, collapse);
            if (shell)
            {
                shell.Tint = Color.Lerp(Color.Lerp(tint, Color.white, 0.35f), new Color(1f, 0.35f, 0.9f), f);
                shell.Apply(0.004f + 0.04f * f, 0f, Vector3.up, 1f, Mathf.Lerp(1f, 6f, f));
            }
            if (dyingTime >= duration) Destroy(gameObject);
        }

        void UpdateFission()
        {
            const float duration = 0.12f;
            float k = Mathf.Clamp01(dyingTime / duration);
            transform.localScale = restScale * Mathf.Lerp(1.15f, 0.01f, k);
            if (shell) shell.Apply(0.04f, k, shell.transform.InverseTransformDirection(-shotDirection), 1f, 3f);
            if (dyingTime >= duration) Destroy(gameObject);
        }

        // ---------- Soft ground shadow ----------
        const float DeckTop = 0.132f;       // base-local height of the arena deck surface
        const float DeckRadius = 0.31f;     // base-local radius of the deck
        static readonly int ShadowColorId = Shader.PropertyToID("_BaseColor");
        Transform groundShadow;
        MeshRenderer shadowRenderer;
        MaterialPropertyBlock shadowBlock;

        /// <summary>A soft contact shadow on the deck (or the floor) beneath the bubble, so it reads as physically present.</summary>
        void CreateGroundShadow()
        {
            if (groundShadow || !Owner || !Owner.ArenaRoot) return;
            Material material = FxMaterials.Shadow;
            if (!material) return;
            var go = new GameObject("Bubble ground shadow", typeof(MeshFilter), typeof(MeshRenderer));
            groundShadow = go.transform;
            groundShadow.SetParent(Owner.ArenaRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = FxMaterials.Quad;
            shadowRenderer = go.GetComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = material;
            shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadowBlock = new MaterialPropertyBlock();
        }

        void UpdateGroundShadow(float strength)
        {
            if (!groundShadow) return;
            Transform arena = groundShadow.parent;
            if (!arena) return;
            Vector3 local = arena.InverseTransformPoint(transform.position);
            bool overDeck = new Vector2(local.x, local.z).magnitude < DeckRadius;
            float surface = overDeck ? DeckTop : 0f;
            float height = Mathf.Max(0f, local.y - surface);
            // Higher bubbles cast larger, softer and fainter shadows.
            float size = HitRadius * (1.1f + height * 2.2f);
            float alpha = Mathf.Clamp(0.5f - height * 0.9f, 0.12f, 0.5f) * strength;
            groundShadow.localPosition = new Vector3(local.x, surface + 0.003f, local.z);
            groundShadow.localRotation = Quaternion.identity;
            groundShadow.localScale = new Vector3(size, 1f, size);
            shadowBlock.SetColor(ShadowColorId, new Color(0f, 0.01f, 0.02f, alpha));
            shadowRenderer.SetPropertyBlock(shadowBlock);
            shadowRenderer.enabled = alpha > 0.005f && shell != null;
        }

        void OnDestroy()
        {
            if (groundShadow) Destroy(groundShadow.gameObject);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
