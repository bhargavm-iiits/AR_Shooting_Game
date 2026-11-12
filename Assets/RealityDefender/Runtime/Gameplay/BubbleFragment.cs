using UnityEngine;

namespace RealityDefender
{
    /// <summary>
    /// A mini bubble thrown out by a Fission death. It flies with air drag and a little buoyancy, jiggles, and pops on
    /// its own timer with a small blast. Purely visual: it has no collider and never scores.
    /// </summary>
    public sealed class BubbleFragment : MonoBehaviour
    {
        WaveController owner;
        int roundId;
        BubbleShell shell;
        Vector3 velocity;
        float age, popAt, scale, popping = -1f;
        Vector3 impact;

        public static void Spawn(WaveController owner, int roundId, Vector3 position, Vector3 velocity, float worldRadius,
            float popAt, Color tint, Transform parent)
        {
            var go = new GameObject("Fission bubble");
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var fragment = go.AddComponent<BubbleFragment>();
            fragment.owner = owner;
            fragment.roundId = roundId;
            fragment.velocity = velocity;
            fragment.popAt = popAt;
            fragment.scale = worldRadius;
            fragment.impact = Random.onUnitSphere;
            // The shell's local radius is 1; size it in world space so a scaled parent does not distort it.
            fragment.shell = BubbleShell.Create(go.transform, 1f, tint, Random.Range(0f, 50f));
            go.transform.localScale = Vector3.one;
            fragment.ApplyWorldRadius(worldRadius * 0.2f);
            if (!fragment.shell) Destroy(go);
        }

        void ApplyWorldRadius(float radius)
        {
            Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            float p = Mathf.Max(1e-4f, Mathf.Abs(parentScale.x));
            transform.localScale = Vector3.one * (radius / p);
        }

        void Update()
        {
            if (!owner || owner.RoundId != roundId || !shell) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            age += dt;
            if (popping < 0f)
            {
                // Air drag plus gentle buoyancy, with a spiral flutter.
                velocity *= Mathf.Exp(-dt * 3.2f);
                velocity += Vector3.up * (scale * 0.9f * dt);
                transform.position += velocity * dt;
                float grow = Mathf.Min(1f, age / 0.08f);
                float jiggle = 1f + Mathf.Sin(age * 38f) * 0.08f * Mathf.Exp(-age * 4f);
                ApplyWorldRadius(scale * Mathf.Lerp(0.2f, 1f, grow) * jiggle);
                shell.Apply(0.02f * Mathf.Exp(-age * 2f) + 0.004f, 0f, impact, 1f, 2.2f);
                if (age >= popAt)
                {
                    popping = 0f;
                    // SpawnBlast applies the arena scale itself, so pass a size relative to one bubble.
                    float bubbleSize = scale / (EnergyTarget.HitRadius * owner.ArenaScale);
                    owner.SpawnBlast(transform.position, bubbleSize * 0.42f);
                    owner.PlayVaried(GameAssets.Clip("sfx_bubble_pop"), .22f, 1.25f, 1.7f);
                }
                return;
            }
            popping += dt;
            float k = Mathf.Clamp01(popping / 0.14f);
            ApplyWorldRadius(scale * (1f + k * 0.35f));
            shell.Apply(0.03f, k, impact, 1f - k * 0.4f, 3f);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
