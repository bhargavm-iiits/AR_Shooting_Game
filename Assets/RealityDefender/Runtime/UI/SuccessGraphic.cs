using UnityEngine;
using UnityEngine.UI;

namespace RealityDefender
{
    /// <summary>Font-independent completion check.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SuccessGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            Vector2 a = new Vector2(r.xMin + r.width * .18f, r.yMin + r.height * .48f);
            Vector2 b = new Vector2(r.xMin + r.width * .43f, r.yMin + r.height * .25f);
            Vector2 c = new Vector2(r.xMin + r.width * .85f, r.yMin + r.height * .77f);
            Segment(vh, a, b, r.height * .10f, color);
            Segment(vh, b, c, r.height * .10f, color);
        }

        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var dir = (b - a).normalized;
            var normal = new Vector2(-dir.y, dir.x) * width * .5f;
            TechGraphic.Quad(vh, a - normal, b - normal, b + normal, a + normal, color);
        }
    }
}
