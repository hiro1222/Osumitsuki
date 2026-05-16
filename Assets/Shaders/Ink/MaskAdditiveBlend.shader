// 累積マスクブレンドシェーダー（裏方、MaskedInkProgressのGraphics.Blit用）
//
// ■ 動作:
// 出力 = saturate(_PrevTex + _NewMask × _NewStrength)
//
// ユーザーが触る必要はない（MaskedInkProgressから内部的に使われるだけ）

Shader "Hidden/MaskAdditiveBlend"
{
    Properties
    {
        _PrevTex ("Prev Accum", 2D) = "black" {}
        _NewMask ("New Mask", 2D) = "black" {}
        _NewStrength ("New Strength", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_PrevTex);  SAMPLER(sampler_PrevTex);
            TEXTURE2D(_NewMask);  SAMPLER(sampler_NewMask);
            float _NewStrength;

            Varyings vert(Attributes input)
            {
                Varyings output;
                // フルスクリーン四角形を頂点IDから生成
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float prev = SAMPLE_TEXTURE2D(_PrevTex, sampler_PrevTex, input.uv).r;
                float newMask = SAMPLE_TEXTURE2D(_NewMask, sampler_NewMask, input.uv).r;
                float result = saturate(prev + newMask * _NewStrength);
                return half4(result, result, result, 1.0);
            }
            ENDHLSL
        }
    }
}
