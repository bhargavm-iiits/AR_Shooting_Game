Shader "RealityDefender/Additive Glow"
{
    // Texture-free additive light for line/trail effects (plasma lance strands, ion rings, muzzle arcs).
    // _Radial = 0: soft glow across the strip width with a white-hot centre line (LineRenderer UV.y spans the width).
    // _Radial = 1: round soft glow for quads.
    Properties
    {
        _Intensity("Intensity", Range(0, 6)) = 1.8
        _Radial("Radial", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Name "AdditiveGlow"
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
            half _Intensity;
            half _Radial;
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
                half across = saturate(1.0 - abs(i.uv.y * 2.0 - 1.0));
                half strip = pow(across, 1.6);
                half hotStrip = pow(across, 9.0);
                half r = length(i.uv * 2.0 - 1.0);
                half radial = pow(saturate(1.0 - r), 2.0);
                half hotRadial = pow(saturate(1.0 - r * 1.8), 3.0);
                half shape = lerp(strip, radial, _Radial);
                half hot = lerp(hotStrip, hotRadial, _Radial);
                half3 rgb = i.color.rgb * _Intensity * shape + hot * 0.9;
                return half4(rgb, saturate(i.color.a * shape));
            }
            ENDHLSL
        }
    }
}
