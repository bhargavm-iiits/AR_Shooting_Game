using UnityEngine;

namespace RealityDefender
{
    /// <summary>
    /// Iridescent soap-film shell drawn around a bubble. Per-instance shader state (wobble, rupture, tint) goes through a
    /// MaterialPropertyBlock so every bubble shares one material.
    /// </summary>
    public sealed class BubbleShell : MonoBehaviour
    {
        static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        static readonly int PopId = Shader.PropertyToID("_Pop");
        static readonly int ImpactId = Shader.PropertyToID("_ImpactDir");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int SwirlId = Shader.PropertyToID("_Swirl");

        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        float seed;
        Color tint;

        /// <summary>Adds a film shell of the given local radius under <paramref name="parent"/>, or returns null without the shader.</summary>
        public static BubbleShell Create(Transform parent, float radius, Color tint, float seed, string name = "Bubble film")
        {
            Material film = FxMaterials.Film;
            if (!film) return null;
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * radius;
            go.GetComponent<MeshFilter>().sharedMesh = FxMaterials.Sphere;
            var shell = go.AddComponent<BubbleShell>();
            shell.meshRenderer = go.GetComponent<MeshRenderer>();
            shell.meshRenderer.sharedMaterial = film;
            shell.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell.meshRenderer.receiveShadows = false;
            shell.block = new MaterialPropertyBlock();
            shell.seed = seed;
            shell.tint = tint;
            shell.Apply(0.003f, 0f, Vector3.forward, 1f, 1f);
            return shell;
        }

        public Color Tint { get => tint; set => tint = value; }

        public void Apply(float wobble, float pop, Vector3 impactDirection, float opacity, float swirl)
        {
            if (!meshRenderer) return;
            block.SetFloat(WobbleId, wobble);
            block.SetFloat(PopId, Mathf.Clamp01(pop));
            block.SetVector(ImpactId, impactDirection.sqrMagnitude > 1e-6f ? impactDirection.normalized : Vector3.forward);
            block.SetFloat(SeedId, seed);
            block.SetColor(TintId, tint);
            block.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            block.SetFloat(SwirlId, swirl);
            meshRenderer.SetPropertyBlock(block);
        }

        public void SetVisible(bool visible)
        {
            if (meshRenderer) meshRenderer.enabled = visible;
        }
    }
}
