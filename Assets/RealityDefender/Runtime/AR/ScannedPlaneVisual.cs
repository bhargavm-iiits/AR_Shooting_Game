using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace RealityDefender
{
    /// <summary>Low-cost, collider-free dots clipped to the provider's actual plane polygon.</summary>
    public sealed class ScannedPlaneVisual : MonoBehaviour
    {
        GameObject visualRoot;
        Mesh dots;
        LineRenderer border;
        readonly List<Vector3> vertices = new List<Vector3>(2400);
        readonly List<int> triangles = new List<int>(3600);

        public void Initialize(Material material)
        {
            if (visualRoot) return;
            visualRoot = new GameObject("Detected Plane Dots");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            dots = new Mesh { name = "Tracked polygon dot mesh" };
            dots.MarkDynamic();
            visualRoot.AddComponent<MeshFilter>().sharedMesh = dots;
            MeshRenderer renderer = visualRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var edgeObject = new GameObject("Detected Plane Border");
            edgeObject.transform.SetParent(visualRoot.transform, false);
            border = edgeObject.AddComponent<LineRenderer>();
            border.sharedMaterial = material;
            border.useWorldSpace = false;
            border.loop = true;
            border.widthMultiplier = 0.0035f;
            border.numCornerVertices = 0;
            border.numCapVertices = 0;
            border.shadowCastingMode = ShadowCastingMode.Off;
            border.receiveShadows = false;
        }

        public void Show(ARPlane plane)
        {
            if (!visualRoot || !plane || !plane.boundary.IsCreated || plane.boundary.Length < 3) { Hide(); return; }
            visualRoot.SetActive(true);
            NativeArray<Vector2> polygon = plane.boundary;
            Vector2 minimum = polygon[0], maximum = polygon[0];
            for (int i = 1; i < polygon.Length; i++)
            {
                minimum = Vector2.Min(minimum, polygon[i]);
                maximum = Vector2.Max(maximum, polygon[i]);
            }
            border.positionCount = polygon.Length;
            for (int i = 0; i < polygon.Length; i++) border.SetPosition(i, new Vector3(polygon[i].x, 0f, polygon[i].y));
            float area = (maximum.x - minimum.x) * (maximum.y - minimum.y);
            float spacing = Mathf.Max(0.07f, Mathf.Sqrt(Mathf.Max(0.01f, area) / 500f));
            float dotRadius = Mathf.Min(0.004f, spacing * 0.06f);
            vertices.Clear();
            triangles.Clear();
            float firstX = Mathf.Ceil(minimum.x / spacing) * spacing;
            float firstZ = Mathf.Ceil(minimum.y / spacing) * spacing;
            for (float x = firstX; x <= maximum.x; x += spacing)
            {
                for (float z = firstZ; z <= maximum.y; z += spacing)
                {
                    if (!Contains(polygon, new Vector2(x, z))) continue;
                    AddQuad(vertices, triangles, new Vector3(x - dotRadius, 0f, z - dotRadius),
                        new Vector3(x + dotRadius, 0f, z - dotRadius), new Vector3(x + dotRadius, 0f, z + dotRadius),
                        new Vector3(x - dotRadius, 0f, z + dotRadius));
                }
            }
            dots.Clear();
            dots.SetVertices(vertices);
            dots.SetTriangles(triangles, 0);
            dots.RecalculateBounds();
        }

        public void Hide() { if (visualRoot) visualRoot.SetActive(false); }

        public static bool Contains(NativeArray<Vector2> polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static void AddQuad(List<Vector3> points, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = points.Count;
            points.Add(a); points.Add(b); points.Add(c); points.Add(d);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
            indices.Add(start); indices.Add(start + 3); indices.Add(start + 2);
        }

        public static GameObject CreateReticle(Material material)
        {
            var root = new GameObject("Segmented Surface Reticle");
            var points = new List<Vector3>(36);
            var indices = new List<int>(54);
            const float inner = 0.071f, outer = 0.078f;
            for (int segment = 0; segment < 8; segment++)
            {
                float a = (segment * 45f + 8f) * Mathf.Deg2Rad;
                float b = (segment * 45f + 37f) * Mathf.Deg2Rad;
                AddQuad(points, indices, new Vector3(Mathf.Cos(a) * inner, 0f, Mathf.Sin(a) * inner),
                    new Vector3(Mathf.Cos(a) * outer, 0f, Mathf.Sin(a) * outer),
                    new Vector3(Mathf.Cos(b) * outer, 0f, Mathf.Sin(b) * outer),
                    new Vector3(Mathf.Cos(b) * inner, 0f, Mathf.Sin(b) * inner));
            }
            var mesh = new Mesh { name = "Segmented placement ring" };
            mesh.SetVertices(points);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var lifetime = root.AddComponent<ScannedPlaneVisual>();
            lifetime.dots = mesh;
            return root;
        }

        void OnDestroy()
        {
            if (dots) Destroy(dots);
            if (visualRoot) Destroy(visualRoot);
        }
    }
}
