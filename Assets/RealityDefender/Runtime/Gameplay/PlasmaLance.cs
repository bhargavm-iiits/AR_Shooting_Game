using System.Collections.Generic;
using UnityEngine;

namespace RealityDefender
{
    /// <summary>
    /// "Plasma lance" presentation for one physics projectile:
    ///  - electric discharge arcs crackle off the railgun muzzle and two charge rings blow outward;
    ///  - a braided double helix of plasma spins around a white-hot core, pinched at both ends;
    ///  - ion rings are shed every half metre along the flight path and expand like a sabot shockwave;
    ///  - the visual starts at the gun muzzle and converges onto the true camera-axis flight line within ~0.5 m,
    ///    so the shot reads as leaving the gun while the physics stay exactly on the camera axis.
    /// Geometry is computed analytically from the flight line each frame, so the helix stays smooth at any frame rate.
    /// The lance never affects physics; it follows the projectile and fades out after the projectile retires.
    /// </summary>
    public sealed class PlasmaLance : MonoBehaviour
    {
        const int StrandPoints = 56;
        const float VisibleLength = 0.95f;
        const float ConvergeDistance = 0.55f;
        const float TwistPerMeter = 2f * Mathf.PI / 0.32f;
        const float HelixRadius = 0.026f;
        const float RingSpacing = 0.5f;
        const float RingSeconds = 0.34f;
        const float FadeSeconds = 0.24f;
        const int RingPoints = 20;

        struct Ring
        {
            public LineRenderer line;
            public Vector3 center;
            public float born;
            public Color color;
        }

        Projectile shot;
        Vector3 launch, dir, muzzleOffset, u, w;
        float head, deadAt = -1f, nextRing;
        Color colorA, colorB;
        LineRenderer strandA, strandB, core, flare;
        readonly Vector3[] buffer = new Vector3[StrandPoints];
        readonly Vector3[] ringBuffer = new Vector3[RingPoints];
        readonly List<Ring> rings = new List<Ring>(12);
        Transform arcRoot;
        LineRenderer[] arcs;
        float arcUntil;
        readonly Vector3[] arcBuffer = new Vector3[8];

        public static PlasmaLance Spawn(Projectile shot, Vector3 launch, Vector3 direction, Vector3 muzzle, Transform muzzleParent, Color a, Color b)
        {
            if (!FxMaterials.Glow || direction.sqrMagnitude < 1e-6f) return null;
            var lance = new GameObject("Plasma lance").AddComponent<PlasmaLance>();
            lance.Init(shot, launch, direction, muzzle, muzzleParent, a, b);
            return lance;
        }

        void Init(Projectile projectile, Vector3 origin, Vector3 direction, Vector3 muzzle, Transform muzzleParent, Color a, Color b)
        {
            shot = projectile;
            launch = origin;
            dir = direction.normalized;
            muzzleOffset = muzzle - origin;
            u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            w = Vector3.Cross(dir, u);
            colorA = a;
            colorB = b;
            float now = Time.time;

            strandA = FxMaterials.Line("Helix strand A", transform);
            strandA.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.55f, 0.006f), new Keyframe(1f, 0.011f));
            strandA.colorGradient = FxMaterials.Fade(b, Color.Lerp(a, Color.white, 0.35f));
            strandB = FxMaterials.Line("Helix strand B", transform);
            strandB.widthCurve = strandA.widthCurve;
            strandB.colorGradient = FxMaterials.Fade(a, Color.Lerp(b, Color.white, 0.35f));
            core = FxMaterials.Line("White-hot core", transform);
            core.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.017f));
            core.colorGradient = FxMaterials.Fade(a, Color.white, 0f, 1f);
            flare = FxMaterials.Line("Head flare", transform);
            flare.colorGradient = FxMaterials.Fade(Color.white, Color.Lerp(a, Color.white, 0.6f), 0.9f, 0.9f);

            // Charge rings blown off the muzzle, then one ion ring every RingSpacing along the path.
            AddRing(0f, now, Color.Lerp(a, Color.white, 0.4f));
            AddRing(0.03f, now + 0.045f, b);
            nextRing = RingSpacing * 0.7f;

            // Electric discharge arcs ride with the gun (local space) so recoil carries them.
            if (muzzleParent)
            {
                arcRoot = new GameObject("Rail discharge").transform;
                arcRoot.SetParent(muzzleParent, true);
                arcRoot.SetPositionAndRotation(muzzle, Quaternion.LookRotation(dir));
                arcs = new LineRenderer[3];
                for (int i = 0; i < arcs.Length; i++)
                {
                    arcs[i] = FxMaterials.Line("Arc " + (i + 1), arcRoot, false, false);
                    arcs[i].widthMultiplier = 0.0045f;
                    arcs[i].colorGradient = FxMaterials.Fade(Color.white, i == 1 ? b : a, 1f, 0.2f);
                }
                arcUntil = now + 0.12f;
            }
        }

        Vector3 Point(float s)
        {
            float k = Mathf.Clamp01(s / ConvergeDistance);
            float converge = 1f - k * k * (3f - 2f * k);
            return launch + dir * s + muzzleOffset * converge;
        }

        float Spread(float s)
        {
            float k = Mathf.Clamp01(s / ConvergeDistance);
            return 1f + 0.8f * (1f - k * k * (3f - 2f * k));
        }

        void AddRing(float distance, float born, Color color)
        {
            var line = FxMaterials.Line("Ion ring", transform, true);
            line.positionCount = 0;
            rings.Add(new Ring { line = line, center = Point(distance), born = born, color = color });
        }

        void LateUpdate()
        {
            float now = Time.time;
            bool alive = shot && !shot.IsSpent;
            if (alive) head = Mathf.Max(head, Vector3.Dot(shot.transform.position - launch, dir));
            else if (deadAt < 0f) deadAt = now;

            float fade = deadAt < 0f ? 1f : 1f - (now - deadAt) / FadeSeconds;
            if (fade <= 0f && rings.Count == 0 && arcs == null) { Destroy(gameObject); return; }
            fade = Mathf.Clamp01(fade);

            UpdateLance(now, fade);
            while (alive && head >= nextRing) { AddRing(nextRing, now, rings.Count % 2 == 0 ? colorA : colorB); nextRing += RingSpacing; }
            UpdateRings(now);
            UpdateArcs(now);
        }

        void UpdateLance(float now, float fade)
        {
            // The lance collapses toward its head while it fades, instead of blinking out.
            float length = Mathf.Min(VisibleLength * fade, head);
            if (fade <= 0f || length < 0.002f)
            {
                strandA.positionCount = strandB.positionCount = core.positionCount = flare.positionCount = 0;
                return;
            }
            float tail = head - length;
            float spin = now * 24f;
            for (int strand = 0; strand < 2; strand++)
            {
                float phase = strand * Mathf.PI;
                for (int i = 0; i < StrandPoints; i++)
                {
                    float x = i / (StrandPoints - 1f);
                    float s = tail + length * x;
                    // Pinched at both ends. Clamp: float sin(PI) is slightly negative, and Pow of a negative base is NaN.
                    float envelope = i == 0 || i == StrandPoints - 1 ? 0f : Mathf.Max(0f, Mathf.Sin(Mathf.PI * x));
                    float radius = HelixRadius * Mathf.Pow(envelope, 0.6f) * Spread(s);
                    float theta = s * TwistPerMeter + spin + phase;
                    buffer[i] = Point(s) + (u * Mathf.Cos(theta) + w * Mathf.Sin(theta)) * radius;
                }
                var line = strand == 0 ? strandA : strandB;
                line.positionCount = StrandPoints;
                line.SetPositions(buffer);
                line.widthMultiplier = fade;
            }
            core.positionCount = 2;
            core.SetPosition(0, Point(head - Mathf.Min(0.42f, length)));
            core.SetPosition(1, Point(head));
            core.widthMultiplier = fade;
            Vector3 tip = Point(head);
            flare.positionCount = 2;
            flare.SetPosition(0, tip - dir * 0.05f);
            flare.SetPosition(1, tip + dir * 0.012f);
            flare.widthMultiplier = 0.06f * fade * (0.85f + 0.15f * Mathf.Sin(now * 60f));
        }

        void UpdateRings(float now)
        {
            for (int r = rings.Count - 1; r >= 0; r--)
            {
                Ring ring = rings[r];
                float age = (now - ring.born) / RingSeconds;
                if (!ring.line) { rings.RemoveAt(r); continue; }
                if (age < 0f) { ring.line.positionCount = 0; continue; }
                if (age >= 1f) { Destroy(ring.line.gameObject); rings.RemoveAt(r); continue; }
                float ease = 1f - Mathf.Pow(1f - age, 3f);
                float radius = Mathf.Lerp(0.008f, 0.075f, ease);
                for (int i = 0; i < RingPoints; i++)
                {
                    float a = i * Mathf.PI * 2f / RingPoints;
                    ringBuffer[i] = ring.center + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * radius;
                }
                ring.line.positionCount = RingPoints;
                ring.line.SetPositions(ringBuffer);
                ring.line.widthMultiplier = 0.006f * (1f - age) + 0.001f;
                Color c = ring.color;
                c.a = Mathf.Pow(1f - age, 1.5f);
                ring.line.startColor = ring.line.endColor = c;
            }
        }

        void UpdateArcs(float now)
        {
            if (arcs == null) return;
            if (!arcRoot || now >= arcUntil)
            {
                if (arcRoot) Destroy(arcRoot.gameObject);
                arcRoot = null;
                arcs = null;
                return;
            }
            float life = Mathf.Clamp01((arcUntil - now) / 0.12f);
            for (int i = 0; i < arcs.Length; i++)
            {
                float length = Random.Range(0.08f, 0.2f);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 bend = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.01f, 0.03f);
                for (int p = 0; p < arcBuffer.Length; p++)
                {
                    float x = p / (arcBuffer.Length - 1f);
                    float bow = Mathf.Sin(x * Mathf.PI);
                    Vector2 jitter = p == 0 ? Vector2.zero : Random.insideUnitCircle * 0.011f;
                    arcBuffer[p] = new Vector3(bend.x * bow + jitter.x, bend.y * bow + jitter.y, x * length);
                }
                arcs[i].positionCount = arcBuffer.Length;
                arcs[i].SetPositions(arcBuffer);
                arcs[i].widthMultiplier = 0.0045f * life * (Random.value < 0.25f ? 0.3f : 1f);
            }
        }

        void OnDestroy()
        {
            if (arcRoot) Destroy(arcRoot.gameObject);
        }
    }
}
