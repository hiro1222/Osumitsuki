using UnityEngine;
using System.Collections;

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
/// ・SetAllyIndex()       : Managerから生成時に呼ぶ（番号付け）
/// ・Consume()            : Managerから消費時に呼ぶ
/// ・ConsumeSelf()        : 自分自身から消費するときに呼ぶ（Manager経由なし）
/// ・GetIsConsumed()      : Managerから消費済み確認に呼ぶ
/// ・GetAllyIndex()       : 番号を取得する
/// ・SetExternalState()   : 外部ステートをセット
/// ・ClearExternalState() : 外部ステートを解除してFollowに戻る
/// </summary>
public class AllyEnemy : MonoBehaviour
{
    // ====================================================================
    //  外部ステートのインターフェース
    // ====================================================================

    public interface IAllyEnemyState
    {
        void OnEnter(AllyEnemy owner);
        bool OnTick(AllyEnemy owner, float dt);
        void OnExit(AllyEnemy owner);
    }

    // ====================================================================
    //  内部ステート
    // ====================================================================

    private enum AllyState
    {
        Bounce,
        Follow,
        External,
        Consumed,
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

    [Header("── 仲間化エフェクト ──")]
    [SerializeField] private GameObject allyEffect;
    [Tooltip("エフェクトの位置オフセット")]
    [SerializeField] private Vector3 allyEffectOffset = Vector3.zero;

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

    // 味方番号（何番目の味方か）
    private int allyIndex = -1;

    // Managerの参照（ConsumeSelf用）
    private AllyEnemyManager manager;

    // ====================================================================
    //  公開プロパティ
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

        PlayAllyEffect();
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
    //  番号付け
    // ====================================================================

    /// <summary>
    /// 味方番号を設定する
    /// AllyEnemyManagerから生成時に呼ぶ
    /// </summary>
    public void SetAllyIndex(int index, AllyEnemyManager allyManager)
    {
        allyIndex = index;
        manager = allyManager;
        Debug.Log($"[AllyEnemy] 味方番号: {allyIndex}");
    }

    /// <summary>味方番号を返す</summary>
    public int GetAllyIndex() => allyIndex;

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

    public void SetExternalState(IAllyEnemyState newState)
    {
        if (state == AllyState.Consumed) return;

        externalState?.OnExit(this);
        externalState = newState;
        state = AllyState.External;
        externalState?.OnEnter(this);
    }

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

        bool continueState = externalState.OnTick(this, Time.deltaTime);
        if (!continueState)
        {
            ClearExternalState();
        }
    }

    // ====================================================================
    //  位置・回転ヘルパー
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

    /// <summary>
    /// 消費処理
    /// Managerから呼ぶ
    /// </summary>
    public void Consume()
    {
        if (state == AllyState.Consumed) return;

        externalState?.OnExit(this);
        externalState = null;

        state = AllyState.Consumed;
        Debug.Log($"[AllyEnemy] 消費（番号: {allyIndex}）");
        Destroy(gameObject);
    }

    /// <summary>
    /// 自分自身から消費する
    /// Manager経由せずに消えたいときに呼ぶ
    /// Managerにも通知する
    /// </summary>
    public void ConsumeSelf()
    {
        if (state == AllyState.Consumed) return;

        // Managerに通知して後処理をしてもらう
        if (manager != null)
        {
            manager.OnAllyConsumedSelf(this);
        }

        Consume();
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

    /// <summary>仲間になった瞬間のエフェクトを再生する</summary>
    private void PlayAllyEffect()
    {
        if (allyEffect == null) return;

        allyEffect.transform.localPosition = allyEffectOffset;
        allyEffect.SetActive(true);
        StartCoroutine(PlayAllyEffectCoroutine());
    }

    private System.Collections.IEnumerator PlayAllyEffectCoroutine()
    {
        yield return null;

        var ps = allyEffect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in ps)
        {
            p.Clear();
            p.Play(true);
        }

        StartCoroutine(PlayAndDestroyEffect(allyEffect));
    }

    /// <summary>1回限りのエフェクトを再生して、終わったら完全に削除する</summary>
    private IEnumerator PlayAndDestroyEffect(GameObject effect)
    {
        if (effect == null) yield break;

        effect.SetActive(true);
        yield return null;

        var ps = effect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in ps)
        {
            p.Clear();
            p.Play(true);
        }

        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;
            foreach (var p in ps)
            {
                if (p != null && p.IsAlive())
                {
                    anyAlive = true;
                    break;
                }
            }
            yield return null;
        }

        if (effect != null)
            Destroy(effect); // ★ 完全に削除
    }
}