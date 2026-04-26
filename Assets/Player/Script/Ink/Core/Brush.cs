using UnityEngine;

/// <summary>
/// 筆の状態
/// 設計書セクション3.1: BrushState
/// </summary>
public enum BrushState
{
    Idle,       // 何もしていない
    Painting,   // 地面に塗っている
    Swinging,   // 振っている（空中）
    Flicking,   // 払い上げ（地面→空中の遷移 = 飛沫）
    Empty,      // 墨切れ
}

/// <summary>
/// 筆の物理状態管理クラス
/// 設計書セクション3: Brush
/// - 位置・速度・筆圧・墨量・状態の毎フレーム更新
/// - state が全てのアクションの起点
/// </summary>
public class Brush : MonoBehaviour
{
    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("墨の設定")]
    [Tooltip("墨の最大量（1.0 = 満タン）")]
    [SerializeField] private float maxInkAmount = 1f;

    [Tooltip("補充にかかる時間（秒）")]
    [SerializeField] private float refillDuration = 1.5f;

    [Header("状態遷移の閾値")]
    [Tooltip("この速度以上で振ると SWINGING になる（m/s）")]
    [SerializeField] private float swingThreshold = 5f;

    [Tooltip("FLICKING 判定: 面から離れたときの速度閾値（m/s）")]
    [SerializeField] private float flickThreshold = 8f;

    [Tooltip("SWINGING → IDLE に戻る速度閾値（m/s）")]
    [SerializeField] private float swingEndThreshold = 2f;

    [Header("筆圧の設定")]
    [Tooltip("塗り半径の範囲 [弱筆圧, 強筆圧]（メートル）")]
    [SerializeField] private Vector2 paintRadiusRange = new Vector2(0.3f, 1.2f);

    [Tooltip("塗り濃度の範囲 [弱筆圧, 強筆圧]（0-255）")]
    [SerializeField] private Vector2Int paintDensityRange = new Vector2Int(80, 220);

    [Tooltip("墨消費率の範囲 [弱筆圧, 強筆圧]（/秒）")]
    [SerializeField] private Vector2 inkConsumeRange = new Vector2(0.02f, 0.12f);

    [Header("振りの設定")]
    [Tooltip("斬撃の射程範囲 [弱振り, 強振り]（メートル）")]
    [SerializeField] private Vector2 slashLengthRange = new Vector2(4f, 15f);

    [Tooltip("斬撃の幅範囲 [弱振り, 強振り]（メートル）")]
    [SerializeField] private Vector2 slashWidthRange = new Vector2(2f, 10f);

    [Tooltip("斬撃の速度範囲 [弱振り, 強振り]（m/s）")]
    [SerializeField] private Vector2 slashSpeedRange = new Vector2(15f, 40f);

    [Tooltip("斬撃のダメージ倍率範囲 [弱振り, 強振り]")]
    [SerializeField] private Vector2 slashDamageMultRange = new Vector2(0.5f, 1.5f);

    [Tooltip("斬撃の墨濃度範囲 [弱振り, 強振り]（0-255）")]
    [SerializeField] private Vector2Int slashDensityRange = new Vector2Int(100, 230);

    [Tooltip("斬撃の墨消費 [弱振り, 強振り]")]
    [SerializeField] private Vector2 slashInkCostRange = new Vector2(0.05f, 0.2f);

    [Tooltip("振りの強さを正規化する最大速度（m/s）")]
    [SerializeField] private float swingSpeedMax = 30f;

    // ====================================================================
    //  位置・姿勢（設計書3.1）
    // ====================================================================

    /// <summary>筆先のワールド座標</summary>
    public Vector3 TipPosition { get; private set; }

    /// <summary>前フレームの筆先位置</summary>
    public Vector3 TipPositionPrev { get; private set; }

    /// <summary>筆の根元の位置（プレイヤーの手元）</summary>
    public Vector3 BasePosition { get; private set; }

    /// <summary>筆の向き（根元→先端の方向）</summary>
    public Vector3 Direction { get; private set; }

    // ====================================================================
    //  速度・力
    // ====================================================================

    /// <summary>筆先の速度ベクトル</summary>
    public Vector3 TipVelocity { get; private set; }

    /// <summary>振りの速さ（tipVelocityの大きさ）</summary>
    public float SwingSpeed { get; private set; }

    /// <summary>振りの強さ（0〜1に正規化）</summary>
    public float SwingPower { get; private set; }

    // ====================================================================
    //  墨の状態
    // ====================================================================

    /// <summary>筆に残っている墨の量（0.0〜1.0）</summary>
    public float InkAmount { get; private set; }

    // ====================================================================
    //  接地状態
    // ====================================================================

    /// <summary>筆先が面に触れているか</summary>
    public bool IsTouchingSurface { get; private set; }

    /// <summary>触れている面の法線</summary>
    public Vector3 SurfaceNormal { get; private set; }

    /// <summary>触れている面上の点</summary>
    public Vector3 SurfacePoint { get; private set; }

    // ====================================================================
    //  筆圧
    // ====================================================================

    /// <summary>筆圧（0.0=浮いてる、1.0=強く押し付け）</summary>
    public float Pressure { get; private set; }

    // ====================================================================
    //  状態
    // ====================================================================

    /// <summary>現在の状態</summary>
    public BrushState State { get; private set; }

    /// <summary>前フレームの状態</summary>
    public BrushState StatePrev { get; private set; }

    /// <summary>補充中か</summary>
    public bool IsRefilling { get; private set; }

    // ====================================================================
    //  筆圧・振りから計算されるパラメータ（読み取り専用）
    //  設計書セクション3.3, 3.4
    // ====================================================================

    /// <summary>現在の筆圧での塗り半径（メートル）</summary>
    public float CurrentPaintRadius =>
        Mathf.Lerp(paintRadiusRange.x, paintRadiusRange.y, Pressure);

    /// <summary>現在の筆圧での塗り濃度（0-255）</summary>
    public byte CurrentPaintDensity =>
        (byte)Mathf.Lerp(paintDensityRange.x, paintDensityRange.y, Pressure);

    /// <summary>現在の筆圧での墨消費率（/秒）</summary>
    public float CurrentInkConsumeRate =>
        Mathf.Lerp(inkConsumeRange.x, inkConsumeRange.y, Pressure);

    /// <summary>現在の振りの強さでの斬撃射程（メートル）</summary>
    public float CurrentSlashLength =>
        Mathf.Lerp(slashLengthRange.x, slashLengthRange.y, SwingPower);

    /// <summary>現在の振りの強さでの斬撃幅（メートル）</summary>
    public float CurrentSlashWidth =>
        Mathf.Lerp(slashWidthRange.x, slashWidthRange.y, SwingPower);

    /// <summary>現在の振りの強さでの斬撃速度（m/s）</summary>
    public float CurrentSlashSpeed =>
        Mathf.Lerp(slashSpeedRange.x, slashSpeedRange.y, SwingPower);

    /// <summary>現在の振りの強さでのダメージ倍率</summary>
    public float CurrentSlashDamageMult =>
        Mathf.Lerp(slashDamageMultRange.x, slashDamageMultRange.y, SwingPower);

    /// <summary>現在の振りの強さでの斬撃墨濃度（0-255）</summary>
    public byte CurrentSlashDensity =>
        (byte)Mathf.Lerp(slashDensityRange.x, slashDensityRange.y, SwingPower);

    /// <summary>現在の振りの強さでの斬撃墨消費</summary>
    public float CurrentSlashInkCost =>
        Mathf.Lerp(slashInkCostRange.x, slashInkCostRange.y, SwingPower);

    // ====================================================================
    //  内部
    // ====================================================================

    private float refillTimer;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Awake()
    {
        InkAmount = maxInkAmount;
        State = BrushState.Idle;
        StatePrev = BrushState.Idle;
    }

    // ====================================================================
    //  外部からの入力（SimplePlayerが毎フレーム呼ぶ）
    // ====================================================================

    /// <summary>
    /// 筆先の位置を更新する。SimplePlayerから毎フレーム呼ぶ。
    /// </summary>
    /// <param name="newTipPosition">筆先のワールド座標</param>
    /// <param name="newBasePosition">筆の根元の位置</param>
    public void UpdatePositions(Vector3 newTipPosition, Vector3 newBasePosition)
    {
        TipPositionPrev = TipPosition;
        TipPosition = newTipPosition;
        BasePosition = newBasePosition;

        Vector3 diff = TipPosition - BasePosition;
        Direction = diff.sqrMagnitude > 0.001f ? diff.normalized : transform.forward;
    }

    /// <summary>
    /// 面への接触情報を設定する。
    /// </summary>
    public void SetSurfaceContact(bool touching, Vector3 normal, Vector3 point)
    {
        IsTouchingSurface = touching;
        SurfaceNormal = normal;
        SurfacePoint = point;
    }

    /// <summary>
    /// 筆圧を設定する（0.0〜1.0）。
    /// 右クリック押下中など、外部から直接設定する。
    /// </summary>
    public void SetPressure(float value)
    {
        Pressure = Mathf.Clamp01(value);
    }

    /// <summary>
    /// 補充を開始する。
    /// </summary>
    public void StartRefill()
    {
        if (InkAmount >= maxInkAmount) return;
        IsRefilling = true;
        refillTimer = 0f;
    }

    /// <summary>
    /// 墨を指定量消費する。
    /// </summary>
    public void ConsumeInk(float amount)
    {
        InkAmount = Mathf.Max(0f, InkAmount - amount);
    }

    /// <summary>
    /// 墨の量を直接設定する（デバッグ用）。
    /// </summary>
    public void SetInkAmount(float amount)
    {
        InkAmount = Mathf.Clamp(amount, 0f, maxInkAmount);
    }

    // ====================================================================
    //  毎フレーム更新
    // ====================================================================

    private void Update()
    {
        UpdateVelocity();
        UpdateRefill();
        UpdateState();
    }

    /// <summary>速度を計算</summary>
    private void UpdateVelocity()
    {
        if (Time.deltaTime > 0f)
        {
            TipVelocity = (TipPosition - TipPositionPrev) / Time.deltaTime;
        }
        SwingSpeed = TipVelocity.magnitude;
        SwingPower = Mathf.Clamp01(SwingSpeed / swingSpeedMax);
    }

    /// <summary>補充処理</summary>
    private void UpdateRefill()
    {
        if (!IsRefilling) return;

        refillTimer += Time.deltaTime;
        InkAmount = Mathf.Lerp(0f, maxInkAmount, refillTimer / refillDuration);

        if (InkAmount >= maxInkAmount)
        {
            InkAmount = maxInkAmount;
            IsRefilling = false;
        }
    }

    /// <summary>
    /// 状態遷移（設計書セクション3.2）
    ///
    ///   inkAmount <= 0 → EMPTY
    ///   EMPTY + 補充完了 → IDLE
    ///   IDLE + 面に接触 + 低速度 → PAINTING
    ///   PAINTING + 面から離れた + 高速度 → FLICKING
    ///   PAINTING + 速度低下 → IDLE
    ///   IDLE/PAINTING + 空中 + swingSpeed >= 閾値 → SWINGING
    ///   SWINGING + 速度低下 → IDLE
    /// </summary>
    private void UpdateState()
    {
        StatePrev = State;

        // ── 墨切れチェック ──
        if (InkAmount <= 0f && State != BrushState.Empty)
        {
            State = BrushState.Empty;
            return;
        }

        // ── EMPTY状態: 補充完了待ち ──
        if (State == BrushState.Empty)
        {
            if (InkAmount > 0f)
            {
                State = BrushState.Idle;
            }
            return;
        }

        // ── FLICKING: 一瞬で IDLE に戻る（1フレームのトリガー的状態） ──
        if (State == BrushState.Flicking)
        {
            State = BrushState.Idle;
            // ※ FLICKING検出はStatePrevで行う
        }

        // ── PAINTING中の遷移 ──
        if (State == BrushState.Painting)
        {
            if (!IsTouchingSurface && SwingSpeed >= flickThreshold)
            {
                // 面から離れた + 高速度 → FLICKING
                State = BrushState.Flicking;
                return;
            }

            if (!IsTouchingSurface)
            {
                // 面から離れた（低速度） → IDLE
                State = BrushState.Idle;
                return;
            }

            // 面に触れたまま → PAINTING継続
            return;
        }

        // ── SWINGING中の遷移 ──
        if (State == BrushState.Swinging)
        {
            if (SwingSpeed < swingEndThreshold)
            {
                State = BrushState.Idle;
            }
            return;
        }

        // ── IDLE からの遷移 ──
        // 空中 + 高速振り → SWINGING
        if (!IsTouchingSurface && SwingSpeed >= swingThreshold)
        {
            State = BrushState.Swinging;
            return;
        }

        // 面に接触 + 低速度 → PAINTING
        if (IsTouchingSurface && SwingSpeed < swingThreshold)
        {
            State = BrushState.Painting;
            return;
        }

        // それ以外 → IDLE維持
    }
}
