using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealityDefender
{
    /// <summary>
    /// One authority for a three-level round in both supported play modes.
    ///  Level 1 "Still Waters": 2 static bubbles.
    ///  Level 2 "Orbit Storm":  3 bubbles orbiting the base on a tilting carousel.
    ///  Level 3 "Chaos Drift":  4 bubbles in free flight that always stay inside the visible screen.
    /// Each cleared level plays world-space fireworks and raises <see cref="LevelCompleted"/> for the HUD's aura
    /// celebration; the next level spawns once the celebration has played and tracking is healthy.
    /// </summary>
    public sealed class WaveController : MonoBehaviour
    {
        public EnergyTarget targetPrefab;
        public Projectile projectilePrefab;
        public GameObject railgunPrefab;
        public ParticleSystem impactPrefab;
        [Tooltip("Additive glow material (RealityDefender/Energy Particle) for the layered orb blast.")]
        public Material blastGlowMaterial;
        [Tooltip("Same shader with Ring = 1, for the shockwave rings.")]
        public Material blastRingMaterial;
        [Range(1f, 30f)] public float projectileSpeed = 11f;
        [Range(.1f, 1f)] public float fireCooldown = .22f;
        [Tooltip("Seconds the level-complete celebration plays before the next level spawns.")]
        [Range(1f, 8f)] public float celebrationSeconds = 3.6f;

        public int Score { get; private set; }
        public int Remaining => living.Count;
        public bool IsActive { get; private set; }
        /// <summary>True once the final level has been cleared.</summary>
        public bool IsComplete { get; private set; }
        public bool IsPractice { get; private set; }
        public bool IsTracking { get; private set; }
        public int RoundId { get; private set; }
        /// <summary>1-based current level, or 0 when no round is running.</summary>
        public int Level { get; private set; }
        /// <summary>True between clearing a level and spawning the next one (and after the final level).</summary>
        public bool IsCelebrating { get; private set; }
        public LevelInfo CurrentLevel => Levels.Get(Mathf.Max(1, Level));
        public int LevelTargetCount => Level > 0 ? CurrentLevel.targets : 0;
        public Camera ActiveCamera { get; private set; }
        public bool CanFire => IsActive && !IsComplete && !IsCelebrating && IsTracking && ActiveCamera &&
            ActiveCamera.isActiveAndEnabled && Time.time >= nextFire && shots.Count < 12;
        public int LiveProjectileCount => shots.Count;
        public IReadOnlyCollection<EnergyTarget> LivingTargets => living;
        /// <summary>World size of the arena relative to its authored 0.65 m footprint (read live: pinch-resize changes it).</summary>
        public float ArenaScale => scaleSource ? Mathf.Max(0.1f, scaleSource.lossyScale.x) : 1f;
        /// <summary>The placed base (null when no round is running).</summary>
        public Transform ArenaRoot => scaleSource;

        public event Action StateChanged;
        public event Action<Vector3> HitAccepted;
        public event Action ShotFired;
        public event Action<LevelInfo> LevelStarted;
        public event Action<LevelResult> LevelCompleted;
        public event Action RoundReset;

        readonly HashSet<EnergyTarget> living = new HashSet<EnergyTarget>();
        readonly HashSet<Projectile> shots = new HashSet<Projectile>();
        readonly ParticleSystem[] impacts = new ParticleSystem[3];
        readonly EnergyBlast[] blasts = new EnergyBlast[10];
        float nextFire;
        Transform scaleSource;
        Transform levelRoot;
        Transform carousel;
        float carouselAngle;
        int pendingLevel;
        float advanceAt;
        int levelShots, levelHits;
        float levelStartedAt;
        float hitStopUntil;
        RailgunPresentation railgun;
        AudioSource audioSource, variedSource;
        AudioClip fireClip, hitClip, clearClip, finalClip;

        struct Firework { public float at; public Vector3 local; public float scale; }
        readonly List<Firework> fireworks = new List<Firework>(12);

        public void BeginWave(GameBase placedBase, Camera camera, bool isPractice = false)
        {
            ResetWave();
            if (!placedBase || !placedBase.HasValidSlots || !camera || !targetPrefab || !projectilePrefab)
            {
                Debug.LogError("Reality Defender: wave requires one base with three unique slots, camera and projectile/target prefabs.");
                return;
            }
            ActiveCamera = camera;
            scaleSource = placedBase.transform;
            IsPractice = isPractice;
            IsTracking = true;
            IsActive = true;
            if (railgunPrefab)
            {
                var visual = Instantiate(railgunPrefab, camera.transform, false);
                railgun = visual.GetComponent<RailgunPresentation>();
                if (!railgun) railgun = visual.AddComponent<RailgunPresentation>();
                railgun.Bind(camera);
            }
            EnsureFeedback();
            StartLevel(1);
        }

        void StartLevel(int number)
        {
            if (!scaleSource) return;
            Level = Mathf.Clamp(number, 1, Levels.Count);
            IsCelebrating = false;
            pendingLevel = 0;
            levelShots = levelHits = 0;
            levelStartedAt = Time.time;
            if (levelRoot) Destroy(levelRoot.gameObject);
            levelRoot = new GameObject("Level " + Level).transform;
            levelRoot.SetParent(scaleSource, false);
            carousel = null;
            LevelInfo info = CurrentLevel;
            Transform parent = levelRoot;
            if (info.motion == TargetMotion.Orbit)
            {
                carousel = new GameObject("Orbit carousel").transform;
                carousel.SetParent(levelRoot, false);
                carousel.localPosition = new Vector3(0f, 0.34f, 0f);
                carouselAngle = 0f;
                parent = carousel;
            }
            for (int i = 0; i < info.targets; i++)
            {
                var target = Instantiate(targetPrefab, parent);
                target.transform.localPosition = SpawnPoint(info, i);
                target.transform.localRotation = Quaternion.identity;
                target.Initialize(this, RoundId, info.motion, info.death, info.primary);
                living.Add(target);
            }
            if (railgun) railgun.SetVisible(IsTracking);
            Play(GameAssets.Clip(Level == 1 ? "sfx_place" : "sfx_level_intro"), Level == 1 ? .6f : .45f);
            LevelStarted?.Invoke(info);
            StateChanged?.Invoke();
        }

        /// <summary>Base-local seat for target <paramref name="index"/> (carousel-local for orbit levels).</summary>
        static Vector3 SpawnPoint(LevelInfo info, int index)
        {
            switch (info.motion)
            {
                case TargetMotion.Static:
                    return index == 0 ? new Vector3(-0.21f, 0.30f, 0.02f) : new Vector3(0.21f, 0.36f, -0.02f);
                case TargetMotion.Orbit:
                    float angle = index * Mathf.PI * 2f / info.targets;
                    return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.25f;
                default:
                    Vector3[] starts =
                    {
                        new Vector3(-0.24f, 0.30f, 0f), new Vector3(0.24f, 0.30f, 0f),
                        new Vector3(-0.09f, 0.46f, 0.05f), new Vector3(0.09f, 0.46f, -0.05f)
                    };
                    return starts[index % starts.Length];
            }
        }

        public bool Fire()
        {
            if (!CanFire || !projectilePrefab) return false;
            nextFire = Time.time + fireCooldown;
            // Read the live camera pose once. Neither the presentation muzzle nor its recoil modifies this pose.
            Vector3 position = ActiveCamera.transform.position;
            Vector3 forward = ActiveCamera.transform.forward;
            var shot = Instantiate(projectilePrefab, position, Quaternion.LookRotation(forward));
            shots.Add(shot);
            shot.Launch(this, RoundId, position, forward, projectileSpeed);
            levelShots++;
            Vector3 muzzle = railgun ? railgun.MuzzleWorld
                : position + ActiveCamera.transform.up * -0.08f + forward * 0.25f;
            LevelInfo info = CurrentLevel;
            if (PlasmaLance.Spawn(shot, position, forward, muzzle, railgun ? railgun.transform : ActiveCamera.transform,
                    Color.Lerp(info.primary, Color.white, 0.15f), info.secondary))
            {
                // The lance replaces the stock tracer; the physics body and collider are untouched.
                foreach (var visual in shot.GetComponentsInChildren<Renderer>()) visual.enabled = false;
            }
            if (railgun) railgun.Recoil();
            // Recorded rail shot (alternating takes, slight pitch drift) over the synthesised plasma discharge.
            PlayVaried(GameAssets.Clip(UnityEngine.Random.value < 0.5f ? "sfx_shot_a" : "sfx_shot_b"), .5f, 0.94f, 1.06f);
            Play(fireClip, .2f);
            ShotFired?.Invoke();
            return true;
        }

        internal bool TryAcceptHit(Projectile projectile, EnergyTarget target, Vector3 point)
        {
            if (!IsActive || !IsTracking || IsComplete || IsCelebrating || !projectile || !target ||
                projectile.RoundId != RoundId || target.RoundId != RoundId || target.Owner != this ||
                target.IsConsumed || !shots.Contains(projectile) || !projectile.Body ||
                (projectile.Body.position - projectile.LaunchPosition).sqrMagnitude > Projectile.MaximumRange * Projectile.MaximumRange ||
                !living.Remove(target)) return false;
            Vector3 direction = projectile.Body.linearVelocity.sqrMagnitude > 1e-6f ? projectile.Body.linearVelocity : projectile.transform.forward;
            target.Consume(true, point, direction);
            levelHits++;
            Score += 10;
            // Layered impact: wet bubble pop, crunchy energy burst, sub-bass thump, synthesised body.
            Play(GameAssets.Clip("sfx_bubble_pop"), .45f);
            Play(GameAssets.Clip("sfx_burst"), .55f);
            Play(GameAssets.Clip("sfx_sub_thump"), .6f);
            Play(hitClip, .3f);
            HitAccepted?.Invoke(point);
            if (living.Count == 0) CompleteLevel();
            StateChanged?.Invoke();
            return true;
        }

        void CompleteLevel()
        {
            RetireShots();
            IsCelebrating = true;
            bool final = Level >= Levels.Count;
            var result = new LevelResult(CurrentLevel, final, Score, levelShots, levelHits, Time.time - levelStartedAt);
            if (final)
            {
                IsComplete = true;
                IsActive = false;
                if (railgun) railgun.SetVisible(false);
                Play(GameAssets.Clip("sfx_burst_final"), .6f);
                Play(GameAssets.Clip("sfx_level_swell"), .55f);
                Play(finalClip, .55f);
            }
            else
            {
                pendingLevel = Level + 1;
                advanceAt = Time.unscaledTime + celebrationSeconds;
                Play(GameAssets.Clip("sfx_level_swell"), .5f);
                Play(clearClip, .5f);
            }
            QueueFireworks(final);
            LevelCompleted?.Invoke(result);
        }

        void QueueFireworks(bool final)
        {
            int count = final ? 9 : 5;
            float now = Time.unscaledTime;
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = UnityEngine.Random.Range(0.05f, 0.32f);
                fireworks.Add(new Firework
                {
                    at = now + 0.15f + i * (final ? 0.2f : 0.24f) + UnityEngine.Random.Range(0f, 0.08f),
                    local = new Vector3(Mathf.Cos(angle) * radius, UnityEngine.Random.Range(0.45f, 0.85f), Mathf.Sin(angle) * radius),
                    scale = UnityEngine.Random.Range(1.3f, final ? 2.6f : 2f)
                });
            }
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (hitStopUntil > 0f && now >= hitStopUntil) EndHitStop();
            for (int i = fireworks.Count - 1; i >= 0; i--)
            {
                if (now < fireworks[i].at) continue;
                if (scaleSource && IsTracking) SpawnBlast(scaleSource.TransformPoint(fireworks[i].local), fireworks[i].scale);
                fireworks.RemoveAt(i);
            }
            if (carousel && IsTracking)
            {
                // A tilting, precessing carousel: the orbit plane wobbles like a spinning gyroscope.
                float t = Time.time;
                carouselAngle += Time.deltaTime * 52f;
                carousel.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 14f, carouselAngle, Mathf.Cos(t * 0.55f) * 9f);
            }
            if (pendingLevel > 0 && IsActive && IsTracking && now >= advanceAt) StartLevel(pendingLevel);
        }

        /// <summary>A random point for a wandering bubble: on screen, at about the arena's depth, above the floor.</summary>
        internal Vector3 PickWanderPoint(Camera cam)
        {
            float s = ArenaScale;
            Vector3 center = scaleSource ? scaleSource.TransformPoint(new Vector3(0f, 0.34f, 0f)) : cam.transform.position + cam.transform.forward * 1.5f;
            float depth = Mathf.Clamp(Vector3.Dot(center - cam.transform.position, cam.transform.forward), 0.8f, 3.4f);
            var viewport = new Vector3(UnityEngine.Random.Range(0.2f, 0.8f), UnityEngine.Random.Range(0.38f, 0.7f),
                depth * UnityEngine.Random.Range(0.8f, 1.15f));
            Vector3 point = cam.ViewportToWorldPoint(viewport);
            if (scaleSource) point.y = Mathf.Max(point.y, scaleSource.position.y + EnergyTarget.HitRadius * s * 1.6f);
            return ClampToScreen(cam, point);
        }

        /// <summary>
        /// Clamps a world point into the playable screen area (clear of the HUD panels) and into a reachable depth band
        /// well inside the projectile's five metre range.
        /// </summary>
        internal Vector3 ClampToScreen(Camera cam, Vector3 point)
        {
            // The whole visible bubble (film shell, plus room for its travel stretch) must fit, not just its centre.
            float radius = 0.084f * 1.2f * ArenaScale;
            bool portrait = cam.aspect < 1f;
            float marginX = portrait ? 0.16f : 0.12f;
            float bottom = portrait ? 0.3f : 0.32f;
            float top = portrait ? 0.78f : 0.74f;
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // Depth at which the bubble still fits between the margins with room to move.
            float fitDepth = radius / (tanHalf * Mathf.Min(top - bottom, (1f - 2f * marginX) * cam.aspect) * 0.45f);
            // Range wins over framing: a huge (pinch-enlarged) arena must still be shootable.
            float near = Mathf.Min(Mathf.Max(cam.nearClipPlane + 0.35f, 0.6f, fitDepth), MaxWanderDistance - radius);

            Vector3 v = cam.WorldToViewportPoint(point);
            v.z = Mathf.Clamp(v.z, near, MaxWanderDistance);
            // Viewport extent of the bubble's radius at this depth.
            float halfHeight = v.z * tanHalf;
            float ry = radius / (2f * halfHeight);
            float rx = radius / (2f * halfHeight * cam.aspect);
            v.x = Mathf.Clamp(v.x, Mathf.Min(0.5f, marginX + rx), Mathf.Max(0.5f, 1f - marginX - rx));
            v.y = Mathf.Clamp(v.y, Mathf.Min(0.5f, bottom + ry), Mathf.Max(0.5f, top - ry));
            Vector3 world = cam.ViewportToWorldPoint(v);

            // Hits are accepted by straight-line distance, and a screen corner is farther than its depth: pull the
            // bubble in along its own view ray (which keeps it at the same screen position) to stay in range.
            Vector3 eye = cam.transform.position;
            Vector3 offset = world - eye;
            float reach = MaxWanderDistance - radius;
            if (offset.magnitude > reach) world = eye + offset.normalized * reach;
            return world;
        }

        /// <summary>Farthest a wandering bubble may be from the camera: comfortably inside the projectile range.</summary>
        const float MaxWanderDistance = Projectile.MaximumRange - 1f;

        /// <summary>Plays a pooled layered energy blast; <paramref name="size"/> is relative to one bubble.</summary>
        internal void SpawnBlast(Vector3 point, float size)
        {
            float scale = ArenaScale * Mathf.Max(0.05f, size);
            foreach (var blast in blasts)
                if (blast && !blast.IsPlaying) { blast.Play(point, scale); return; }
            if (blasts[0]) { blasts[0].Play(point, scale); return; }
            foreach (var effect in impacts)
                if (effect && !effect.gameObject.activeSelf)
                {
                    effect.transform.position = point;
                    effect.transform.localScale = Vector3.one * scale;
                    effect.gameObject.SetActive(true);
                    effect.Play(true);
                    return;
                }
        }

        /// <summary>Brief slow motion for impact weight; always restored, even on pause or reset.</summary>
        internal void HitStop(float seconds, float timeScale)
        {
            // Only the Update loop restores time; never take global time control while it cannot run.
            if (!isActiveAndEnabled) return;
            if (hitStopUntil <= 0f) Time.timeScale = Mathf.Clamp(timeScale, 0.05f, 1f);
            hitStopUntil = Time.unscaledTime + seconds;
        }

        void EndHitStop()
        {
            hitStopUntil = 0f;
            Time.timeScale = 1f;
        }

        public void SetTracking(bool tracking)
        {
            if (IsTracking == tracking) return;
            IsTracking = tracking;
            if (!tracking) { RetireShots(); EndHitStop(); }
            if (railgun) railgun.SetVisible(tracking && IsActive);
            StateChanged?.Invoke();
        }

        public void ResetWave()
        {
            RoundId++;
            IsActive = false;
            IsComplete = false;
            IsTracking = false;
            IsCelebrating = false;
            Level = 0;
            pendingLevel = 0;
            fireworks.Clear();
            EndHitStop();
            RetireShots();
            foreach (var target in living) if (target) target.Consume();
            living.Clear();
            if (levelRoot) Destroy(levelRoot.gameObject);
            levelRoot = null;
            carousel = null;
            for (int i = 0; i < impacts.Length; i++)
                if (impacts[i])
                {
                    impacts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    impacts[i].gameObject.SetActive(false);
                }
            foreach (var blast in blasts) if (blast) blast.Stop();
            scaleSource = null;
            if (railgun) Destroy(railgun.gameObject);
            railgun = null;
            ActiveCamera = null;
            IsPractice = false;
            Score = 0;
            nextFire = 0f;
            RoundReset?.Invoke();
            StateChanged?.Invoke();
        }

        internal void ForgetProjectile(Projectile projectile) => shots.Remove(projectile);

        void RetireShots()
        {
            // Retire removes from the set; copy before visiting it.
            if (shots.Count == 0) return;
            var copy = new Projectile[shots.Count];
            shots.CopyTo(copy);
            shots.Clear();
            foreach (var shot in copy) if (shot) shot.Retire();
        }

        void EnsureFeedback()
        {
            if (blastGlowMaterial && blastRingMaterial)
                for (int i = 0; i < blasts.Length; i++)
                    if (!blasts[i]) { blasts[i] = EnergyBlast.Create(blastGlowMaterial, blastRingMaterial); blasts[i].name = "Energy blast " + (i + 1); }
            if (impactPrefab)
                for (int i = 0; i < impacts.Length; i++)
                    if (!impacts[i])
                    {
                        impacts[i] = Instantiate(impactPrefab);
                        impacts[i].name = "Impact pool " + (i + 1);
                        impacts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = impacts[i].main;
                        main.playOnAwake = false;
                        main.stopAction = ParticleSystemStopAction.Disable;
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        impacts[i].gameObject.SetActive(false);
                    }
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0;
                fireClip = MakeRailShot("Plasma lance", .38f);
                hitClip = MakeBlast("Energy blast", .7f);
                clearClip = MakeFanfare("Level cleared", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, .09f, .9f);
                finalClip = MakeFanfare("All levels cleared", new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, .1f, 1.6f);
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (audioSource && clip) audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>One-shot with a random pitch, on its own source so other sounds keep their pitch.</summary>
        internal void PlayVaried(AudioClip clip, float volume, float minPitch, float maxPitch)
        {
            if (!clip) return;
            if (!variedSource)
            {
                if (!audioSource) return;
                variedSource = gameObject.AddComponent<AudioSource>();
                variedSource.playOnAwake = false;
                variedSource.spatialBlend = 0f;
            }
            variedSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            variedSource.PlayOneShot(clip, volume);
        }

        /// <summary>Synthesised explosion: sharp crack, filtered noise body and a falling sub-bass thump.</summary>
        static AudioClip MakeBlast(string name, float length)
        {
            const int rate = 22050;
            int count = Mathf.CeilToInt(length * rate);
            var data = new float[count];
            var random = new System.Random(4711);
            float low = 0f, phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (noise - low) * Mathf.Lerp(0.55f, 0.04f, Mathf.Clamp01(t / 0.35f)); // brighter at first, darker tail
                float crack = noise * Mathf.Exp(-t * 60f);
                float body = low * Mathf.Exp(-t * 6f) * 1.6f;
                phase += Mathf.Lerp(95f, 38f, Mathf.Clamp01(t / 0.4f)) * 2f * Mathf.PI / rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 7f);
                float attack = Mathf.Min(1f, t * 900f);
                data[i] = Mathf.Clamp((crack * 0.5f + body * 0.55f + thump * 0.75f) * attack * 0.6f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Synthesised plasma-lance shot: a rising capacitor whine, then a falling rail discharge with an electric crackle,
        /// a phaser shimmer and a low thump.
        /// </summary>
        static AudioClip MakeRailShot(string name, float length)
        {
            const int rate = 22050;
            int count = Mathf.CeilToInt(length * rate);
            var data = new float[count];
            var random = new System.Random(1337);
            float whine = 0f, zap = 0f, sub = 0f, shimmer = 0f, crackleHold = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                // 0-35 ms: capacitor whine sweeping up.
                float charge = t < 0.035f ? 1f : Mathf.Exp(-(t - 0.035f) * 90f);
                whine += Mathf.Lerp(900f, 3200f, Mathf.Clamp01(t / 0.035f)) * 2f * Mathf.PI / rate;
                // Discharge: fast downward sweep.
                float d = Mathf.Max(0f, t - 0.03f);
                bool fired = t >= 0.03f;
                zap += Mathf.Lerp(2400f, 140f, Mathf.Clamp01(d / 0.22f)) * 2f * Mathf.PI / rate;
                shimmer += (1800f + Mathf.Sin(t * 90f) * 600f) * 2f * Mathf.PI / rate;
                sub += Mathf.Lerp(120f, 45f, Mathf.Clamp01(d / 0.2f)) * 2f * Mathf.PI / rate;
                float discharge = fired ? Mathf.Exp(-d * 11f) : 0f;
                // Sample-and-hold noise gives an electric crackle rather than a hiss.
                if (random.NextDouble() < 0.08) crackleHold = (float)(random.NextDouble() * 2.0 - 1.0);
                float crackle = fired ? crackleHold * Mathf.Exp(-d * 25f) : 0f;
                float thump = fired ? Mathf.Sin(sub) * Mathf.Exp(-d * 14f) : 0f;
                float sample = Mathf.Sin(whine) * charge * 0.25f
                             + (Mathf.Sin(zap) + 0.35f * Mathf.Sin(zap * 2.01f)) * discharge * 0.5f
                             + Mathf.Sin(shimmer) * discharge * 0.12f
                             + crackle * 0.35f
                             + thump * 0.55f;
                float attack = Mathf.Min(1f, t * 600f);
                data[i] = Mathf.Clamp(sample * attack * 0.7f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Synthesised arpeggio fanfare with a sustained shimmering chord.</summary>
        static AudioClip MakeFanfare(string name, float[] notes, float step, float length)
        {
            const int rate = 22050;
            int count = Mathf.CeilToInt(length * rate);
            var data = new float[count];
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * step;
                float phase = 0f, phase2 = 0f;
                for (int i = Mathf.CeilToInt(start * rate); i < count; i++)
                {
                    float t = (float)i / rate - start;
                    phase += notes[n] * 2f * Mathf.PI / rate;
                    phase2 += notes[n] * 2.003f * 2f * Mathf.PI / rate;
                    float env = Mathf.Min(1f, t * 80f) * Mathf.Exp(-t * 2.4f);
                    float vibrato = 1f + 0.15f * Mathf.Sin(t * 30f);
                    data[i] += (Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase2) * 0.25f * vibrato + Mathf.Sin(phase * 3f) * 0.08f) * env * 0.22f;
                }
            }
            for (int i = 0; i < count; i++)
            {
                float fadeOut = Mathf.Clamp01((count - i) / (rate * 0.15f));
                data[i] = Mathf.Clamp(data[i] * fadeOut, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SetTracking(false);
        }

        // Update restores hit-stop; if this controller stops updating, never leave the whole game in slow motion.
        void OnDisable()
        {
            EndHitStop();
            RetireShots(); // no in-flight shot may land (and request hit-stop) while this controller is disabled
        }

        void OnDestroy()
        {
            ResetWave();
            foreach (var effect in impacts) if (effect) Destroy(effect.gameObject);
            foreach (var blast in blasts) if (blast) Destroy(blast.gameObject);
            if (fireClip) Destroy(fireClip);
            if (hitClip) Destroy(hitClip);
            if (clearClip) Destroy(clearClip);
            if (finalClip) Destroy(finalClip);
        }
    }
}
