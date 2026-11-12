using UnityEngine;
using UnityEngine.UI;

namespace RealityDefender
{
    /// <summary>
    /// Full-screen procedural "aura" drawn additively on the HUD canvas, rebuilt every frame from time alone:
    /// a white-hot flash, licking aura flames along all four screen edges, rotating god rays, a breathing central aura,
    /// expanding shock rings, rising embers and twinkling star sparkles. Intro mode draws a lighter light-band with
    /// horizontal streaks for level announcements. Texture-free; all softness comes from vertex-alpha gradients.
    /// </summary>
    public sealed class AuraBurstGraphic : Graphic
    {
        public enum Mode { Celebrate, Intro }

        public Mode mode;
        public float time;
        public float duration = 3f;
        public Color primary = Color.cyan;
        public Color secondary = Color.magenta;
        public bool final;
        public float seed;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 1f || r.height <= 1f) return;
            float master = Mathf.Clamp01(time / 0.08f) * Mathf.Clamp01((duration - time) / 0.5f);
            if (master <= 0f) return;
            if (mode == Mode.Intro) DrawIntro(vh, r, time, master);
            else DrawCelebration(vh, r, time, master);
        }

        void DrawCelebration(VertexHelper vh, Rect r, float t, float master)
        {
            Vector2 c = r.center;
            float minDim = Mathf.Min(r.width, r.height), maxDim = Mathf.Max(r.width, r.height);
            float open = Ease(Mathf.Clamp01(t / 0.4f));

            // 1. White-hot flash.
            float flash = Mathf.Clamp01(1f - t / 0.3f);
            flash *= flash;
            if (flash > 0.001f)
            {
                Color f = Alpha(Color.Lerp(Color.white, primary, 0.25f), flash * 0.6f);
                Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), f, f, f, f);
            }

            // 2. Aura flames licking in from every screen edge.
            EdgeAura(vh, r, minDim * (final ? 0.2f : 0.16f) * (1f + 0.12f * Mathf.Sin(t * 4f)), t, master * 0.6f);

            // 3. Rotating god rays.
            int rays = final ? 28 : 20;
            float spinAngle = t * 0.32f + seed;
            Color clear = Alpha(primary, 0f);
            for (int i = 0; i < rays; i++)
            {
                float h = Hash(i + 1.7f);
                float a = spinAngle + i * Mathf.PI * 2f / rays;
                float half = 0.03f + 0.03f * h;
                float length = maxDim * (0.5f + 0.4f * h) * open * (0.85f + 0.15f * Mathf.Sin(t * 2.3f + i));
                Color col = Alpha(i % 2 == 0 ? primary : secondary, 0.3f * master);
                Tri(vh, c, col, c + Dir(a - half) * length, clear, c + Dir(a + half) * length, clear);
            }

            // 4. Breathing central aura.
            Glow(vh, c, minDim * (0.42f + 0.05f * Mathf.Sin(t * 3f)) * open, Alpha(primary, 0.42f * master), 40);
            Glow(vh, c, minDim * 0.2f * open, Alpha(Color.white, 0.22f * master), 28);

            // 5. Expanding shock rings.
            int ringCount = final ? 4 : 3;
            for (int k = 0; k < ringCount; k++)
            {
                float age = t - k * 0.14f;
                if (age <= 0f || age >= 1f) continue;
                float radius = maxDim * 0.75f * (1f - Mathf.Pow(1f - age, 3f));
                float thickness = Mathf.Lerp(42f, 4f, age);
                float alpha = Mathf.Pow(1f - age, 1.6f) * 0.85f * master;
                Ring(vh, c, radius, thickness, Alpha(k % 2 == 0 ? primary : secondary, alpha), 56);
            }

            // 6. Embers rising from below.
            int embers = final ? 90 : 64;
            float span = r.height * 1.2f;
            for (int i = 0; i < embers; i++)
            {
                float h1 = Hash(i * 1.31f), h2 = Hash(i * 2.17f + 3f), h3 = Hash(i * 3.73f + 7f), h4 = Hash(i * 5.11f + 11f);
                float speed = r.height * (0.12f + 0.25f * h2);
                float travel = Mathf.Repeat(t * speed + h3 * span, span);
                float life = travel / span;
                var p = new Vector2(r.xMin + h1 * r.width + Mathf.Sin(t * (0.8f + h2) + i) * 28f, r.yMin - r.height * 0.1f + travel);
                float size = 5f + 12f * h4;
                float flicker = 0.6f + 0.4f * Mathf.Sin(t * (9f + 7f * h1) + i);
                float a = master * (1f - life) * flicker * 0.9f;
                if (a <= 0.01f) continue;
                Glow(vh, p, size, Alpha(Color.Lerp(primary, secondary, h3), a), 8);
                Glow(vh, p, size * 0.35f, Alpha(Color.white, a), 6);
            }

            // 7. Twinkling star sparkles around the title.
            int sparkles = final ? 34 : 24;
            for (int i = 0; i < sparkles; i++)
            {
                float h = Hash(i * 7.7f + 1f);
                var p = new Vector2(c.x + (Hash(i * 3.3f) - 0.5f) * r.width * 0.8f, c.y + (Hash(i * 5.1f) - 0.5f) * r.height * 0.7f);
                float twinkle = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (2.5f + 3f * h) + h * 20f)), 6f);
                if (twinkle < 0.02f) continue;
                Spark(vh, p, (16f + 26f * h) * (0.6f + 0.4f * twinkle), Alpha(Color.Lerp(Color.white, primary, 0.3f), twinkle * master), t * 1.5f + h * 6f);
            }
        }

        void DrawIntro(VertexHelper vh, Rect r, float t, float master)
        {
            Vector2 c = r.center;
            float minDim = Mathf.Min(r.width, r.height);
            float band = Mathf.Min(r.height * 0.16f, 220f) * Ease(Mathf.Clamp01(t / 0.25f));
            Color core = Alpha(primary, 0.35f * master), clear = Alpha(primary, 0f);
            Quad(vh, new Vector2(r.xMin, c.y), new Vector2(r.xMax, c.y), new Vector2(r.xMax, c.y + band), new Vector2(r.xMin, c.y + band), core, core, clear, clear);
            Quad(vh, new Vector2(r.xMin, c.y), new Vector2(r.xMax, c.y), new Vector2(r.xMax, c.y - band), new Vector2(r.xMin, c.y - band), core, core, clear, clear);
            for (int i = 0; i < 26; i++)
            {
                float h = Hash(i * 1.9f), h2 = Hash(i * 4.3f + 2f), h3 = Hash(i * 6.1f + 5f);
                float y = c.y + (h - 0.5f) * band * 1.6f;
                float speed = r.width * (1.2f + 1.6f * h2);
                float x = r.xMin - r.width * 0.15f + Mathf.Repeat(t * speed + h3 * r.width, r.width * 1.3f);
                float length = 80f + 220f * h;
                float thick = 2f + 3f * h2;
                Color head = Alpha(Color.Lerp(Color.white, primary, h3), 0.8f * master);
                Color tail = Alpha(primary, 0f);
                Quad(vh, new Vector2(x - length, y - thick), new Vector2(x, y - thick), new Vector2(x, y + thick), new Vector2(x - length, y + thick), tail, head, head, tail);
            }
            EdgeAura(vh, r, minDim * 0.08f, t, master * 0.35f);
            float flash = Mathf.Clamp01(1f - t / 0.3f);
            if (flash > 0f) Glow(vh, c, minDim * 0.3f, Alpha(Color.white, flash * flash * 0.5f), 28);
        }

        void EdgeAura(VertexHelper vh, Rect r, float depth, float t, float alpha)
        {
            const int segments = 24;
            Color clear = Alpha(primary, 0f);
            for (int edge = 0; edge < 4; edge++)
            {
                Vector2 inward = edge == 0 ? Vector2.up : edge == 1 ? Vector2.down : edge == 2 ? Vector2.right : Vector2.left;
                for (int j = 0; j < segments; j++)
                {
                    float u0 = (float)j / segments, u1 = (float)(j + 1) / segments;
                    Vector2 o0 = EdgePoint(r, edge, u0), o1 = EdgePoint(r, edge, u1);
                    float d0 = depth * Flame(u0, edge, t), d1 = depth * Flame(u1, edge, t);
                    Color c0 = Alpha(Color.Lerp(primary, secondary, 0.5f + 0.5f * Mathf.Sin(u0 * 6f + t * 2f + edge)), alpha);
                    Color c1 = Alpha(Color.Lerp(primary, secondary, 0.5f + 0.5f * Mathf.Sin(u1 * 6f + t * 2f + edge)), alpha);
                    Quad(vh, o0, o1, o1 + inward * d1, o0 + inward * d0, c0, c1, clear, clear);
                }
            }
        }

        static float Flame(float u, int edge, float t) =>
            0.45f + 0.35f * (0.5f + 0.5f * Mathf.Sin(u * 13f + t * 3.1f + edge * 1.7f)) + 0.2f * (0.5f + 0.5f * Mathf.Sin(u * 29f - t * 5.3f + edge));

        static Vector2 EdgePoint(Rect r, int edge, float u)
        {
            switch (edge)
            {
                case 0: return new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), r.yMin);
                case 1: return new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), r.yMax);
                case 2: return new Vector2(r.xMin, Mathf.Lerp(r.yMin, r.yMax, u));
                default: return new Vector2(r.xMax, Mathf.Lerp(r.yMin, r.yMax, u));
            }
        }

        // ---------- geometry helpers ----------
        static UIVertex V(Vector2 p, Color c)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            return v;
        }

        internal static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int i = vh.currentVertCount;
            vh.AddVert(V(a, ca)); vh.AddVert(V(b, cb)); vh.AddVert(V(c, cc)); vh.AddVert(V(d, cd));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Tri(VertexHelper vh, Vector2 a, Color ca, Vector2 b, Color cb, Vector2 c, Color cc)
        {
            int i = vh.currentVertCount;
            vh.AddVert(V(a, ca)); vh.AddVert(V(b, cb)); vh.AddVert(V(c, cc));
            vh.AddTriangle(i, i + 1, i + 2);
        }

        static void Glow(VertexHelper vh, Vector2 center, float radius, Color inner, int segments)
        {
            if (radius <= 0.5f || inner.a <= 0.001f) return;
            int start = vh.currentVertCount;
            Color outer = Alpha(inner, 0f);
            vh.AddVert(V(center, inner));
            for (int s = 0; s <= segments; s++) vh.AddVert(V(center + Dir(s * Mathf.PI * 2f / segments) * radius, outer));
            for (int s = 0; s < segments; s++) vh.AddTriangle(start, start + 1 + s, start + 2 + s);
        }

        static void Ring(VertexHelper vh, Vector2 center, float radius, float thickness, Color color, int segments)
        {
            if (color.a <= 0.001f) return;
            int start = vh.currentVertCount;
            Color clear = Alpha(color, 0f);
            float inner = Mathf.Max(0f, radius - thickness), outer = radius + thickness * 0.35f;
            for (int s = 0; s <= segments; s++)
            {
                Vector2 d = Dir(s * Mathf.PI * 2f / segments);
                vh.AddVert(V(center + d * inner, clear));
                vh.AddVert(V(center + d * radius, color));
                vh.AddVert(V(center + d * outer, clear));
            }
            for (int s = 0; s < segments; s++)
            {
                int a = start + s * 3, b = a + 3;
                vh.AddTriangle(a, a + 1, b + 1); vh.AddTriangle(a, b + 1, b);
                vh.AddTriangle(a + 1, a + 2, b + 2); vh.AddTriangle(a + 1, b + 2, b + 1);
            }
        }

        static void Spark(VertexHelper vh, Vector2 center, float size, Color color, float rotation)
        {
            Color clear = Alpha(color, 0f);
            for (int arm = 0; arm < 2; arm++)
            {
                float a = rotation + arm * Mathf.PI * 0.5f;
                Vector2 along = Dir(a) * size, across = Dir(a + Mathf.PI * 0.5f) * size * 0.08f;
                int i = vh.currentVertCount;
                vh.AddVert(V(center, color));
                vh.AddVert(V(center + along, clear));
                vh.AddVert(V(center + across, clear));
                vh.AddVert(V(center - along, clear));
                vh.AddVert(V(center - across, clear));
                vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
                vh.AddTriangle(i, i + 3, i + 4); vh.AddTriangle(i, i + 4, i + 1);
            }
            Glow(vh, center, size * 0.3f, color, 10);
        }

        static Vector2 Dir(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        internal static Color Alpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
        static float Ease(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);
        float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f + seed * 311.7f) * 43758.5453f, 1f);
    }
}
