using UnityEngine;
using UnityEngine.UI;

namespace RealityDefender
{
    /// <summary>
    /// Physically simulated confetti, evaluated in closed form from time so it is frame-rate independent:
    /// two cannons fire from the bottom corners, then a rain falls from the top. Each piece flies under gravity with
    /// linear air drag (so it reaches a fluttering terminal velocity), sways, spins, and tumbles in 3D (its width and
    /// shading follow a flip angle).
    /// </summary>
    public sealed class ConfettiGraphic : Graphic
    {
        public float time;
        public float duration = 3f;
        public Color[] palette;
        public bool final;
        public float seed;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (palette == null || palette.Length == 0 || r.width <= 1f) return;
            float master = Mathf.Clamp01((duration - time) / 0.6f);
            if (master <= 0f) return;
            int count = final ? 180 : 120;
            int cannons = Mathf.RoundToInt(count * 0.65f);
            const float drag = 1.7f;
            var gravity = new Vector2(0f, -r.height * 1.5f);
            Vector2 terminal = gravity / drag;
            for (int i = 0; i < count; i++)
            {
                float h1 = Hash(i * 1.13f), h2 = Hash(i * 2.71f + 1f), h3 = Hash(i * 3.97f + 2f);
                float h4 = Hash(i * 5.23f + 3f), h5 = Hash(i * 7.19f + 4f), h6 = Hash(i * 9.41f + 5f);
                bool cannon = i < cannons;
                float age = time - (cannon ? h1 * 0.15f : 0.2f + h1 * 1.1f);
                if (age < 0f) continue;
                Vector2 p0, v0;
                if (cannon)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    p0 = new Vector2(side < 0f ? r.xMin + 30f : r.xMax - 30f, r.yMin + 30f);
                    v0 = new Vector2(-side * r.width * (0.25f + 0.5f * h2), r.height * (1.15f + 0.7f * h3));
                }
                else
                {
                    p0 = new Vector2(r.xMin + h2 * r.width, r.yMax + 40f);
                    v0 = new Vector2((h3 - 0.5f) * 120f, -r.height * 0.05f);
                }
                // Linear-drag ballistics: p = p0 + vt*t + (v0 - vt)(1 - e^-kt)/k, with vt the terminal velocity.
                float decay = 1f - Mathf.Exp(-drag * age);
                Vector2 p = p0 + terminal * age + (v0 - terminal) * (decay / drag);
                p.x += Mathf.Sin(age * (4f + 4f * h4) + h5 * 10f) * 22f * Mathf.Min(1f, age * 2f);
                if (p.y < r.yMin - 40f || p.x < r.xMin - 60f || p.x > r.xMax + 60f) continue;
                float angle = h5 * 6.283f + age * (3f + 6f * h6) * (h4 < 0.5f ? -1f : 1f);
                float flip = Mathf.Cos(age * (6f + 8f * h2) + h3 * 5f);
                float width = 12f + 10f * h3;
                float height = (6f + 5f * h6) * Mathf.Max(0.15f, Mathf.Abs(flip));
                Color col = palette[i % palette.Length];
                float shade = 0.65f + 0.35f * Mathf.Abs(flip);
                col = new Color(col.r * shade, col.g * shade, col.b * shade, master);
                RotatedQuad(vh, p, width, height, angle, col);
            }
        }

        static void RotatedQuad(VertexHelper vh, Vector2 center, float width, float height, float angle, Color color)
        {
            Vector2 x = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (width * 0.5f);
            Vector2 y = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * (height * 0.5f);
            AuraBurstGraphic.Quad(vh, center - x - y, center + x - y, center + x + y, center - x + y, color, color, color, color);
        }

        float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f + seed * 311.7f) * 43758.5453f, 1f);
    }
}
