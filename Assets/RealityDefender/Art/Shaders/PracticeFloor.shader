Shader "RealityDefender/Practice Floor"
{
    Properties { _BaseColor("Floor",Color)=(0.035,0.055,0.085,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p=i.world.xz;
                float2 cell=abs(frac(p*4-.5)-.5)/max(fwidth(p*4),.0001);
                float gridLine=1-saturate(min(cell.x,cell.y));
                float2 major=abs(frac(p-.5)-.5)/max(fwidth(p),.0001);
                float wide=1-saturate(min(major.x,major.y));
                float fade=exp(-length(p)*.15);
                float grain=frac(sin(dot(floor(p*700),float2(12.9898,78.233)))*43758.5453);
                half3 col=_BaseColor.rgb*(.90+grain*.13);
                col+=half3(.03,.16,.20)*(gridLine*.32+wide*.28)*fade;
                col+=half3(.015,.028,.043)*exp(-length(p)*.5);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
