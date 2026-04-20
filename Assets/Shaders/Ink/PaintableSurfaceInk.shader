// 設計書 第5章: PaintableSurfaceInk シェーダー（色対応+グレースケール版）
//
// ■ 機能:
// - メッシュのUV1でdensity/colorId/paletteを読む
// - 墨が塗られた場所は元の色をグレースケール化（墨絵表現、方式D）
// - 色番号から色パレットを引いて墨の色を決定
//
// ■ テクスチャ:
// _InkTex       : density (R8, 0-255)
// _InkColorTex  : 色番号 (R8, 0-255)
// _InkPalette   : 色パレット (256x1 RGBA32)

Shader "Ink/PaintableSurfaceInk"
{
    Properties
    {
        _BaseColor ("地面の色", Color) = (0.85, 0.82, 0.75, 1)
        _BaseTex ("地面のテクスチャ", 2D) = "white" {}
        _OsumiTex ("オスミツキテクスチャ (R: density, G: colorId, B: palette)", 2D) = "black" {}

        [Header(Ink Textures (auto set))]
        _InkTex ("Ink Density", 2D) = "black" {}
        _InkColorTex ("Ink Color ID", 2D) = "black" {}
        _InkPalette ("Ink Palette", 2D) = "black" {}

        [Header(Style)]
        [Toggle] _EnableGrayscale ("World Grayscale Under Ink", Float) = 1
        _GrayscaleStrength ("Grayscale Strength", Range(0, 1)) = 1.0
        _InkColorStrength ("Ink Color Strength", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "PaintableSurfaceInk"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ENABLEGRAYSCALE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseTex_ST;
                float _GrayscaleStrength;
                float _InkColorStrength;
            CBUFFER_END

            TEXTURE2D(_BaseTex);     SAMPLER(sampler_BaseTex);
            TEXTURE2D(_OsumiTex);    SAMPLER(sampler_OsumiTex);
            TEXTURE2D(_InkTex);      SAMPLER(sampler_InkTex);
            TEXTURE2D(_InkColorTex); SAMPLER(sampler_InkColorTex);
            TEXTURE2D(_InkPalette);  SAMPLER(sampler_InkPalette);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ── 地面の色 ──
                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseTex);
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, baseUV) * _BaseColor;

                // ── 墨のデータを読む ──
                float density  = SAMPLE_TEXTURE2D(_InkTex, sampler_InkTex, input.uv).r;
                float colorIdN = SAMPLE_TEXTURE2D(_InkColorTex, sampler_InkColorTex, input.uv).r;

                // ── 色パレットから墨の色を取得 ──
                // colorIdN は 0-1 正規化されているので、そのままパレットのUとして使える
                float3 inkColor = SAMPLE_TEXTURE2D(_InkPalette, sampler_InkPalette,
                                                    float2(colorIdN, 0.5)).rgb;

                half3 finalRgb = baseCol.rgb;

                if (density > 0.004)
                {
                    #ifdef _ENABLEGRAYSCALE_ON
                    // ── 方式D: 墨が塗られた場所は元の色をグレースケール化 ──
                    float gray = dot(baseCol.rgb, float3(0.299, 0.587, 0.114));
                    float3 grayRgb = float3(gray, gray, gray);
                    // density で グレースケール化
                    finalRgb = lerp(finalRgb, grayRgb, density * _GrayscaleStrength);
                    #endif

                    // ── 墨の色をグレー（or 元の色）の上に重ねる ──
                    float inkAlpha = smoothstep(0.0, 0.4, density) * _InkColorStrength;
                    finalRgb = lerp(finalRgb, inkColor, inkAlpha);
                }
                else 
                {
                    half4 OsumiCol = SAMPLE_TEXTURE2D(_OsumiTex, sampler_OsumiTex, baseUV) * _BaseColor;
                    finalRgb = OsumiCol.rgb;
                }

                // ── 簡易ライティング ──
                float3 lightDir = normalize(float3(0.5, 1.0, 0.3));
                float NdotL = saturate(dot(input.normalWS, lightDir));
                finalRgb *= (NdotL * 0.5 + 0.5);

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
