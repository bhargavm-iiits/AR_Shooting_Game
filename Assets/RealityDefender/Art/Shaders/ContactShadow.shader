Shader "RealityDefender/Contact Shadow"
{
    // Soft procedural ground-contact decal so the arena reads as resting on the real surface.
    Properties
    {
        _BaseColor("Shadow", Color) = (0, 0.01, 0.02, 0.62)
        _Inner("Solid radius", Range(0, 1)) = 0.42
        _Outer("Fade radius", Range(0, 1)) = 0.98
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" "IgnoreProjector"="True" }
        Pass
        {
            Name "ContactShadow"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Inner;
            half _Outer;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half r = length(i.uv * 2 - 1);
                half a = 1 - smoothstep(_Inner, _Outer, r);
                return half4(_BaseColor.rgb, _BaseColor.a * a * a);
            }
            ENDHLSL
        }
    }
}
