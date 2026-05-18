// マスク塗りシェーダー（累積版）
//
// ■ 動作:
// 累積RT（_MaskTex）の値をブレンド係数として、ベース色と墨色を補間
// マスク値が0 = ベース色、1 = 墨色、中間 = グラデーション
//
// マスクの濃さは MaskedInkProgress 側で各マスクの strength で制御済み

Shader "Ink/MaskedInk"
{
    Properties
    {
        _BaseColor ("ベース色", Color) = (1, 1, 1, 1)
        _BaseTex ("ベーステクスチャ", 2D) = "white" {}

        [Header(Mask)]
        _MaskTex ("Mask Accum (auto set)", 2D) = "black" {}

        [Header(Ink Color)]
        _InkColor ("墨の色", Color) = (0.02, 0.02, 0.05, 1)

        [Header(Threshold)]
        _Threshold ("Threshold (この値超で墨色)", Range(0, 1)) = 0.3
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "MaskedInk"
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
                float4 _BaseColor;
                float4 _BaseTex_ST;
                float4 _MaskTex_ST;
                float4 _InkColor;
                float _Threshold;
                float _EdgeSoftness;
            CBUFFER_END

            TEXTURE2D(_BaseTex);  SAMPLER(sampler_BaseTex);
            TEXTURE2D(_MaskTex);  SAMPLER(sampler_MaskTex);

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
                // ベース色
                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseTex);
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, baseUV) * _BaseColor;

                // 累積マスクの値（0=ベース色、1=完全に墨色）
                float maskValue = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).r;

                // 閾値判定: maskValueが_Thresholdを超えたら墨色になる
                // smoothstepで境界を少しだけソフトにしてジャギーを防ぐ
                float blend = smoothstep(_Threshold - _EdgeSoftness, _Threshold + _EdgeSoftness, maskValue);

                // ベース色と墨色をブレンド（中間値は出ない、ほぼ二値）
                half3 finalRgb = lerp(baseCol.rgb, _InkColor.rgb, blend);

                // 簡易ライティング（塗られた部分は影響を弱める）
                float3 lightDir = normalize(float3(0.5, 1.0, 0.3));
                float NdotL = saturate(dot(input.normalWS, lightDir));
                float baseLighting = NdotL * 0.5 + 0.5;
                // blend=0 のところは通常ライティング、=1 のところはライティング影響なし（真っ黒のまま）
                float lighting = lerp(baseLighting, 1.0, blend);
                finalRgb *= lighting;

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
