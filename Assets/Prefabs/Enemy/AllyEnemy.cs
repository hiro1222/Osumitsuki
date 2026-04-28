using UnityEngine;

/// <summary>
/// 友好Enemy
/// どの敵対Enemyがお墨付きされても同じ動きをする
/// AllyEnemyManagerから生成・管理される
///
/// 【外部から呼ぶ関数】
/// ・SetFollowTarget() : Managerから毎フレーム呼ぶ
/// ・Consume()         : Managerから消費時に呼ぶ
/// ・GetIsConsumed()   : Managerから消費済み確認に呼ぶ
/// </summary>
public class AllyEnemy : MonoBehaviour
{
    // ====================================================================
    //  状態
    // ====================================================================

    private enum AllyState
    {
        Bounce,   // 跳ねアニメーション（生成直後）
        Follow,   // 追従
        Consumed, // 消費済み
    }

    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("移動")]
    [SerializeField] private float followSpeed = 6f;
    [SerializeField] private float anchorSpeed = 12f;  // アンカー移動時の速度

    private bool useAnchorSpeed = false;  // 追加


    [Header("跳ねアニメーション")]
    [SerializeField] private float bounceHeight = 1.5f;
    [SerializeField] private float bounceDuration = 0.5f;

    [Header("見た目")]
    [SerializeField] private Color allyColor = Color.cyan;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    private AllyState state = AllyState.Bounce;
    private Vector3 followTargetPos;
    private bool followTargetSet;
    private float bounceTimer;
    private Vector3 bounceBasePos;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {
        // 色を設定
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(renderer.sharedMaterial);
            renderer.material.color = allyColor;
        }

        // 生成時はまず跳ねる
        StartBounce();
    }

    // ====================================================================
    //  毎フレーム
    // ====================================================================

    private void Update()
    {
        switch (state)
        {
            case AllyState.Bounce:   UpdateBounce(); break;
            case AllyState.Follow:   UpdateFollow(); break;
            case AllyState.Consumed: break;
        }
    }

    // ====================================================================
    //  追従
    // ====================================================================

    /// <summary>
    /// 追従位置を設定する
    /// AllyEnemyManagerから毎フレーム呼ぶ
    /// </summary>
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

    // ====================================================================
    //  消費処理
    // ====================================================================

    /// <summary>
    /// 消費処理
    /// 身代わり・ギミック援助時にManagerから呼ぶ
    /// 消費されたら復活しない
    /// </summary>
    public void Consume()
    {
        if (state == AllyState.Consumed) return;
        state = AllyState.Consumed;

        // 消費エフェクトなどはここに追加（アーティスト担当と要確認）
        Destroy(gameObject);
    }

    /// <summary>消費済みかどうかを返す</summary>
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

    public void SetUseAnchorSpeed(bool use)
    {
        useAnchorSpeed = use;
    }
}
