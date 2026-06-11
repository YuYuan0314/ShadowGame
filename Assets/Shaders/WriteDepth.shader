Shader "Hidden/WriteDepth"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "WriteDepth"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float depth : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.depth = output.positionCS.z / max(output.positionCS.w, 0.00001);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return float4(saturate(input.depth), 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
