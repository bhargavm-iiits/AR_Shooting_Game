Shader "RealityDefender/Placement Ring"
{
    // Soft ground placeholder ring drawn procedurally on a flat quad (UV 0..1 spans the quad).
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 0.85)
        _Radius("Ring radius (0..1 of quad)", Range(0.1, 1)) = 0.86
        _Width("Ring width", Range(0.01, 0.5)) = 0.11
        _Softness("Edge softness", Range(0.001, 0.2)) = 0.035
        _Fill("Inner fill alpha", Range(0, 1)) = 0.10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "PlacementRing"
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
            half _Radius;
            half _Width;
            half _Softness;
            half _Fill;
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
                half inner = _Radius - _Width;
                half ring = smoothstep(inner - _Softness, inner + _Softness, r) * (1 - smoothstep(_Radius - _Softness, _Radius + _Softness, r));
                half fill = (1 - smoothstep(inner - _Softness, inner + _Softness, r)) * _Fill;
                return half4(_BaseColor.rgb, _BaseColor.a * saturate(ring + fill));
            }
            ENDHLSL
        }
    }
}
