using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 墨の斬撃テクスチャを自動生成するエディタツール
/// 
/// ■ 使い方:
/// メニュー > Ink > Generate Slash Textures を実行
/// → Assets/Textures/Ink/ に5枚のテクスチャが生成される
///   - SlashHorizontal.png  横一文字（三日月弧）
///   - SlashVertical.png    縦斬り（縦の筆跡）
///   - SlashDiagonal.png    斜め斬り（斜めの筆跡）
///   - SlashCircle.png      円弧（リング状）
///   - SlashTrail.png       Trail Renderer用（横グラデーション）
///
/// 生成後、各テクスチャのImport Settingsで以下を設定:
///   - Texture Type: Default
///   - Alpha Source: From Gray Scale（または Input Texture Alpha）
///   - Alpha Is Transparency: ON
/// </summary>
public static class SlashTextureGenerator
{
    // 生成先フォルダ
    private const string OUTPUT_FOLDER = "Assets/Textures/Ink";

    // ノイズのシード（変えると形が変わる）
    private static int noiseSeed = 42;

#if UNITY_EDITOR
    [MenuItem("Ink/Generate Slash Textures")]
    public static void GenerateAll()
    {
        // フォルダ作成
        if (!AssetDatabase.IsValidFolder("Assets/Textures"))
            AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
            AssetDatabase.CreateFolder("Assets/Textures", "Ink");

        noiseSeed = 42;

        GenerateHorizontal();
        GenerateVertical();
        GenerateDiagonal();
        GenerateCircle();
        GenerateTrail();

        AssetDatabase.Refresh();
        Debug.Log($"[SlashTextureGenerator] 5枚のテクスチャを {OUTPUT_FOLDER} に生成しました");
    }
#endif

    // ====================================================================
    //  横一文字（三日月弧）256x128
    // ====================================================================
    private static void GenerateHorizontal()
    {
        int w = 256, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float cx = w * 0.5f, cy = h * 0.85f; // 弧の中心（下寄り）
        float outerR = w * 0.55f;
        float innerR = w * 0.35f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 弧の範囲内か
                float arcAlpha = 0f;
                if (dist > innerR && dist < outerR && dy < 0) // 上半分の弧
                {
                    // 弧の中心線からの距離で濃さを決める
                    float midR = (outerR + innerR) * 0.5f;
                    float halfThick = (outerR - innerR) * 0.5f;
                    float fromMid = Mathf.Abs(dist - midR) / halfThick;
                    arcAlpha = Smoothstep(1f, 0f, fromMid);

                    // 端を薄くする（左右フェード）
                    float angle = Mathf.Atan2(-dy, dx);
                    float edgeFade = Mathf.Sin(angle); // 0°と180°で0、90°で1
                    arcAlpha *= Mathf.Pow(edgeFade, 0.6f);
                }

                // ノイズでかすれを追加
                float noise = BrushNoise(x, y, 5f, noiseSeed);
                arcAlpha *= Mathf.Lerp(0.4f, 1f, noise);

                // 細かいノイズで繊維感
                float fineNoise = BrushNoise(x, y, 15f, noiseSeed + 100);
                arcAlpha *= Mathf.Lerp(0.7f, 1f, fineNoise);

                arcAlpha = Mathf.Clamp01(arcAlpha);

                // 墨の色（わずかに青み）
                Color col = new Color(0.02f, 0.02f, 0.05f, arcAlpha);
                tex.SetPixel(x, y, col);
            }
        }

        SaveTexture(tex, "SlashHorizontal");
    }

    // ====================================================================
    //  縦斬り（縦の筆跡）64x256
    // ====================================================================
    private static void GenerateVertical()
    {
        int w = 64, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float cx = w * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 中心線からの距離
                float dx = Mathf.Abs(x - cx) / (w * 0.5f);

                // 筆跡の太さ（上下で変化: 上が太く下が細い = 筆の入り・抜き）
                float yNorm = (float)y / h;
                float brushWidth = Mathf.Lerp(0.7f, 0.3f, yNorm); // 上太→下細
                brushWidth += BrushNoise(x, y, 3f, noiseSeed + 200) * 0.15f; // 揺らぎ

                float alpha = 0f;
                if (dx < brushWidth)
                {
                    float fromEdge = 1f - (dx / brushWidth);
                    alpha = Smoothstep(0f, 1f, fromEdge * 2f);

                    // 上端と下端をフェード
                    float tipFade = Mathf.Min(yNorm * 5f, (1f - yNorm) * 4f);
                    tipFade = Mathf.Clamp01(tipFade);
                    alpha *= tipFade;
                }

                // かすれノイズ
                float noise = BrushNoise(x, y, 8f, noiseSeed + 300);
                alpha *= Mathf.Lerp(0.3f, 1f, noise);

                // 縦方向のかすれ（筆の毛の跡）
                float streak = BrushNoise(x * 3, y, 2f, noiseSeed + 400);
                alpha *= Mathf.Lerp(0.6f, 1f, streak);

                alpha = Mathf.Clamp01(alpha);
                Color col = new Color(0.02f, 0.02f, 0.05f, alpha);
                tex.SetPixel(x, y, col);
            }
        }

        SaveTexture(tex, "SlashVertical");
    }

    // ====================================================================
    //  斜め斬り（斜めの筆跡）256x256
    // ====================================================================
    private static void GenerateDiagonal()
    {
        int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        // 斜め45°の線
        float angle = -45f * Mathf.Deg2Rad;
        float cosA = Mathf.Cos(angle);
        float sinA = Mathf.Sin(angle);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 中心からの相対座標
                float rx = (x - w * 0.5f) / w;
                float ry = (y - h * 0.5f) / h;

                // 回転して斜め方向を軸にする
                float along = rx * cosA - ry * sinA;  // 斜め方向（長さ方向）
                float across = rx * sinA + ry * cosA;  // 垂直方向（太さ方向）

                // 筆跡の形状
                float brushWidth = 0.08f + BrushNoise(x, y, 4f, noiseSeed + 500) * 0.03f;
                float lengthLimit = 0.45f;

                float alpha = 0f;
                if (Mathf.Abs(across) < brushWidth && Mathf.Abs(along) < lengthLimit)
                {
                    // 太さ方向のフェード
                    float fromEdge = 1f - Mathf.Abs(across) / brushWidth;
                    alpha = Smoothstep(0f, 1f, fromEdge * 2.5f);

                    // 端のフェード（入り・抜き）
                    float tipFade = Mathf.Min(
                        (along + lengthLimit) / (lengthLimit * 0.3f),
                        (lengthLimit - along) / (lengthLimit * 0.2f));
                    alpha *= Mathf.Clamp01(tipFade);
                }

                // かすれ
                float noise = BrushNoise(x, y, 6f, noiseSeed + 600);
                alpha *= Mathf.Lerp(0.35f, 1f, noise);

                alpha = Mathf.Clamp01(alpha);
                Color col = new Color(0.02f, 0.02f, 0.05f, alpha);
                tex.SetPixel(x, y, col);
            }
        }

        SaveTexture(tex, "SlashDiagonal");
    }

    // ====================================================================
    //  円弧（リング状）256x256
    // ====================================================================
    private static void GenerateCircle()
    {
        int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float cx = w * 0.5f, cy = h * 0.5f;
        float outerR = w * 0.45f;
        float innerR = w * 0.3f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = 0f;
                if (dist > innerR && dist < outerR)
                {
                    float midR = (outerR + innerR) * 0.5f;
                    float halfThick = (outerR - innerR) * 0.5f;
                    float fromMid = Mathf.Abs(dist - midR) / halfThick;
                    alpha = Smoothstep(1f, 0f, fromMid);
                }

                // 角度に応じたかすれ（筆の一周で濃淡が出る）
                float angle = Mathf.Atan2(dy, dx);
                float angleFade = Mathf.Abs(Mathf.Sin(angle * 2f));
                alpha *= Mathf.Lerp(0.6f, 1f, angleFade);

                // ノイズ
                float noise = BrushNoise(x, y, 6f, noiseSeed + 700);
                alpha *= Mathf.Lerp(0.4f, 1f, noise);

                float fineNoise = BrushNoise(x, y, 18f, noiseSeed + 800);
                alpha *= Mathf.Lerp(0.75f, 1f, fineNoise);

                alpha = Mathf.Clamp01(alpha);
                Color col = new Color(0.02f, 0.02f, 0.05f, alpha);
                tex.SetPixel(x, y, col);
            }
        }

        SaveTexture(tex, "SlashCircle");
    }

    // ====================================================================
    //  Trail Renderer用（横グラデーション）256x64
    // ====================================================================
    private static void GenerateTrail()
    {
        int w = 256, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float cy = h * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float xNorm = (float)x / w;  // 0=先端（新しい）、1=末尾（古い）
                float dy = Mathf.Abs(y - cy) / (h * 0.5f);

                // 先端が濃く、末尾にかけてかすれていく
                float lengthFade = 1f - Mathf.Pow(xNorm, 1.5f);

                // 幅方向のフェード
                float widthFade = Smoothstep(1f, 0f, dy);

                float alpha = lengthFade * widthFade;

                // かすれノイズ（末尾ほどかすれが強い）
                float noiseStrength = Mathf.Lerp(0.1f, 0.7f, xNorm);
                float noise = BrushNoise(x, y, 8f, noiseSeed + 900);
                alpha *= Mathf.Lerp(1f - noiseStrength, 1f, noise);

                alpha = Mathf.Clamp01(alpha);
                Color col = new Color(0.02f, 0.02f, 0.05f, alpha);
                tex.SetPixel(x, y, col);
            }
        }

        SaveTexture(tex, "SlashTrail");
    }

    // ====================================================================
    //  ユーティリティ
    // ====================================================================

    /// <summary>簡易ブラシノイズ（Value Noise風）</summary>
    private static float BrushNoise(float x, float y, float scale, int seed)
    {
        // シンプルなハッシュベースのノイズ
        float nx = x / scale;
        float ny = y / scale;

        int ix = Mathf.FloorToInt(nx);
        int iy = Mathf.FloorToInt(ny);
        float fx = nx - ix;
        float fy = ny - iy;

        // Smoothstep補間
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        float v00 = Hash(ix, iy, seed);
        float v10 = Hash(ix + 1, iy, seed);
        float v01 = Hash(ix, iy + 1, seed);
        float v11 = Hash(ix + 1, iy + 1, seed);

        float a = Mathf.Lerp(v00, v10, fx);
        float b = Mathf.Lerp(v01, v11, fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>整数座標→0〜1のハッシュ</summary>
    private static float Hash(int x, int y, int seed)
    {
        int h = x * 374761393 + y * 668265263 + seed;
        h = (h ^ (h >> 13)) * 1274126177;
        h = h ^ (h >> 16);
        return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    /// <summary>Smoothstep補間</summary>
    private static float Smoothstep(float a, float b, float t)
    {
        t = Mathf.Clamp01(t);
        t = t * t * (3f - 2f * t);
        return Mathf.Lerp(a, b, t);
    }

    /// <summary>テクスチャをPNGで保存</summary>
    private static void SaveTexture(Texture2D tex, string name)
    {
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        string path = $"{OUTPUT_FOLDER}/{name}.png";

        // Editorでのみファイルに書き出す
#if UNITY_EDITOR
        System.IO.File.WriteAllBytes(path, png);
        Debug.Log($"  生成: {path} ({tex.width}x{tex.height})");
#endif

        Object.DestroyImmediate(tex);
    }
}
