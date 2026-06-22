// マスク塗りシェーダー（フラット拡散 + 濡れ墨スペキュラ + ノーマルマップ）
//
// ■ 方針:
// - PBR（UniversalFragmentPBR）は使わない＝環境(青空)を反射しない＝青くならない
// - 拡散は旧フラット（最低明るさあり）。床(PaintableSurfaceInk)と同じ固定光で陰影を揃える。
// - 「濡れ墨スペキュラ」: albedoに依存しない“加算ハイライト”を法線マップで揺らす
//   → 真っ黒な墨の上でも凸凹がテカリで浮く。墨部分は _InkWetness でツヤ増し。
//
// ■ _MaskTex は MaskedInkProgress 側から自動セット。
// ■ 注意: Always Included Shaders に入れないこと（FallBackのURP/Litを巻き込んでビルド激遅）。

Shader "Ink/MaskedInk"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseTex ("Base Map", 2D) = "white" {}

        [Header(Normal)]
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(Wet Ink Sheen  nuregumi)]
        _SpecColor ("ツヤの色", Color) = (1, 1, 1, 1)
        _SpecStrength ("ツヤの強さ", Range(0, 2)) = 0.5
        _Shininess ("ツヤの鋭さ", Range(1, 256)) = 32
        _InkWetness ("墨のツヤ倍率", Range(0, 4)) = 2.0

        [Header(Lighting flat)]
        _LightFloor ("最低明るさ", Range(0, 1)) = 0.5

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
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float  fogCoord   : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseTex_ST;
                float  _BumpScale;
                float4 _SpecColor;
                float  _SpecStrength;
                float  _Shininess;
                float  _InkWetness;
                float  _LightFloor;
                float4 _InkColor;
                float  _Threshold;
                float  _EdgeSoftness;
            CBUFFER_END

            TEXTURE2D(_BaseTex); SAMPLER(sampler_BaseTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = nrmInputs.normalWS;
                output.tangentWS  = float4(nrmInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv         = input.uv;
                output.fogCoord   = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseTex);

                // --- albedo: ベース色↔墨色（マスクでブレンド）---
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, baseUV) * _BaseColor;
                float maskValue = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).r;
                float blend = smoothstep(_Threshold - _EdgeSoftness, _Threshold + _EdgeSoftness, maskValue);
                half3 albedo = lerp(baseCol.rgb, _InkColor.rgb, blend);

                // --- ノーマルマップ → ワールド法線 ---
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseUV), _BumpScale);
                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                float3 N = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                // --- 固定光（床と同じ）＋ビュー方向 ---
                float3 lightDir = normalize(float3(0.5, 1.0, 0.3));
                float3 viewDir  = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfDir  = normalize(lightDir + viewDir);
                float NdotL = saturate(dot(N, lightDir));
                float NdotH = saturate(dot(N, halfDir));

                // --- 拡散（フラット・最低明るさあり）: 明るいベース部分の凸凹用 ---
                half3 diffuse = albedo * (NdotL * (1.0 - _LightFloor) + _LightFloor);

                // --- 濡れ墨スペキュラ（加算・albedo非依存）: 黒い墨の上でも凸凹がテカる ---
                float wet = lerp(1.0, _InkWetness, blend);   // 墨部分でツヤ増し
                half3 specular = _SpecColor.rgb * (pow(NdotH, _Shininess) * _SpecStrength * wet);

                half3 finalRgb = diffuse + specular;
                finalRgb = MixFog(finalRgb, input.fogCoord);
                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
