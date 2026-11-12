Shader "RealityDefender/Energy Particle"
{
    // Texture-free additive glow for the orb blast. Particle vertex colour drives tint and fade.
    // _Ring = 0: soft hot core falloff (flash, plasma, sparks, embers). _Ring = 1: thin shockwave ring.
    Properties
    {
        _Ring("Ring mode", Range(0, 1)) = 0
        _Intensity("Intensity", Range(0, 4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Name "EnergyParticle"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half _Ring;
            half _Intensity;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half r = length(i.uv * 2 - 1);
                // Hot white-ish centre with a soft halo.
                half core = saturate(1 - r);
                core = core * core * (0.55 + 0.45 * core);
                half hot = pow(saturate(1 - r * 1.6), 3);
                // Thin bright ring with soft inner glow for the shockwave.
                half ring = saturate(1 - abs(r - 0.82) / 0.12);
                ring = ring * ring + saturate(1 - r) * 0.12;
                half shape = lerp(core, ring, _Ring);
                half3 rgb = i.color.rgb * _Intensity + hot * (1 - _Ring) * 0.8;
                return half4(rgb, saturate(i.color.a * shape));
            }
            ENDHLSL
        }
    }
}
