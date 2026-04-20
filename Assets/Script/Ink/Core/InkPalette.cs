using UnityEngine;

/// <summary>
/// 墨の色パレット（チーム共有）
/// 
/// ■ このクラスのルール:
/// - 色の追加はここに色を追加するだけでOK
/// - 色番号は絶対に変更しない（既存の保存データが壊れるため）
/// - 最大256色まで追加可能
/// 
/// ■ 使い方:
///   byte colorId = InkPalette.ID_RED;
///   InkPaintService.Paint(hit, radius, density, colorId);
/// </summary>
public static class InkPalette
{
    // ── 色番号定数（チーム全体で共有） ──
    // 新しい色を追加するときはここに追加
    // 既存のIDは絶対に変更しない
    public const byte ID_BLACK  = 0;  // 黒墨（デフォルト）
    public const byte ID_RED    = 1;  // 赤墨
    public const byte ID_GOLD   = 2;  // 金墨
    public const byte ID_BLUE   = 3;  // 青墨
    public const byte ID_GREEN  = 4;  // 緑墨
    public const byte ID_PURPLE = 5;  // 紫墨
    public const byte ID_WHITE  = 6;  // 白墨（黒い面用）
    public const byte ID_SILVER = 7;  // 銀墨

    // ── 色パレット ──
    // Index が色番号、値が実際のColor
    private static readonly Color[] palette = new Color[]
    {
        new Color(0.02f, 0.02f, 0.05f), // 0: 黒墨
        new Color(0.80f, 0.15f, 0.15f), // 1: 赤墨
        new Color(0.90f, 0.75f, 0.20f), // 2: 金墨
        new Color(0.15f, 0.30f, 0.70f), // 3: 青墨
        new Color(0.15f, 0.55f, 0.25f), // 4: 緑墨
        new Color(0.55f, 0.25f, 0.70f), // 5: 紫墨
        new Color(0.95f, 0.95f, 0.90f), // 6: 白墨
        new Color(0.80f, 0.80f, 0.85f), // 7: 銀墨
    };

    /// <summary>色番号数</summary>
    public static int Count => palette.Length;

    /// <summary>色番号からColorを取得</summary>
    public static Color GetColor(byte colorId)
    {
        if (colorId >= palette.Length) return palette[0];
        return palette[colorId];
    }

    // ====================================================================
    //  パレットテクスチャ（シェーダーに送る用）
    // ====================================================================

    private static Texture2D paletteTexture;

    /// <summary>
    /// シェーダー用のパレットテクスチャを取得
    /// 256x1のテクスチャで、X座標が色番号に対応
    /// 初回呼び出し時に自動生成される
    /// </summary>
    public static Texture2D GetPaletteTexture()
    {
        if (paletteTexture == null)
            BuildPaletteTexture();
        return paletteTexture;
    }

    private static void BuildPaletteTexture()
    {
        paletteTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,  // 色番号をぴったり引くためBilinearにしない
            wrapMode = TextureWrapMode.Clamp,
            name = "InkPalette"
        };

        Color[] pixels = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            pixels[i] = i < palette.Length ? palette[i] : palette[0];
        }
        paletteTexture.SetPixels(pixels);
        paletteTexture.Apply();
    }
}
