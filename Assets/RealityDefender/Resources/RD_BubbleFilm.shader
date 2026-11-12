Shader "RealityDefender/Bubble Film"
{
    // Soap-film shell around each energy target.
    //  - Thin-film interference: hue bands from a drifting thickness field that drains downward like real soap film,
    //    shifted by viewing angle, so the colours swirl and change as the phone moves.
    //  - Fresnel-weighted opacity (clear centre, bright rim) plus two sharp "window" reflections.
    //  - Surface-tension wobble: low-order standing waves displace the vertices (_Wobble spikes on impact).
    //  - Rupture: _Pop tears the film open starting at _ImpactDir with a glowing edge, like a bubble bursting.
    //  - Room reflection and real-world light tint (ARCore light estimation via the global _RD_LightTint).
    // Back faces are drawn first (dimmer) so the far side of the film is visible through the near side.
    Properties
    {
        _Tint("Tint", Color) = (0.55, 0.9, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 1
        _Wobble("Wobble", Range(0, 0.06)) = 0.003
        _Pop("Rupture", Range(0, 1)) = 0
        _ImpactDir("Impact direction (object space)", Vector) = (0, 0, 1, 0)
        _Seed("Seed", Float) = 0
        _Swirl("Swirl speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _Tint;
        half _Opacity;
        float _Wobble;
        half _Pop;
        float4 _ImpactDir;
        float _Seed;
        float _Swirl;
        CBUFFER_END

        // Real-world light tint from ARCore light estimation (white when unavailable). Set globally from C#.
        half4 _RD_LightTint;

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionOS : TEXCOORD2;
        };

        float Hash3(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float Noise3(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x),
                             lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                             lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        Varyings Vert(Attributes v)
        {
            Varyings o;
            float t = _Time.y + _Seed;
            float3 p = v.positionOS.xyz;
            float3 n = normalize(v.normalOS);
            float r = max(length(p), 1e-4);
            float3 d = p / r;
            // Low-order standing waves, independent of mesh scale.
            float w = sin(t * 4.1 + d.y * 3.1) * 0.45
                    + sin(t * 5.7 + d.x * 2.6 + 1.3) * 0.35
                    + sin(t * 3.3 + d.z * 3.5 + 2.1) * 0.20;
            p += n * (w * _Wobble * r * 12.0);
            o.positionOS = d;
            o.positionWS = TransformObjectToWorld(p);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(n);
            return o;
        }

        half4 Shade(Varyings i, float backFace)
        {
            float t = _Time.y * _Swirl + _Seed;
            float3 n = normalize(i.normalWS) * (backFace > 0.5 ? -1.0 : 1.0);
            float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
            float ndv = saturate(abs(dot(n, v)));
            float fres = pow(1.0 - ndv, 2.4);

            float3 q = i.positionOS * 2.2;
            // Film thickness: drains downward over time, with swirling convection cells.
            float thick = i.positionOS.y * 0.85
                        + Noise3(q + float3(0.0, t * 0.55, t * 0.21)) * 1.4
                        + Noise3(q * 2.3 - float3(t * 0.37, 0.0, 0.0)) * 0.45
                        + t * 0.11;
            float viewShift = (1.0 - ndv) * 1.3;
            float3 iri = 0.5 + 0.5 * cos(6.28318 * (thick + viewShift + float3(0.0, 0.33, 0.67)));
            float3 col = lerp(_Tint.rgb, iri, 0.78);

            float3 refl = reflect(-v, n);
            float spec = pow(saturate(dot(refl, normalize(float3(0.45, 0.8, 0.35)))), 90.0) * 2.4
                       + pow(saturate(dot(refl, normalize(float3(-0.6, 0.35, -0.2)))), 36.0) * 0.55;

            half alpha = saturate(0.045 + fres * 0.9);
            half3 rgb = col * alpha + spec * _Tint.rgb * (1.0 - _Pop);

            // Room reflection: a real bubble mirrors its surroundings, brightest at grazing angles. Bright ceiling and
            // windows above the horizon, a darker warm floor below it, with a soft horizon line.
            float up = refl.y;
            float3 ceiling = float3(0.86, 0.93, 1.0);
            float3 floorColor = float3(0.24, 0.2, 0.17);
            float3 room = lerp(floorColor, ceiling, smoothstep(-0.3, 0.35, up));
            room += (1.0 - smoothstep(0.0, 0.08, abs(up))) * 0.25;
            rgb += room * fres * 0.4 * (1.0 - _Pop);

            // Match the real room: dimmer and warmer/cooler with the measured light, but never fully dark (the core glows).
            half3 lightTint = lerp(half3(1.0, 1.0, 1.0), saturate(_RD_LightTint.rgb), 0.65);
            rgb *= lightTint;

            if (_Pop > 0.0)
            {
                // Tear opens from the impact point and races around the sphere with a ragged edge.
                float along = dot(i.positionOS, normalize(_ImpactDir.xyz)) * 0.5 + 0.5;
                float tear = (1.0 - along) + Noise3(i.positionOS * 7.0) * 0.32;
                float front = _Pop * 1.45;
                clip(tear - front);
                float edge = saturate(1.0 - (tear - front) / 0.09);
                rgb += _Tint.rgb * edge * 2.2 + edge * 0.6;
                alpha = saturate(alpha + edge * 0.8);
            }

            return half4(rgb * _Opacity, alpha * _Opacity);
        }
        ENDHLSL

        Pass
        {
            Name "FilmBack"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBack
            half4 FragBack(Varyings i) : SV_Target { return Shade(i, 1.0) * 0.5; }
            ENDHLSL
        }

        Pass
        {
            Name "FilmFront"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFront
            half4 FragFront(Varyings i) : SV_Target { return Shade(i, 0.0); }
            ENDHLSL
        }
    }
}
