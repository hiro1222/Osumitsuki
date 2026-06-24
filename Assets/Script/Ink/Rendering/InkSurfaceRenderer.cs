using UnityEngine;

/// <summary>
/// インクの描画責務（責務分割 P2-Step3 → F: 描画解像度の分離）。
///
/// ■ F の方針:
/// - density(見た目) は「高解像度の RenderTexture」に GPU でブラシをスプラットする。
///   → CPU配列(density)とは独立。解像度を上げても CPU コスト・メモリは増えない。
/// - colorId(色) は低解像度のまま CPU から Texture2D にアップロード（色境界は粗くてOK）。
/// - density(コリジョン/歩行判定用の CPU 配列)は PaintableSurface 側にそのまま残る。
///
/// ■ 注意:
/// - シェーダ(Ink/PaintableSurfaceInk)は無改造。_InkTex に RenderTexture を差すだけ。
/// - GPUブラシは Hidden/InkBrushSplat（Always Included Shaders 登録が必要）。
/// - GPUスプラットはUV空間の円なので、UV島をまたぐ複雑メッシュでは見た目が島を越えて
///   にじむ可能性がある（平面/単一UV島なら問題なし）。コリジョン/歩行はCPU側で3D距離
///   チェック済みなので影響しない。
/// </summary>
internal class InkSurfaceRenderer
{
    private RenderTexture densityRT;   // 高解像度・GPU描画（density, R8）
    private Texture2D colorTexture;    // 低解像度・CPUアップロード（colorId, R8）
    private Material brushMat;         // Hidden/InkBrushSplat
    private MaterialPropertyBlock propBlock;
    private Renderer meshRenderer;
    private int visualRes;

    private static readonly int ID_InkTex      = Shader.PropertyToID("_InkTex");
    private static readonly int ID_InkColorTex = Shader.PropertyToID("_InkColorTex");
    private static readonly int ID_InkPalette  = Shader.PropertyToID("_InkPalette");
    private static readonly int ID_Brush       = Shader.PropertyToID("_Brush");
    private static readonly int ID_FlipX       = Shader.PropertyToID("_FlipX");
    private static readonly int ID_FlipY       = Shader.PropertyToID("_FlipY");

    /// <summary>
    /// 高解像度densityRT・低解像度colorテクスチャ・GPUブラシを生成し、マテリアルに送る。
    /// </summary>
    /// <param name="gridW">CPUグリッド幅（colorId解像度）</param>
    /// <param name="gridH">CPUグリッド高さ（colorId解像度）</param>
    /// <param name="visualResolution">見た目RTの解像度（grid解像度と独立。上げても軽い）</param>
    /// <param name="brushShader">Hidden/InkBrushSplat</param>
    /// <param name="flipX">墨が左右反転して見える場合のみ true</param>
    /// <param name="flipY">墨が上下反転して見える場合のみ true</param>
    public void Init(Renderer renderer, int gridW, int gridH,
                     int visualResolution, Shader brushShader, bool flipX, bool flipY)
    {
        meshRenderer = renderer;
        visualRes = Mathf.Max(64, visualResolution);

        densityRT = new RenderTexture(visualRes, visualRes, 0, RenderTextureFormat.R8)
        {
            name = "InkDensityRT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        densityRT.Create();
        ClearVisual();   // 黒(density=0)で初期化

        colorTexture = new Texture2D(gridW, gridH, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        if (brushShader != null)
        {
            brushMat = new Material(brushShader) { hideFlags = HideFlags.HideAndDontSave };
            brushMat.SetFloat(ID_FlipX, flipX ? 1f : 0f);
            brushMat.SetFloat(ID_FlipY, flipY ? 1f : 0f);
        }

        propBlock = new MaterialPropertyBlock();
        BindTextures();
    }

    /// <summary>マテリアルに3枚のテクスチャを束ねて送る。</summary>
    private void BindTextures()
    {
        if (meshRenderer == null) return;
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture(ID_InkTex, densityRT);
        propBlock.SetTexture(ID_InkColorTex, colorTexture);
        propBlock.SetTexture(ID_InkPalette, InkPalette.GetPaletteTexture());
        meshRenderer.SetPropertyBlock(propBlock);
    }

    /// <summary>density RT にブラシを加算（塗り）。</summary>
    public void SplatAdd(Vector2 uv, float uvRadius, byte density)
    {
        Splat(uv, uvRadius, density / 255f, pass: 0);
    }

    /// <summary>density RT からブラシ分を減算（消し）。</summary>
    public void SplatErase(Vector2 uv, float uvRadius)
    {
        Splat(uv, uvRadius, 1f, pass: 1);
    }

    private void Splat(Vector2 uv, float uvRadius, float strength, int pass)
    {
        if (brushMat == null || densityRT == null) return;

        brushMat.SetVector(ID_Brush, new Vector4(uv.x, uv.y, Mathf.Max(uvRadius, 1e-4f), strength));

        RenderTexture prev = RenderTexture.active;
        Graphics.SetRenderTarget(densityRT);
        brushMat.SetPass(pass);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
        RenderTexture.active = prev;
    }

    /// <summary>density RT を全消去（黒）。</summary>
    public void ClearVisual()
    {
        if (densityRT == null) return;
        RenderTexture prev = RenderTexture.active;
        Graphics.SetRenderTarget(densityRT);
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = prev;
    }

    /// <summary>colorId(低解像度) をテクスチャに書き込み、マテリアルに送る。</summary>
    public void UploadColor(byte[] colorId)
    {
        if (meshRenderer == null || colorTexture == null) return;
        colorTexture.SetPixelData(colorId, 0);
        colorTexture.Apply(false);
        BindTextures();
    }

    /// <summary>RT・テクスチャ・マテリアルを破棄する（PaintableSurface.OnDestroyから呼ぶ）。</summary>
    public void Dispose()
    {
        if (densityRT != null) { densityRT.Release(); Object.Destroy(densityRT); }
        if (colorTexture != null) Object.Destroy(colorTexture);
        if (brushMat != null) Object.Destroy(brushMat);
    }
}
