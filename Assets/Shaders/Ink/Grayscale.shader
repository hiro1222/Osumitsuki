// 白黒変換シェーダー
// ■ 使い方:
// 1. このファイルを Assets/Shaders/ に配置
// 2. マテリアルを作成 → Shader を "Custom/Grayscale" に設定
// 3. _BaseMap に木目テクスチャを設定
// 4. Grayscale Amount で白黒の強度を調整（0=元の色, 1=完全白黒）

Shader "Custom/Grayscale"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)

        [Header(Grayscale)]
        _GrayscaleAmount ("Grayscale Amount", Range(0, 1)) = 1.0
        _Brightness ("Brightness", Range(0, 2)) = 1.0
        _Contrast ("Contrast", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "GrayscaleForward"
            Tags { "LightMode"="UniversalForward" }

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
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _GrayscaleAmount;
                float _Brightness;
                float _Contrast;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // テクスチャをサンプリング
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // グレースケール変換
                // 人間の目が感じる明るさに合わせた重み付け（Rec.709係数）
                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));

                // 元の色とグレーを補間
                col.rgb = lerp(col.rgb, float3(gray, gray, gray), _GrayscaleAmount);

                // 明るさ・コントラスト調整
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;
                col.rgb *= _Brightness;

                // 簡易ライティング
                float3 lightDir = normalize(float3(0.5, 1.0, 0.3));
                float NdotL = saturate(dot(input.normalWS, lightDir));
                col.rgb *= (NdotL * 0.5 + 0.5);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
