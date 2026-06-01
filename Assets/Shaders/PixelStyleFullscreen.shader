Shader "Hidden/Shadow/PixelStyleFullscreen"
{
    Properties
    {
        _PixelHeight ("Pixel Height", Range(24, 240)) = 64
        _ColorLevels ("Color Levels", Range(2, 16)) = 5
        _EdgeStrength ("Edge Strength", Range(0, 4)) = 1.25
        _Blend ("Blend", Range(0, 1)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1.18
        _Warmth ("Warmth", Range(-1, 1)) = 0.12
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.16
        _GridStrength ("Block Grid Strength", Range(0, 1)) = 0.18
        _GridWidth ("Block Grid Width", Range(0.01, 0.2)) = 0.055
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "PixelStyle"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelHeight;
            float _ColorLevels;
            float _EdgeStrength;
            float _Blend;
            float _Saturation;
            float _Warmth;
            float _DitherStrength;
            float _GridStrength;
            float _GridWidth;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Luma(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float3 ApplyPalette(float3 color, float2 pixelCoord)
            {
                float lum = Luma(color);
                color = lerp(lum.xxx, color, _Saturation);

                float warmth = saturate(_Warmth * 0.5 + 0.5);
                float3 cool = color * float3(0.92, 0.98, 1.08);
                float3 warm = color * float3(1.08, 1.02, 0.90);
                color = lerp(cool, warm, warmth);

                float levels = max(2.0, _ColorLevels);
                float noise = (Hash21(pixelCoord) - 0.5) * _DitherStrength / levels;
                color = saturate(color + noise);
                color = floor(color * levels + 0.5) / levels;
                return saturate(color);
            }

            float GetBlockGridMask(float2 uv, float2 grid)
            {
                float2 cell = frac(uv * grid);
                float2 edgeDistance = min(cell, 1.0 - cell);
                float edge = 1.0 - min(edgeDistance.x, edgeDistance.y) / max(_GridWidth, 0.0001);
                return saturate(edge);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = saturate(input.texcoord.xy);
                float aspect = max(_ScreenParams.x / max(_ScreenParams.y, 1.0), 0.001);
                float pixelHeight = max(8.0, _PixelHeight);
                float2 grid = float2(max(8.0, floor(pixelHeight * aspect)), pixelHeight);
                float2 pixelCoord = floor(uv * grid);
                float2 pixelUv = (pixelCoord + 0.5) / grid;
                float2 pixelSize = 1.0 / grid;

                float3 original = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel).rgb;
                float3 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, pixelUv, _BlitMipLevel).rgb;

                float l = Luma(color);
                float lLeft = Luma(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, pixelUv + float2(-pixelSize.x, 0), _BlitMipLevel).rgb);
                float lRight = Luma(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, pixelUv + float2(pixelSize.x, 0), _BlitMipLevel).rgb);
                float lUp = Luma(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, pixelUv + float2(0, pixelSize.y), _BlitMipLevel).rgb);
                float lDown = Luma(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, pixelUv + float2(0, -pixelSize.y), _BlitMipLevel).rgb);
                float edge = max(max(abs(l - lLeft), abs(l - lRight)), max(abs(l - lUp), abs(l - lDown)));
                edge = saturate(edge * _EdgeStrength);

                color = ApplyPalette(color, pixelCoord);
                color = lerp(color, color * 0.62, edge);

                float gridMask = GetBlockGridMask(uv, grid);
                float3 gridTint = color * 0.52;
                color = lerp(color, gridTint, gridMask * _GridStrength);

                return half4(lerp(original, saturate(color), saturate(_Blend)), 1.0);
            }
            ENDHLSL
        }
    }
}
