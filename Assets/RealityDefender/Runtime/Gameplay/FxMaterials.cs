using System.Collections.Generic;
using UnityEngine;

namespace RealityDefender
{
    /// <summary>
    /// Runtime materials for the procedural effects. The shaders live in a Resources folder so they are always shipped
    /// with the player build without any scene or prefab wiring. A missing or unsupported shader degrades gracefully:
    /// the effect is skipped rather than drawn magenta.
    /// </summary>
    public static class FxMaterials
    {
        static Material glow, uiAdditive, film, shadow;
        static Mesh sphere, quad;
        static bool lightTintReady, shadowUnavailable;

        /// <summary>Global real-world light tint (ARCore light estimation) read by the bubble film; white by default.</summary>
        public static readonly int LightTintId = Shader.PropertyToID("_RD_LightTint");

        public static Material Glow => glow ? glow : glow = Make("RD_AdditiveGlow", "Runtime Lance Glow");
        public static Material UIAdditive => uiAdditive ? uiAdditive : uiAdditive = Make("RD_UIAdditive", "Runtime UI Aura");
        public static Material Film
        {
            get
            {
                EnsureLightTint();
                return film ? film : film = Make("RD_BubbleFilm", "Runtime Bubble Film");
            }
        }

        /// <summary>Soft ground-contact decal (the arena's own contact-shadow shader), shared by every bubble shadow.</summary>
        public static Material Shadow
        {
            get
            {
                if (shadow || shadowUnavailable) return shadow;
                var shader = Shader.Find("RealityDefender/Contact Shadow");
                if (!shader || !shader.isSupported)
                {
                    shadowUnavailable = true;
                    Debug.LogWarning("Reality Defender FX: contact shadow shader is " + (shader ? "unsupported" : "missing") + "; bubble shadows skipped.");
                    return null;
                }
                return shadow = new Material(shader) { name = "Runtime Bubble Shadow" };
            }
        }

        /// <summary>Horizontal quad of half-size 1 with 0..1 UVs, facing up.</summary>
        public static Mesh Quad
        {
            get
            {
                if (quad) return quad;
                quad = new Mesh { name = "Unit ground quad" };
                quad.SetVertices(new List<Vector3> { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) });
                quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }

        public static void EnsureLightTint()
        {
            if (lightTintReady) return;
            lightTintReady = true;
            Shader.SetGlobalColor(LightTintId, Color.white);
        }

        static Material Make(string resource, string name)
        {
            var shader = Resources.Load<Shader>(resource);
            if (!shader || !shader.isSupported)
            {
                Debug.LogWarning("Reality Defender FX: shader " + resource + " is unavailable; effect skipped.");
                return null;
            }
            return new Material(shader) { name = name };
        }

        /// <summary>Unit-radius UV sphere with normals, shared by every bubble shell and fragment.</summary>
        public static Mesh Sphere
        {
            get
            {
                if (sphere) return sphere;
                const int rings = 18, segments = 28;
                var vertices = new List<Vector3>((rings + 1) * (segments + 1));
                var normals = new List<Vector3>(vertices.Capacity);
                var triangles = new List<int>(rings * segments * 6);
                for (int r = 0; r <= rings; r++)
                {
                    float v = (float)r / rings;
                    float theta = v * Mathf.PI;
                    for (int s = 0; s <= segments; s++)
                    {
                        float phi = (float)s / segments * Mathf.PI * 2f;
                        var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                        vertices.Add(n);
                        normals.Add(n);
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < segments; s++)
                    {
                        int a = r * (segments + 1) + s, b = a + segments + 1;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                        triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                    }
                sphere = new Mesh { name = "Bubble sphere" };
                sphere.SetVertices(vertices);
                sphere.SetNormals(normals);
                sphere.SetTriangles(triangles, 0);
                // The film shader displaces vertices outward by up to 12 * _Wobble (0.06 max) = 72% of the radius;
                // grow the bounds to match so the shell is never culled while still visible at the screen edge.
                sphere.bounds = new Bounds(Vector3.zero, Vector3.one * 2f * 1.75f);
                return sphere;
            }
        }

        /// <summary>A soft additive LineRenderer using the shared glow material.</summary>
        public static LineRenderer Line(string name, Transform parent, bool loop = false, bool worldSpace = true)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Glow;
            line.useWorldSpace = worldSpace;
            line.loop = loop;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.allowOcclusionWhenDynamic = false;
            line.positionCount = 0;
            return line;
        }

        public static Gradient Fade(Color tail, Color head, float tailAlpha = 0f, float headAlpha = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(tail, 0f), new GradientColorKey(head, 1f) },
                new[] { new GradientAlphaKey(tailAlpha, 0f), new GradientAlphaKey(headAlpha * 0.85f, 0.7f), new GradientAlphaKey(headAlpha, 1f) });
            return g;
        }
    }
}
