using UnityEngine;

/// <summary>
/// 斬撃の方向タイプ（設計書セクション5.1）
/// </summary>
public enum SlashDirection
{
    Horizontal,     // 横一文字 ─  広い扇形
    Vertical,       // 縦斬り   │  細い矩形
    DiagonalR,      // 右斜め   ╲  狭い扇形
    DiagonalL,      // 左斜め   ╱  狭い扇形
    Circle,         // 円弧     ○  全方位
}

/// <summary>
/// 墨痕の塗り形状
/// </summary>
public enum InkShape
{
    Rectangle,      // 矩形（縦斬り用）
    Arc,            // 扇形（横一文字・斜め用）
    Circle,         // 全周（円弧用）
}

/// <summary>
/// 斬撃パターンの定義データ（設計書セクション5.1〜5.2）
/// ScriptableObjectなのでInspectorでパラメータ調整可能
/// 
/// ■ 作成手順:
/// Assets > Create > Ink/Slash Pattern でアセットを作成
/// </summary>
[CreateAssetMenu(fileName = "NewSlashPattern", menuName = "Ink/Slash Pattern")]
public class SlashPattern : ScriptableObject
{
    [Header("基本情報")]
    public string patternName = "横一文字";
    public SlashDirection direction = SlashDirection.Horizontal;

    [Header("色")]
    [Tooltip("墨の色番号。InkPalette.ID_* を使用。0 = 黒墨")]
    public byte inkColorId = 0;

    [Header("形状")]
    [Tooltip("到達距離（メートル）")]
    public float length = 8f;
    [Tooltip("横方向の広がり（メートル）")]
    public float width = 12f;
    [Tooltip("弧の角度（0=直線矩形, 360=全周）")]
    [Range(0f, 360f)]
    public float arcAngle = 120f;

    [Header("飛行")]
    public float speed = 25f;
    public float gravity = 2f;
    public float lifetime = 2f;

    [Header("ダメージ")]
    public float baseDamage = 30f;
    [Tooltip("距離による減衰率（0=減衰なし, 1=最大距離でダメージ0）")]
    [Range(0f, 1f)]
    public float damageDecay = 0.3f;

    [Header("墨痕")]
    [Tooltip("墨の濃さ（0〜255）")]
    [Range(0, 255)]
    public int inkDensity = 120;
    [Tooltip("飛行中に墨痕を落とす間隔（メートル）")]
    public float trailInterval = 0.5f;
    [Tooltip("飛行中の墨痕の半径")]
    public float trailRadius = 0.3f;
    [Tooltip("飛行中の墨痕の濃さ（0〜255。0なら墨痕を落とさない）")]
    [Range(0, 255)]
    public int trailDensity = 72;

    [Header("着弾痕")]
    [Tooltip("着弾時の墨痕の半径")]
    public float impactRadius = 1.2f;

    [Header("墨消費")]
    [Tooltip("1発あたりの墨消費量（0〜1）")]
    [Range(0f, 1f)]
    public float inkCost = 0.15f;

    [Header("ビジュアル")]
    [Tooltip("斬撃テクスチャ（SlashTextureGeneratorで生成）")]
    public Texture2D slashTexture;
    [Header("エフェクト設定")]

    [Tooltip("斬撃に付けるエフェクトPrefab")]
    public GameObject effectPrefab;
    [Tooltip("エフェクトのローカル位置オフセット")]
    public Vector3 effectOffset = Vector3.zero;
    [Tooltip("エフェクトのローカル回転（XYZ）")]
    public Vector3 effectRotation = Vector3.zero;
    [Tooltip("エフェクトのローカルスケール")]
    public Vector3 effectScale = Vector3.one;

    [Tooltip("Quadの表示サイズ（メートル）")]
    public Vector2 visualSize = new Vector2(3f, 1.5f);
    [Tooltip("Quadの回転角度（度）。0=そのまま, 90=縦, 45=斜め")]
    [Range(-180f, 180f)]
    public float visualRotation = 0f;
    [Tooltip("飛行中にフェードアウトする速度")]
    [Range(0f, 3f)]
    public float fadeSpeed = 1f;
    [Tooltip("Trail Rendererの幅")]
    public float trailWidth = 0.5f;
    [Tooltip("Trail Rendererの長さ（秒）")]
    public float trailTime = 0.3f;

    /// <summary>塗り形状を自動判定</summary>
    public InkShape GetInkShape()
    {
        if (arcAngle >= 360f) return InkShape.Circle;
        if (arcAngle <= 0f) return InkShape.Rectangle;
        return InkShape.Arc;
    }
}