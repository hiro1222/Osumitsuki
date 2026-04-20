Shader "Ink/GroundInk_Simple"
{
    // Phase 1 用の最小限シェーダー
    // ワールドXZ座標からグリッドテクスチャをサンプリングして黒く塗る
    // 滲み・和紙テクスチャはPhase 2で追加する

    Properties
    {
        _BaseColor ("地面の色", Color) = (0.85, 0.82, 0.75, 1) // 和紙っぽいベージュ
        _InkTex ("Ink Texture (自動設定)", 2D) = "black" {}
        _InkColorDark ("濃墨の色", Color) = (0.02, 0.02, 0.05, 1)
        _InkColorLight ("淡墨の色", Color) = (0.15, 0.14, 0.18, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "InkGround"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            // マテリアルプロパティ
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _InkColorDark;
                float4 _InkColorLight;
            CBUFFER_END

            // インクテクスチャ
            TEXTURE2D(_InkTex);
            SAMPLER(sampler_InkTex);

            // MaterialPropertyBlock から送られるパラメータ
            float4 _InkGridOrigin;  // xy = originX, originZ
            float4 _InkGridSize;    // xy = widthM, depthM

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ── ① ワールドXZ → inkTexture UV変換 ──
                float2 inkUV = float2(
                    (input.positionWS.x - _InkGridOrigin.x) / _InkGridSize.x,
                    (input.positionWS.z - _InkGridOrigin.y) / _InkGridSize.y
                );

                // グリッド範囲外は地面色のまま
                if (inkUV.x < 0 || inkUV.x > 1 || inkUV.y < 0 || inkUV.y > 1)
                    return _BaseColor;

                // ── ② density取得 ──
                // R8テクスチャなので .r に 0〜1 の値が入る
                float rawDensity = SAMPLE_TEXTURE2D(_InkTex, sampler_InkTex, inkUV).r;

                // 塗られていなければ地面色を返す
                if (rawDensity < 0.004) // density 1/255 未満
                    return _BaseColor;

                // ── ③ 濃淡で墨色を補間 ──
                // density低い → 淡墨、density高い → 濃墨
                float3 inkColor = lerp(_InkColorLight.rgb, _InkColorDark.rgb, rawDensity);

                // ── ④ 地面色と墨色をブレンド ──
                // rawDensityが低い（薄い墨）ほど地面色が透ける
                float inkAlpha = smoothstep(0.0, 0.4, rawDensity);
                float3 finalColor = lerp(_BaseColor.rgb, inkColor, inkAlpha);

                // 簡易ライティング（URP MainLight）
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(input.normalWS, mainLight.direction));
                float3 lit = finalColor * (NdotL * 0.6 + 0.4) * mainLight.color;

                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
