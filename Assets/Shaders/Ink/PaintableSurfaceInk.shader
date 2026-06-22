// PaintableSurfaceInk シェーダー（旧フラットライティング + ノーマルマップ対応）
//
// ■ 方針:
// - 旧の見た目（フラットライティング, 最低明るさ0.5, 青空を拾わない）を維持
// - そこに「塗った/塗ってない」で別のノーマルマップを足すだけ
// - PBR/環境アンビエント/環境反射は使わない（＝床が青く/暗くならない）
// - メタルネスは無し（メタルネスは環境反射＝青空を映すため、ここでは扱わない）
//
// ■ _InkTex / _InkColorTex / _InkPalette は InkSurfaceRenderer から自動セット

Shader "Ink/PaintableSurfaceInk"
{
    Properties
    {
        _BaseColor ("地面の色", Color) = (0.85, 0.82, 0.75, 1)

        [Header(Painted ground  Base)]
        _BaseTex ("Base Map", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(Unpainted ground  Osumi)]
        _OsumiTex ("Osumi Base Map", 2D) = "black" {}
        [Normal] _OsumiBumpMap ("Osumi Normal Map", 2D) = "bump" {}
        _OsumiBumpScale ("Osumi Normal Scale", Float) = 1.0

        [Header(Ink Textures auto set)]
        _InkTex ("Ink Density", 2D) = "black" {}
        _InkColorTex ("Ink Color ID", 2D) = "black" {}
        _InkPalette ("Ink Palette", 2D) = "black" {}

        [Header(Style)]
        [Toggle] _EnableGrayscale ("World Grayscale Under Ink", Float) = 1
        _GrayscaleStrength ("Grayscale Strength", Range(0, 1)) = 1.0
        _InkColorStrength ("Ink Color Strength", Range(0, 1)) = 0.7

        [Header(Nijimi Bleed)]
        [Toggle] _EnableBleed ("滲みエッジ ON", Float) = 1
        _BleedStrength ("滲みの強さ", Range(0, 1)) = 0.5
        _BleedScale ("滲みノイズの大きさ", Range(1, 50)) = 12

        [Header(Lighting flat)]
        _LightFloor ("最低明るさ", Range(0, 1)) = 0.5
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
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseTex_ST;
                float  _BumpScale;
                float  _OsumiBumpScale;
                float  _GrayscaleStrength;
                float  _InkColorStrength;
                float  _BleedStrength;
                float  _BleedScale;
                float  _EnableBleed;
                float  _EnableGrayscale;
                float  _LightFloor;
            CBUFFER_END

            TEXTURE2D(_BaseTex);      SAMPLER(sampler_BaseTex);
            TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OsumiTex);     SAMPLER(sampler_OsumiTex);
            TEXTURE2D(_OsumiBumpMap); SAMPLER(sampler_OsumiBumpMap);
            TEXTURE2D(_InkTex);       SAMPLER(sampler_InkTex);
            TEXTURE2D(_InkColorTex);  SAMPLER(sampler_InkColorTex);
            TEXTURE2D(_InkPalette);   SAMPLER(sampler_InkPalette);

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i + float2(0, 0));
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float fbm(float2 p)
            {
                float total = 0.0, amp = 0.5, freq = 1.0;
                for (int i = 0; i < 3; i++)
                {
                    total += valueNoise(p * freq) * amp;
                    freq *= 2.0; amp *= 0.5;
                }
                return total;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS  = nrm.normalWS;
                output.tangentWS = float4(nrm.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv        = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseTex);

                // --- 墨データ ---
                float density  = SAMPLE_TEXTURE2D(_InkTex, sampler_InkTex, input.uv).r;
                float colorIdN = SAMPLE_TEXTURE2D(_InkColorTex, sampler_InkColorTex, input.uv).r;
                if (_EnableBleed > 0.5)
                {
                    float bleedNoise = fbm(input.uv * _BleedScale);
                    float edgeFactor = density * (1.0 - density) * 4.0;
                    density = saturate(density + (bleedNoise - 0.5) * _BleedStrength * edgeFactor);
                }
                float3 inkColor = SAMPLE_TEXTURE2D(_InkPalette, sampler_InkPalette, float2(colorIdN, 0.5)).rgb;
                float painted = density > 0.001 ? 1.0 : 0.0;

                // --- albedo（塗り側: グレースケール＋墨 / 未塗り側: オスミツキ）---
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, baseUV) * _BaseColor;
                half3 paintedAlbedo = baseCol.rgb;
                if (_EnableGrayscale > 0.5)
                {
                    float gray = dot(baseCol.rgb, float3(0.299, 0.587, 0.114));
                    paintedAlbedo = lerp(paintedAlbedo, float3(gray, gray, gray), _GrayscaleStrength);
                }
                float inkAlpha = smoothstep(0.0, 0.4, density) * _InkColorStrength;
                paintedAlbedo = lerp(paintedAlbedo, inkColor, inkAlpha);

                half3 osumiAlbedo = (SAMPLE_TEXTURE2D(_OsumiTex, sampler_OsumiTex, baseUV) * _BaseColor).rgb;
                half3 finalRgb = lerp(osumiAlbedo, paintedAlbedo, painted);

                // --- ノーマルマップ（塗り状態で別マップ）→ ワールド法線 ---
                float3 nBase  = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseUV), _BumpScale);
                float3 nOsumi = UnpackNormalScale(SAMPLE_TEXTURE2D(_OsumiBumpMap, sampler_OsumiBumpMap, baseUV), _OsumiBumpScale);
                float3 normalTS = lerp(nOsumi, nBase, painted);
                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                float3 N = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                // --- 旧フラットライティング（青空を拾わない・最低明るさあり）---
                // ノーマルマップで揺らいだ N を使うので、凹凸が陰影に出る
                float3 lightDir = normalize(float3(0.5, 1.0, 0.3));
                float NdotL = saturate(dot(N, lightDir));
                finalRgb *= (NdotL * (1.0 - _LightFloor) + _LightFloor);

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
