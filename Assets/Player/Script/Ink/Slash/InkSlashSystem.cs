using UnityEngine;

/// <summary>
/// FlyingSlashの生成・管理（設計書セクション11: InkSlashSystem）
/// - 複数のSlashPatternを保持
/// - CreateSlash() で選択中のパターンの斬撃を発射
///
/// ■ セットアップ:
/// 1. このスクリプトをアタッチ
/// 2. InkManager をドラッグ
/// 3. Patterns 配列にSlashPatternアセットをドラッグ（なければデフォルトで生成される）
/// </summary>
public class InkSlashSystem : MonoBehaviour
{
    //[Header("参照")]
    //[SerializeField] private InkManager inkManager;

    [Header("斬撃プレハブ（空ならQuadを自動生成）")]
    [SerializeField] private GameObject slashPrefab;

    [Header("Trail用テクスチャ（SlashTrail.png）")]
    [SerializeField] private Texture2D trailTexture;

    [Header("斬撃テクスチャ（Generate後にドラッグ: 横一文字, 縦斬り, 斜め, 円弧 の順）")]
    [SerializeField] private Texture2D[] slashTextures;

    [Header("斬撃パターン（空ならデフォルト4種を自動生成）")]
    [SerializeField] private SlashPattern[] patterns;

    // 現在選択中のパターン
    private int currentPatternIndex;
    private LayerMask hitMask;

    /// <summary>現在のパターン</summary>
    public SlashPattern CurrentPattern =>
        patterns != null && patterns.Length > 0 ? patterns[currentPatternIndex] : null;

    /// <summary>現在のパターンインデックス</summary>
    public int CurrentPatternIndex => currentPatternIndex;

    /// <summary>パターン数</summary>
    public int PatternCount => patterns != null ? patterns.Length : 0;

    /// <summary>指定インデックスのパターンを取得</summary>
    public SlashPattern GetPattern(int index)
    {
        if (patterns == null || index < 0 || index >= patterns.Length) return null;
        return patterns[index];
    }

    private void Start()
    {
        // Playerレイヤーを除外
        int playerLayer = LayerMask.NameToLayer("Player");
        hitMask = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;

        // パターンが未設定ならデフォルトを生成
        if (patterns == null || patterns.Length == 0)
        {
            patterns = CreateDefaultPatterns();
            Debug.Log("[InkSlashSystem] デフォルトパターン4種を生成しました。" +
                      "Assets > Create > Ink/Slash Pattern でカスタムパターンを作れます。");
        }

        currentPatternIndex = 0;
    }

    /// <summary>パターンを切り替える</summary>
    public void SelectPattern(int index)
    {
        if (patterns == null || patterns.Length == 0) return;
        currentPatternIndex = Mathf.Clamp(index, 0, patterns.Length - 1);
    }

    /// <summary>次のパターンに切り替え</summary>
    public void NextPattern()
    {
        if (patterns == null || patterns.Length == 0) return;
        currentPatternIndex = (currentPatternIndex + 1) % patterns.Length;
    }

    /// <summary>前のパターンに切り替え</summary>
    public void PrevPattern()
    {
        if (patterns == null || patterns.Length == 0) return;
        currentPatternIndex = (currentPatternIndex - 1 + patterns.Length) % patterns.Length;
    }

    /// <summary>斬撃を発射する</summary>
    public void CreateSlash(Vector3 position, Vector3 direction)
    {
        SlashPattern pat = CurrentPattern;
        if (pat == null) return;

        // オブジェクト生成
        GameObject obj;
        if (slashPrefab != null)
        {
            obj = Instantiate(slashPrefab, position, Quaternion.LookRotation(direction));
        }
        else
        {
            obj = CreateDefaultSlashObject(position, direction, pat);
        }

        // FlyingSlashコンポーネント設定
        FlyingSlash slash = obj.GetComponent<FlyingSlash>();
        if (slash == null)
            slash = obj.AddComponent<FlyingSlash>();

        slash.velocity = direction.normalized * pat.speed;
        slash.pattern = pat;
        slash.hitMask = hitMask;
    }

    /// <summary>プレハブ未設定時: Quad＋TrailRendererで斬撃を作る</summary>
    private GameObject CreateDefaultSlashObject(Vector3 position, Vector3 direction, SlashPattern pat)
    {
        GameObject obj = new GameObject($"Slash_{pat.patternName}");
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.LookRotation(direction);

        // ── Quad（斬撃の見た目本体） ──
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Visual";
        quad.transform.SetParent(obj.transform, false);

        // Colliderを外す
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // サイズをパターンに合わせる
        Vector2 size = pat.visualSize;
        if (size.sqrMagnitude < 0.01f) size = new Vector2(3f, 1.5f);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);

        // Quadを進行方向に対して垂直に立てる（XY平面がカメラに向く）
        // Quadのデフォルトは-Z方向を向いているので、90°回転して進行方向と直交させる
        quad.transform.localRotation = Quaternion.Euler(0, 0, 0);

        // マテリアル設定
        var renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader slashShader = Shader.Find("Ink/SlashVisual");
            if (slashShader == null) slashShader = Shader.Find("Universal Render Pipeline/Unlit");

            var mat = new Material(slashShader);
            mat.color = new Color(0.02f, 0.02f, 0.05f, 1f);

            if (pat.slashTexture != null)
            {
                mat.SetTexture("_MainTex", pat.slashTexture);
            }

            // 透明描画の設定
            mat.SetFloat("_Surface", 1); // Transparent
            mat.renderQueue = 3000;

            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ── Trail Renderer（飛行の軌跡） ──
        var trail = obj.AddComponent<TrailRenderer>();
        trail.time = pat.trailTime > 0 ? pat.trailTime : 0.3f;
        float tw = pat.trailWidth > 0 ? pat.trailWidth : 0.5f;
        trail.startWidth = tw;
        trail.endWidth = tw * 0.1f;
        trail.minVertexDistance = 0.1f;
        trail.autodestruct = false;
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;

        // Trail用マテリアル
        Shader trailShader = Shader.Find("Ink/SlashVisual");
        if (trailShader == null) trailShader = Shader.Find("Universal Render Pipeline/Unlit");
        var trailMat = new Material(trailShader);
        trailMat.color = new Color(0.02f, 0.02f, 0.05f, 0.8f);
        trailMat.renderQueue = 3000;

        // Trail用テクスチャがあればセット（SlashTrail.png）
        if (trailTexture != null)
        {
            trailMat.SetTexture("_MainTex", trailTexture);
        }

        trail.material = trailMat;

        // Trail の色カーブ（先端濃い→末尾薄い）
        var colorGrad = new Gradient();
        colorGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.02f, 0.02f, 0.05f), 0f),
                new GradientColorKey(new Color(0.1f, 0.1f, 0.13f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = colorGrad;

        return obj;
    }

    /// <summary>
    /// デフォルトの4パターンを生成（設計書セクション5.2の値）
    /// </summary>
    private SlashPattern[] CreateDefaultPatterns()
    {
        var horizontal = ScriptableObject.CreateInstance<SlashPattern>();
        horizontal.patternName = "横一文字";
        horizontal.direction = SlashDirection.Horizontal;
        horizontal.length = 8f;
        horizontal.width = 12f;
        horizontal.arcAngle = 120f;
        horizontal.speed = 25f;
        horizontal.gravity = 2f;
        horizontal.lifetime = 1.5f;
        horizontal.baseDamage = 30f;
        horizontal.inkDensity = 220;
        horizontal.trailInterval = 0.1f;
        horizontal.trailRadius = 0.8f;
        horizontal.trailDensity = 150;
        horizontal.impactRadius = 2.0f;
        horizontal.inkCost = 0.1f;
        horizontal.visualSize = new Vector2(4f, 1.5f);
        horizontal.visualRotation = 90f;    // 横向きそのまま
        horizontal.trailWidth = 0.6f;
        horizontal.trailTime = 0.25f;
        horizontal.fadeSpeed = 1.2f;

        var vertical = ScriptableObject.CreateInstance<SlashPattern>();
        vertical.patternName = "縦斬り";
        vertical.direction = SlashDirection.Vertical;
        vertical.length = 15f;
        vertical.width = 2f;
        vertical.arcAngle = 0f;
        vertical.speed = 35f;
        vertical.gravity = 1f;
        vertical.lifetime = 2f;
        vertical.baseDamage = 80f;
        vertical.inkDensity = 230;
        vertical.trailInterval = 0.3f;
        vertical.trailRadius = 0.2f;
        vertical.trailDensity = 140;
        vertical.impactRadius = 1.0f;
        vertical.inkCost = 0.2f;
        vertical.visualSize = new Vector2(1f, 3.5f);
        vertical.visualRotation = 0f;      // サイズで既に縦長
        vertical.trailWidth = 0.3f;
        vertical.trailTime = 0.35f;
        vertical.fadeSpeed = 0.8f;

        var diagonal = ScriptableObject.CreateInstance<SlashPattern>();
        diagonal.patternName = "斜め";
        diagonal.direction = SlashDirection.DiagonalR;
        diagonal.length = 10f;
        diagonal.width = 6f;
        diagonal.arcAngle = 60f;
        diagonal.speed = 28f;
        diagonal.gravity = 2f;
        diagonal.lifetime = 1.8f;
        diagonal.baseDamage = 50f;
        diagonal.inkDensity = 170;
        diagonal.trailInterval = 0.4f;
        diagonal.trailRadius = 0.35f;
        diagonal.trailDensity = 100;
        diagonal.impactRadius = 1.5f;
        diagonal.inkCost = 0.15f;
        diagonal.visualSize = new Vector2(3f, 3f);
        diagonal.visualRotation = 45f;     // 斜め45°
        diagonal.trailWidth = 0.4f;
        diagonal.trailTime = 0.3f;
        diagonal.fadeSpeed = 1.0f;

        var circle = ScriptableObject.CreateInstance<SlashPattern>();
        circle.patternName = "円弧";
        circle.direction = SlashDirection.Circle;
        circle.length = 6f;
        circle.width = 6f;
        circle.arcAngle = 360f;
        circle.speed = 15f;
        circle.gravity = 3f;
        circle.lifetime = 1.2f;
        circle.baseDamage = 20f;
        circle.inkDensity = 150;
        circle.trailInterval = 0.3f;
        circle.trailRadius = 0.4f;
        circle.trailDensity = 90;
        circle.impactRadius = 2.5f;
        circle.inkCost = 0.25f;
        circle.visualSize = new Vector2(3.5f, 3.5f);
        circle.visualRotation = 0f;        // リング状なので回転不要
        circle.trailWidth = 0.5f;
        circle.trailTime = 0.2f;
        circle.fadeSpeed = 1.5f;

        // テクスチャが設定されていれば割り当て（横一文字, 縦斬り, 斜め, 円弧 の順）
        var result = new[] { horizontal, vertical, diagonal, circle };
        if (slashTextures != null)
        {
            for (int i = 0; i < result.Length && i < slashTextures.Length; i++)
            {
                if (slashTextures[i] != null)
                {
                    result[i].slashTexture = slashTextures[i];
                }
            }
        }

        return result;
    }
}