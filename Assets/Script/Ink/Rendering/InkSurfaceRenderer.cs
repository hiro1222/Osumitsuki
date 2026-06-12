using UnityEngine;

/// <summary>
/// インクの density/colorId 配列を R8 テクスチャに焼き込み、メッシュのマテリアルに送る。
/// PaintableSurface から切り出した描画責務（責務分割 P2-Step3）。
/// </summary>
internal class InkSurfaceRenderer
{
    private Texture2D densityTexture;
    private Texture2D colorTexture;
    private MaterialPropertyBlock propBlock;
    private Renderer meshRenderer;

    /// <summary>描画用テクスチャ(gridW×gridH, R8)とプロパティブロックを生成する。</summary>
    public void Init(Renderer renderer, int gridW, int gridH)
    {
        meshRenderer = renderer;

        densityTexture = new Texture2D(gridW, gridH, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        colorTexture = new Texture2D(gridW, gridH, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        propBlock = new MaterialPropertyBlock();
    }

    /// <summary>density/colorId をテクスチャに書き込み、マテリアルに送る。</summary>
    public void Upload(byte[] density, byte[] colorId)
    {
        if (meshRenderer == null) return;

        densityTexture.SetPixelData(density, 0);
        densityTexture.Apply(false);

        colorTexture.SetPixelData(colorId, 0);
        colorTexture.Apply(false);

        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture("_InkTex", densityTexture);
        propBlock.SetTexture("_InkColorTex", colorTexture);
        propBlock.SetTexture("_InkPalette", InkPalette.GetPaletteTexture());
        meshRenderer.SetPropertyBlock(propBlock);
    }

    /// <summary>テクスチャを破棄する（PaintableSurface.OnDestroyから呼ぶ）。</summary>
    public void Dispose()
    {
        if (densityTexture != null) Object.Destroy(densityTexture);
        if (colorTexture != null) Object.Destroy(colorTexture);
    }
}
