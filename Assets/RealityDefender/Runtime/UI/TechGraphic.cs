using UnityEngine;
using UnityEngine.UI;

namespace RealityDefender
{
    /// <summary>Small, texture-free mesh for the game's clipped metal HUD plates.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TechGraphic : MaskableGraphic
    {
        public Color borderColor = new Color(.12f, .85f, 1f, .9f);
        public float borderWidth = 2f;
        public float cut = 20f;
        public bool accents = true;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float c = Mathf.Min(cut, Mathf.Min(r.width, r.height) * .28f);
            Vector2[] points = Points(r, c);
            Vector2 center = r.center;
            for (int i = 0; i < 8; i++)
                Triangle(vh, center, points[i], points[(i + 1) % 8], color);
            var inner = new Rect(r.xMin + borderWidth, r.yMin + borderWidth,
                r.width - borderWidth * 2f, r.height - borderWidth * 2f);
            Vector2[] inset = Points(inner, Mathf.Max(0, c - borderWidth * .5f));
            for (int i = 0; i < 8; i++) Quad(vh, points[i], points[(i + 1) % 8], inset[(i + 1) % 8], inset[i], borderColor);
            if (accents)
            {
                float length = Mathf.Min(75f, r.width * .15f);
                Quad(vh, new Vector2(r.xMin + c + 6, r.yMax), new Vector2(r.xMin + c + length, r.yMax),
                    new Vector2(r.xMin + c + length - 4, r.yMax - 5), new Vector2(r.xMin + c + 2, r.yMax - 5), borderColor);
                Quad(vh, new Vector2(r.xMax - c - length, r.yMin + 5), new Vector2(r.xMax - c - 2, r.yMin + 5),
                    new Vector2(r.xMax - c - 6, r.yMin), new Vector2(r.xMax - c - length + 4, r.yMin), borderColor);
            }
        }

        static Vector2[] Points(Rect r, float c) => new[]
        {
            new Vector2(r.xMin+c,r.yMin), new Vector2(r.xMax-c,r.yMin), new Vector2(r.xMax,r.yMin+c),
            new Vector2(r.xMax,r.yMax-c), new Vector2(r.xMax-c,r.yMax), new Vector2(r.xMin+c,r.yMax),
            new Vector2(r.xMin,r.yMax-c), new Vector2(r.xMin,r.yMin+c)
        };

        internal static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }

        internal static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
