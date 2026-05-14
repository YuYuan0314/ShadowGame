Shader "Custom/VolumetricBeam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.5)
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir     : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDir = GetWorldSpaceViewDir(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ---- distance attenuation: bright near light, fade toward far end ----
                half distAttn = 1.0 - pow(IN.uv.y, 0.6);

                // ---- fresnel silhouette: softens at grazing/view edges ----
                float3 worldViewDir = normalize(IN.viewDir);
                float3 worldNormal = normalize(IN.worldNormal);
                half NdotV = abs(dot(worldNormal, worldViewDir));
                // _EdgeSoftness=0: crisp silhouette, no edge darkening
                // _EdgeSoftness=1: wide soft edge via fresnel
                half edgeFade = pow(saturate(1.0 - NdotV), lerp(24.0, 1.5, _EdgeSoftness));
                half silhouette = lerp(1.0, 1.0 - edgeFade * 0.65, _EdgeSoftness);

                half alpha = distAttn * silhouette * _Color.a;
                alpha = saturate(alpha);

                return half4(_Color.rgb, alpha);
            }

            ENDHLSL
        }
    }
}
