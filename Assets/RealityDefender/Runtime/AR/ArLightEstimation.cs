using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RealityDefender
{
    /// <summary>
    /// Makes the virtual arena respond to the real room's lighting. ARCore's ambient light estimate (average brightness
    /// and colour correction, the cheapest estimation mode) drives the key light's intensity and colour and the bubble
    /// film's global tint, smoothed so the image never flickers. Restores the authored lighting when AR stops.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public sealed class ArLightEstimation : MonoBehaviour
    {
        public Light keyLight;
        ARCameraManager cameraManager;
        float baseIntensity = 1f;
        Color baseColor = Color.white;
        bool captured;
        float lastEstimateAt = -10f;
        float targetScale = 1f, currentScale = 1f;
        Color targetTint = Color.white, currentTint = Color.white;

        public void Bind(Light light)
        {
            if (light == keyLight && captured) return;
            // Hand the previous light back exactly as authored before taking over a new one.
            if (keyLight && captured)
            {
                keyLight.intensity = baseIntensity;
                keyLight.color = baseColor;
            }
            keyLight = light;
            captured = keyLight;
            if (captured)
            {
                baseIntensity = keyLight.intensity;
                baseColor = keyLight.color;
            }
        }

        void OnEnable()
        {
            if (keyLight && !captured) Bind(keyLight); // Inspector-assigned light
            cameraManager = GetComponent<ARCameraManager>();
            cameraManager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor;
            cameraManager.frameReceived += OnFrame;
        }

        void OnDisable()
        {
            if (cameraManager) cameraManager.frameReceived -= OnFrame;
            targetScale = currentScale = 1f;
            targetTint = currentTint = Color.white;
            Apply();
        }

        void OnFrame(ARCameraFrameEventArgs args)
        {
            lastEstimateAt = Time.unscaledTime;
            ARLightEstimationData estimate = args.lightEstimation;
            if (estimate.averageBrightness.HasValue)
                // ARCore pixel intensity is ~0.5 in a normally lit room; keep the virtual light in a believable band.
                targetScale = Mathf.Clamp(estimate.averageBrightness.Value * 2f, 0.4f, 1.35f);
            if (estimate.colorCorrection.HasValue)
            {
                Color c = estimate.colorCorrection.Value;
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (max > 1e-4f) targetTint = new Color(c.r / max, c.g / max, c.b / max, 1f);
            }
        }

        void Update()
        {
            // No camera frames for a second (AR session stopped or paused): ease back to the authored lighting.
            if (Time.unscaledTime - lastEstimateAt > 1f)
            {
                targetScale = 1f;
                targetTint = Color.white;
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f);
            currentScale = Mathf.Lerp(currentScale, targetScale, k);
            currentTint = Color.Lerp(currentTint, targetTint, k);
            Apply();
        }

        void Apply()
        {
            if (keyLight && captured)
            {
                keyLight.intensity = baseIntensity * currentScale;
                keyLight.color = baseColor * currentTint;
            }
            Shader.SetGlobalColor(FxMaterials.LightTintId, currentTint * Mathf.Clamp(currentScale, 0.55f, 1.15f));
        }
    }
}
