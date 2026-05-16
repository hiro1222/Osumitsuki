using UnityEngine;

/// <summary>
/// 友好Enemy
/// どの敵対Enemyがお墨付きされても同じ動きをする
/// AllyEnemyManagerから生成・管理される
///
/// 【外部から制御できるステート】
/// SetExternalState(IAllyEnemyState) で外部からステートを差し込める
/// ClearExternalState() で通常のFollowに戻る
///
/// 【外部から呼ぶ関数】
/// ・SetFollowTarget()    : Managerから毎フレーム呼ぶ
/// ・Consume()            : Managerから消費時に呼ぶ
/// ・GetIsConsumed()      : Managerから消費済み確認に呼ぶ
/// ・SetExternalState()   : 外部ステートをセット
/// ・ClearExternalState() : 外部ステートを解除してFollowに戻る
/// </summary>
public class AllyEnemy : MonoBehaviour
{
    // ====================================================================
    //  外部ステートのインターフェース
    //  他のスクリプトでこれを実装して SetExternalState() に渡す
    // ====================================================================

    public interface IAllyEnemyState
    {
        /// <summary>ステート開始時に1回呼ばれる</summary>
        void OnEnter(AllyEnemy owner);

        /// <summary>毎フレーム呼ばれる。falseを返すとステート終了→Followに戻る</summary>
        bool OnTick(AllyEnemy owner, float dt);

        /// <summary>ステート終了時に1回呼ばれる</summary>
        void OnExit(AllyEnemy owner);
    }

    // ====================================================================
    //  内部ステート
    // ====================================================================

    private enum AllyState
    {
        Bounce,   // 跳ねアニメーション（生成直後）
        Follow,   // 追従
        External, // 外部ステートに委ねる
        Consumed, // 消費済み
    }

    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("移動")]
    [SerializeField] private float followSpeed = 6f;
    [SerializeField] private float anchorSpeed = 12f;

    [Header("跳ねアニメーション")]
    [SerializeField] private float bounceHeight = 1.5f;
    [SerializeField] private float bounceDuration = 0.5f;

    [Header("見た目")]
    [SerializeField] private Color allyColor = Color.cyan;

    // ====================================================================
    //  内部状態
    // ====================================================================

    private AllyState state = AllyState.Bounce;
    private Vector3 followTargetPos;
    private bool followTargetSet;
    private float bounceTimer;
    private Vector3 bounceBasePos;
    private bool useAnchorSpeed = false;

    // 外部ステート
    private IAllyEnemyState externalState;

    // ====================================================================
    //  公開プロパティ（外部ステートから位置などを参照するため）
    // ====================================================================

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(rend.sharedMaterial);
            rend.material.color = allyColor;
        }

        StartBounce();
    }

    // ====================================================================
    //  毎フレーム
    // ====================================================================

    private void Update()
    {
        switch (state)
        {
            case AllyState.Bounce: UpdateBounce(); break;
            case AllyState.Follow: UpdateFollow(); break;
            case AllyState.External: UpdateExternal(); break;
            case AllyState.Consumed: break;
        }
    }

    // ====================================================================
    //  追従（通常）
    // ====================================================================

    public void SetFollowTarget(Vector3 pos)
    {
        followTargetPos = pos;
        followTargetSet = true;
    }

    private void UpdateFollow()
    {
        Vector3 target = followTargetSet ? followTargetPos : transform.position;
        Vector3 toTarget = target - transform.position;

        if (toTarget.magnitude > 0.3f)
        {
            float speed = useAnchorSpeed ? anchorSpeed : followSpeed;
            transform.position += toTarget.normalized * speed * Time.deltaTime;
        }

        if (toTarget.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTarget),
                8f * Time.deltaTime);
        }

        followTargetSet = false;
    }

    public void SetUseAnchorSpeed(bool use) => useAnchorSpeed = use;

    // ====================================================================
    //  外部ステート
    // ====================================================================

    /// <summary>
    /// 外部ステートをセットする。
    /// 即座に External ステートに切り替わり、IAllyEnemyState.OnEnter が呼ばれる。
    /// </summary>
    public void SetExternalState(IAllyEnemyState newState)
    {
        if (state == AllyState.Consumed) return;

        // 既存の外部ステートを終了
        externalState?.OnExit(this);

        externalState = newState;
        state = AllyState.External;

        externalState?.OnEnter(this);
    }

    /// <summary>
    /// 外部ステートを解除して Follow に戻る。
    /// </summary>
    public void ClearExternalState()
    {
        if (externalState != null)
        {
            externalState.OnExit(this);
            externalState = null;
        }

        if (state == AllyState.External)
            state = AllyState.Follow;
    }

    private void UpdateExternal()
    {
        if (externalState == null)
        {
            state = AllyState.Follow;
            return;
        }

        // OnTick が false を返したらステート終了
        bool continueState = externalState.OnTick(this, Time.deltaTime);
        if (!continueState)
        {
            ClearExternalState();
        }
    }

    // ====================================================================
    //  位置・回転を外部ステートから直接操作するためのヘルパー
    // ====================================================================

    public void MoveTo(Vector3 pos) => transform.position = pos;
    public void RotateTo(Quaternion rot) => transform.rotation = rot;
    public void MoveToward(Vector3 target, float speed)
    {
        Vector3 dir = target - transform.position;
        if (dir.magnitude > 0.05f)
            transform.position += dir.normalized * speed * Time.deltaTime;
    }

    // ====================================================================
    //  消費処理
    // ====================================================================

    public void Consume()
    {
        if (state == AllyState.Consumed) return;

        externalState?.OnExit(this);
        externalState = null;

        state = AllyState.Consumed;
        Destroy(gameObject);
    }

    public bool GetIsConsumed() => state == AllyState.Consumed;

    // ====================================================================
    //  跳ねアニメーション
    // ====================================================================

    private void StartBounce()
    {
        state = AllyState.Bounce;
        bounceTimer = 0f;
        bounceBasePos = transform.position;
    }

    private void UpdateBounce()
    {
        bounceTimer += Time.deltaTime;
        float t = bounceTimer / bounceDuration;

        float yOffset = Mathf.Sin(t * Mathf.PI) * bounceHeight;
        transform.position = bounceBasePos + Vector3.up * yOffset;

        if (bounceTimer >= bounceDuration)
        {
            transform.position = bounceBasePos;
            state = AllyState.Follow;
        }
    }
}
