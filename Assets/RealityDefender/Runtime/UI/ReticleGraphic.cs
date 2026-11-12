using UnityEngine;
using UnityEngine.UI;

namespace RealityDefender
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReticleGraphic : MaskableGraphic
    {
        public bool fireControl;
        public float thickness = 2.5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .43f;
            Vector2 c = rect.center;
            if (fireControl)
            {
                const int steps = 96;
                Color fill = color;
                fill.a *= .22f;
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps, b = (i + 1) * Mathf.PI * 2f / steps;
                    TechGraphic.Triangle(vh, c, c + Unit(a) * radius, c + Unit(b) * radius, fill);
                }
                Arc(vh, c, radius, thickness * 1.5f, 0, Mathf.PI * 2f, color);
                Color outer = color; outer.a *= .65f;
                for (int i = 0; i < 4; i++) Arc(vh, c, radius * 1.1f, thickness, i * Mathf.PI / 2f + .10f, i * Mathf.PI / 2f + 1.35f, outer);
                Arc(vh, c, radius * .92f, 1f, 0, Mathf.PI * 2f, outer);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2f;
                    Arc(vh, c, radius, thickness, a + .22f, a + 1.34f, color);
                    Vector2 dir = Unit(a), tangent = Unit(a + Mathf.PI / 2f) * thickness * .5f;
                    TechGraphic.Quad(vh, c + dir * radius * .72f - tangent, c + dir * radius * 1.2f - tangent,
                        c + dir * radius * 1.2f + tangent, c + dir * radius * .72f + tangent, color);
                }
                Arc(vh, c, 2f, 2f, 0, Mathf.PI * 2f, color);
            }
        }

        static Vector2 Unit(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        static void Arc(VertexHelper vh, Vector2 center, float radius, float width, float from, float to, Color color)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((to - from) * radius / 6f));
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.Lerp(from, to, (float)i / steps), b = Mathf.Lerp(from, to, (float)(i + 1) / steps);
                TechGraphic.Quad(vh, center + Unit(a) * radius, center + Unit(b) * radius,
                    center + Unit(b) * (radius - width), center + Unit(a) * (radius - width), color);
            }
        }
    }
}
