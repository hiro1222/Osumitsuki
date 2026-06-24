// GPUインク用ブラシスプラット（F: 描画解像度の分離）
//
// density用の RenderTexture(R8) に、UV空間で円を「加算(塗り)/減算(消し)」する裏方シェーダ。
// PaintableSurface(InkSurfaceRenderer)から Shader.Find("Hidden/InkBrushSplat") + DrawProceduralNow で使う。
//
// ■ _Brush = (中心UV.x, 中心UV.y, UV半径U, UV半径V) / _BrushStrength = 強さ
//   U/V別半径＝楕円(UV)で、非一様スケールの板でも世界では真円になる
// ■ _FlipY = 1 で上下反転（墨が上下逆に出る環境用の保険）
// ■ 注意: マテリアル参照0・Shader.Findのみなので、ビルドでは
//         Graphics > Always Included Shaders に登録すること（MaskAdditiveBlendと同じ扱い）。

Shader "Hidden/InkBrushSplat"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always

        HLSLINCLUDE
        float4 _Brush;   // (中心uv.x, 中心uv.y, UV半径U, UV半径V)
        float  _BrushStrength;
        float  _FlipX;
        float  _FlipY;

        struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

        // 頂点ID から全画面三角形を生成（メッシュ不要・行列不要）
        Varyings vert(uint id : SV_VertexID)
        {
            Varyings o;
            float2 uv = float2((id << 1) & 2, id & 2);   // (0,0)(2,0)(0,2)
            o.uv = uv;
            o.pos = float4(uv * 2.0 - 1.0, 0.0, 1.0);
            return o;
        }

        half4 frag(Varyings i) : SV_Target
        {
            float2 s = float2(lerp(i.uv.x, 1.0 - i.uv.x, _FlipX),
                              lerp(i.uv.y, 1.0 - i.uv.y, _FlipY));
            // U/V別半径で正規化 → 楕円(UV)＝世界で真円。d=1.0 が縁
            float dx = (s.x - _Brush.x) / max(_Brush.z, 1e-5);
            float dy = (s.y - _Brush.y) / max(_Brush.w, 1e-5);
            float d  = sqrt(dx * dx + dy * dy);
            float a  = 1.0 - smoothstep(0.8, 1.0, d);   // 中心ベタ＋柔らかい縁
            return half4(a * _BrushStrength, 0.0, 0.0, 0.0);
        }
        ENDHLSL

        // Pass 0: 加算（塗り）  dst += src
        Pass
        {
            Name "Add"
            Blend One One
            BlendOp Add
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        // Pass 1: 減算（消し）  dst -= src
        Pass
        {
            Name "Erase"
            Blend One One
            BlendOp RevSub
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
