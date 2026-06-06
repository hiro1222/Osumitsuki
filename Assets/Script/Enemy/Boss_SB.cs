using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ボスエネミー「鎧墨袋（Boss_SB）」
///
/// 【状態遷移】
/// フェーズ1: Idle → Chase → Charge → Tackle → Stop → Chase
/// フェーズ2: Idle → Chase → Roll（ゴロゴロ・ホーミング）→ RollEnd（ジャンプ着地）→ Chase
/// 木箱衝突時(P1): Stun
/// 灯籠3回バウンド時(P2): Stun
/// お墨付き完了時: Roar → フェーズ進行 → Chase（or 撃破）
///
/// 【フェーズ】
/// フェーズ1 → 咆哮 → フェーズ2
/// フェーズ2 → 咆哮 → フェーズ3
/// フェーズ3 → 咆哮 → 撃破
///
/// 【外部から呼ぶ関数】
/// ・StartBossBattle()      : BossAreaTriggerから呼ぶ
/// ・ReceiveInk()           : EnemyHitReceiverから呼ぶ
/// ・GetIsAlly()            : 外部からお墨付き状態を確認する
/// ・NotifyHitCrate()       : 木箱スクリプトからタックル衝突時に呼ぶ（フェーズ1）
/// ・NotifyLanternBounce()  : 灯籠スクリプトからバウンド時に呼ぶ（フェーズ2）
/// ・GetRollDirection()     : 灯籠スクリプトから転がり方向を取得するために呼ぶ
/// ・SetRollDirection()     : 灯籠スクリプトからバウンド後の方向を設定するために呼ぶ
/// </summary>
public class Boss_SB : MonoBehaviour, IF_Enemy
{
    // ====================================================================
    //  状態定義
    //  ★新しい状態を追加するときはここに追加する
    // ====================================================================

    private enum BossState
    {
        Idle,    // 待機（ボス戦開始前）
        Chase,   // 追従
        Charge,  // チャージ（タックル前の溜め）フェーズ1
        Tackle,  // タックル（急直進）フェーズ1
        Roll,    // ゴロゴロ（ホーミング転がり）フェーズ2
        RollEnd, // ゴロゴロ終了（ジャンプ→着地）フェーズ2
        Stop,    // 停止（攻撃後3秒）
        Stun,    // スタン（10秒）
        Roar,    // 咆哮
        Defeated // 撃破
    }

    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("── デバッグ ──")]
    [Tooltip("開始時のフェーズ（0=フェーズ1, 1=フェーズ2, 2=フェーズ3）デバッグ用")]
    [SerializeField] private int debugStartPhase = 0;

    [Header("── プレイヤー参照 ──")]
    [SerializeField] private Transform player;

    [Header("── モデル参照 ──")]
    [Tooltip("距離判定の基準にするモデルのTransform（子オブジェクト）")]
    [SerializeField] private Transform bodyTransform;
    [Tooltip("ゴロゴロ回転させるモデルのTransform（子オブジェクト）")]
    [SerializeField] private Transform rollModelTransform;

    [Header("── 移動 ──")]
    [Tooltip("追従速度")]
    [SerializeField] private float chaseSpeed = 3f;
    [Tooltip("追従中の衝突判定距離")]
    [SerializeField] private float collideDistance = 2f;

    [Header("── 障害物回避 ──")]
    [Tooltip("障害物検知距離（m）")]
    [SerializeField] private float avoidDistance = 2f;

    [Header("── フェーズ2オブジェクト生成（灯籠）──")]
    [Tooltip("灯籠のPrefab")]
    [SerializeField] private GameObject lanternPrefab;
    [Tooltip("灯籠の生成位置（XZ座標）・Y座標は上から落とす")]
    [SerializeField] private List<Transform> lanternSpawnPoints = new List<Transform>();
    [Tooltip("生成するY座標の高さ")]
    [SerializeField] private float spawnHeight = 20f;
    [Tooltip("生成間隔（秒）")]
    [SerializeField] private float spawnInterval = 0.5f;

    [Header("── フェーズ3オブジェクト生成 ──")]
    [Tooltip("フェーズ3オブジェクトのPrefab")]
    [SerializeField] private GameObject phase3ObjectPrefab;
    [Tooltip("フェーズ3オブジェクトの生成位置")]
    [SerializeField] private List<Transform> phase3SpawnPoints = new List<Transform>();

    [Header("── タックル（フェーズ1）──")]
    [Tooltip("タックル速度")]
    [SerializeField] private float tackleSpeed = 12f;
    [Tooltip("タックルの最大移動距離（m）")]
    [SerializeField] private float tackleMaxDistance = 20f;

    [Header("── ゴロゴロ（フェーズ2）──")]
    [Tooltip("ゴロゴロの速度")]
    [SerializeField] private float rollSpeed = 8f;
    [Tooltip("ゴロゴロの最大時間（秒）")]
    [SerializeField] private float rollDuration = 10f;
    [Tooltip("近距離時の最大曲がり角度（度/秒）")]
    [SerializeField] private float rollMaxTurnAngle = 3f;
    [Tooltip("遠距離時の最大曲がり角度（度/秒）")]
    [SerializeField] private float rollMaxTurnAngleFar = 15f;
    [Tooltip("この距離以上離れたら遠距離ホーミングになる（m）")]
    [SerializeField] private float rollHomingDistanceThreshold = 10f;
    [Tooltip("ゴロゴロ中のX軸回転速度")]
    [SerializeField] private float rollRotateSpeed = 360f;
    [Tooltip("ゴロゴロ終了時のジャンプ高さ")]
    [SerializeField] private float rollEndJumpHeight = 3f;
    [Tooltip("ゴロゴロ終了時のジャンプ時間（秒）")]
    [SerializeField] private float rollEndJumpDuration = 0.5f;

    [Header("── ゴロゴロ障害物回避 ──")]
    [Tooltip("障害物検知Rayの長さ")]
    [SerializeField] private float rollAvoidRayLength = 2f;
    [Tooltip("何フレームに1回Raycastするか（重さ軽減）")]
    [SerializeField] private int rollAvoidRayInterval = 3;

    [Header("── 灯籠セット（フェーズ2）──")]
    [SerializeField] private Boss_LanternSet lanternSet;

    [Header("── 攻撃タイミング（共通）──")]
    [Tooltip("追従開始から攻撃までの待機時間（最小・秒）")]
    [SerializeField] private float attackDelayMin = 3f;
    [Tooltip("追従開始から攻撃までの待機時間（最大・秒）")]
    [SerializeField] private float attackDelayMax = 8f;

    [Header("── チャージ ──")]
    [Tooltip("チャージ時間（秒）")]
    [SerializeField] private float chargeDuration = 1f;

    [Header("── 停止（攻撃後） ──")]
    [Tooltip("攻撃後の停止時間（秒）")]
    [SerializeField] private float stopDuration = 3f;

    [Header("── スタン ──")]
    [Tooltip("スタン時間（秒）")]
    [SerializeField] private float stunDuration = 10f;
    [Tooltip("攻撃ヒット時にタイマーをリセットするか")]
    [SerializeField] private bool resetStunTimerOnHit = true;

    [Header("── ステータス ──")]
    [Tooltip("攻撃力（フェーズ1:5 フェーズ2:6）")]
    [SerializeField] private float[] attackPowers = { 5f, 6f, 7f };
    [Tooltip("上方向のノックバック強さ")]
    [SerializeField] private float knockbackUpForce = 5f;
    [Tooltip("ノックバックの持続時間（秒）")]
    [SerializeField] private float knockbackDuration = 0.8f;
    [Tooltip("回復量（フェーズ1:2 フェーズ2:10）")]
    [SerializeField] private float[] inkRecoveries = { 2f, 10f, 10f };

    [Header("── フェーズ別ステータス ──")]
    [Tooltip("必要塗り回数（フェーズ1:10 フェーズ2:12 フェーズ3:15）")]
    [SerializeField] private int[] requiredInkCounts = { 10, 12, 15 };
    [Tooltip("咆哮の円形リセット範囲・直径（フェーズ1:5 フェーズ2:7 フェーズ3:10）単位:m")]
    [SerializeField] private float[] roarRanges = { 5f, 7f, 10f };

    [Header("── 咆哮 ──")]
    [Tooltip("咆哮時にジャンプするフィールド中央の座標")]
    [SerializeField] private Transform fieldCenter;
    [Tooltip("咆哮ジャンプの高さ")]
    [SerializeField] private float roarJumpHeight = 5f;
    [Tooltip("咆哮ジャンプの速度")]
    [SerializeField] private float roarJumpSpeed = 10f;

    [Header("── 咆哮リセット対象 ──")]
    [Tooltip("ボス自身のPaintableSurface")]
    [SerializeField] private List<PaintableSurface> bossSurfaces = new List<PaintableSurface>();

    [Header("── 木箱（フェーズ1）──")]
    [Tooltip("木箱オブジェクト（咆哮で飛ばす対象）")]
    [SerializeField] private List<GameObject> crateObjects = new List<GameObject>();
    [Tooltip("木箱を飛ばす方向と強さ")]
    [SerializeField] private Vector3 crateBlastForce = new Vector3(0f, 10f, 20f);
    [Tooltip("木箱が消えるまでの時間（秒）")]
    [SerializeField] private float crateDestroyDelay = 3f;

    [Header("── 灯籠（フェーズ2）──")]
    [Tooltip("灯籠オブジェクト（咆哮で飛ばす対象）")]
    [SerializeField] private List<GameObject> lanternObjects = new List<GameObject>();
    [Tooltip("灯籠を飛ばす方向と強さ")]
    [SerializeField] private Vector3 lanternBlastForce = new Vector3(0f, 10f, 20f);
    [Tooltip("灯籠が消えるまでの時間（秒）")]
    [SerializeField] private float lanternDestroyDelay = 3f;

    [Header("── スタン演出 ──")]
    [Tooltip("後退速度")]
    [SerializeField] private float recoilSpeed = 5f;
    [Tooltip("後退時間（秒）")]
    [SerializeField] private float recoilDuration = 0.5f;
    [Tooltip("傾く時間（秒）")]
    [SerializeField] private float tiltDuration = 0.3f;

    [Header("── ボスエリア ──")]
    [Tooltip("ボスエリアのPointA")]
    [SerializeField] private Transform areaPointA;
    [Tooltip("ボスエリアのPointB")]
    [SerializeField] private Transform areaPointB;

    [Header("── 攻撃予告 ──")]
    [SerializeField] private AttackIndicator attackIndicator;

    [Header("── 地面追従 ──")]
    [SerializeField] private float groundFollowSpeed = 10f;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    private BossState state = BossState.Idle;
    private int currentPhase = 0;
    private int inkHitCount = 0;
    private bool isAlly = false;

    // Chase用
    private float attackTimer = 0f;
    private float attackDelay = 0f;

    // Charge用
    private float chargeTimer = 0f;
    private Vector3 chargeTargetDir;

    // Tackle用（フェーズ1）
    private Vector3 tackleDirection;
    private Vector3 tackleStartPos;

    // Roll用（フェーズ2）
    private float rollTimer = 0f;
    private Vector3 rollDirection;
    private int bounceCount = 0; // バウンドフラグ成立回数
    private bool canStunOnBounce = true; // バウンドでスタンできるか
    private float rollXRotation = 0f;
    private float avoidTimer = 0f;
    private float avoidDuration = 0.5f; // 回避後この時間で元に戻る
    private bool isAvoiding = false;
    private float bounceHomingDisableTimer = 0f;
    private float bounceHomingDisableDuration = 1.5f; // バウンド後この時間はホーミング無効
    /// <summary>ゴロゴロ中かどうかを返す</summary>
    public bool GetIsRolling() => state == BossState.Roll;

    private int rollAvoidFrameCount = 0;
    private Vector3 rollAvoidMoveDir; // キャッシュした移動方向

    // Stop用
    private float stopTimer = 0f;

    // Stun用
    private float stunTimer = 0f;
    private float lastInkTime = -999f;
    private float inkCooldown = 0.5f;

    // ノックバック
    private CharacterController playerController;
    private PlayerMove playerMove;
    private Vector3 knockbackVelocity;
    private float knockbackTimer;
    private bool isPlayerKnockedBack;

    private CharacterController bossController;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                player = go.transform;
                playerController = go.GetComponent<CharacterController>();
                if (playerController == null)
                    playerController = go.GetComponentInChildren<CharacterController>();
                if (playerController == null)
                    playerController = go.GetComponentInParent<CharacterController>();
            }
        }
        else
        {
            playerController = player.GetComponent<CharacterController>();
            if (playerController == null)
                playerController = player.GetComponentInChildren<CharacterController>();
            if (playerController == null)
                playerController = player.GetComponentInParent<CharacterController>();
        }

        if (player != null)
        {
            playerMove = player.GetComponent<PlayerMove>();
            if (playerMove == null)
                playerMove = player.GetComponentInChildren<PlayerMove>();
            if (playerMove == null)
                playerMove = player.GetComponentInParent<PlayerMove>();
        }

        bossController = GetComponent<CharacterController>();

        // デバッグ用フェーズ設定
        currentPhase = Mathf.Clamp(debugStartPhase, 0, 2);
        inkHitCount = 0;
        state = BossState.Idle;

        Debug.Log($"[Boss_SB] 開始フェーズ: {currentPhase + 1}");
    }

    // ====================================================================
    //  毎フレーム
    // ====================================================================

    private void Update()
    {
        if (player == null) return;

        UpdatePlayerKnockback();
        ClampToArea();
        CheckPlayerCollision();

        switch (state)
        {
            case BossState.Idle: break;
            case BossState.Chase: UpdateChase(); break;
            case BossState.Charge: UpdateCharge(); break;
            case BossState.Tackle: UpdateTackle(); break;
            case BossState.Roll: UpdateRoll(); break;
            case BossState.RollEnd: break; // コルーチンで処理
            case BossState.Stop: UpdateStop(); break;
            case BossState.Stun: UpdateStun(); break;
            case BossState.Roar: break;
            case BossState.Defeated: break;
        }
    }

    // ====================================================================
    //  外部から呼ぶ関数
    // ====================================================================

    /// <summary>ボス戦を開始する（BossAreaTriggerから呼ぶ）</summary>
    public void StartBossBattle()
    {
        if (state != BossState.Idle) return;
        inkHitCount = 0;
        Debug.Log($"[Boss_SB] ボス戦開始！フェーズ{currentPhase + 1}");
        EnterChase();
    }

    /// <summary>墨を塗られたときの処理（EnemyHitReceiverから呼ぶ）</summary>
    public void ReceiveInk()
    {
        if (isAlly) return;
        if (state == BossState.Roar) return;
        if (state == BossState.Defeated) return;

        int required = requiredInkCounts[currentPhase];

        if (Time.time - lastInkTime < inkCooldown) return;
        lastInkTime = Time.time;

        if (state != BossState.Stun)
        {
            if (inkHitCount >= required - 1) return;
            inkHitCount++;
            Debug.Log($"[Boss_SB] 塗り回数: {inkHitCount} / {required - 1}（スタン外上限）フェーズ{currentPhase + 1}");
            return;
        }

        inkHitCount++;
        Debug.Log($"[Boss_SB] 塗り回数(スタン中): {inkHitCount} / {required} フェーズ{currentPhase + 1}");

        if (resetStunTimerOnHit)
            stunTimer = 0f;

        if (inkHitCount >= required)
        {
            inkHitCount = 0;
            EnterRoar();
        }
    }

    /// <summary>お墨付き状態を返す</summary>
    public bool GetIsAlly() => isAlly;

    /// <summary>現在のフェーズを返す</summary>
    public int GetCurrentPhase() => currentPhase;

    /// <summary>
    /// 木箱に衝突したときに呼ぶ（フェーズ1・木箱スクリプトから呼ぶ）
    /// タックル中のみスタンに移行する
    /// </summary>
    public void NotifyHitCrate()
    {
        if (currentPhase != 0) return;
        if (state != BossState.Tackle) return;
        Debug.Log("[Boss_SB] 木箱に衝突！スタン開始");
        EnterStun();
    }

    /// <summary>
    /// 灯籠にバウンドしたときに呼ぶ（フェーズ2・灯籠スクリプトから呼ぶ）
    /// isInner: 内面に当たったか（trueでバウンドフラグ成立）
    /// newDirection: バウンド後の方向
    /// </summary>
    public void NotifyLanternBounce(bool isInner, Vector3 newDirection)
    {
        if (currentPhase != 1) return;
        if (state != BossState.Roll) return;

        // 方向を更新
        rollDirection = newDirection.normalized;
        rollDirection.y = 0f;

        bounceHomingDisableTimer = bounceHomingDisableDuration;

        if (isInner)
        {
            // 内面バウンド → フラグ成立
            bounceCount++;
            Debug.Log($"[Boss_SB] 灯籠内面バウンド！フラグ成立: {bounceCount}/3");

            if (bounceCount >= 3)
            {
                // 3回でスタン
                Debug.Log("[Boss_SB] 3回バウンド！スタン開始");
                EnterStun();
            }
        }
        else
        {
            // 外面バウンド → フラグ不成立・3秒後に移動に戻る
            Debug.Log("[Boss_SB] 灯籠外面バウンド。3秒後に移動に戻る");
            StartCoroutine(RollEndAfterDelay(3f));
        }
    }

    /// <summary>現在のゴロゴロ方向を返す（灯籠スクリプトからバウンド計算用）</summary>
    public Vector3 GetRollDirection() => rollDirection;

    private void CheckPlayerCollision()
    {
        if (state == BossState.Idle ||
            state == BossState.Stop ||
            state == BossState.Stun ||
            state == BossState.Roar ||
            state == BossState.RollEnd ||
            state == BossState.Defeated) return;

        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;
        Vector3 playerPos = player.position;
        bossPos.y = 0f;
        playerPos.y = 0f;
        float dist = Vector3.Distance(bossPos, playerPos);


        if (dist <= collideDistance)
        {
            ApplyKnockbackToPlayer();

            if (state == BossState.Tackle)
                EnterStop();
            else if (state == BossState.Roll)
                StartCoroutine(RollEndCoroutine());
            else if (state == BossState.Chase)
                EnterStop();
        }
    }

    // ====================================================================
    //  Chase（追従）
    // ====================================================================

    private void EnterChase()
    {
        state = BossState.Chase;
        attackTimer = 0f;
        attackDelay = Random.Range(attackDelayMin, attackDelayMax);
        bounceCount = 0;

        StartCoroutine(StandUpCoroutine());

        if (lanternSet != null)
            lanternSet.ResetBounceHistory();

        Debug.Log($"[Boss_SB] Chase開始。攻撃まで{attackDelay:F1}秒 フェーズ{currentPhase + 1}");
    }

    private void UpdateChase()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackDelay)
        {
            // フェーズによって攻撃パターンを切り替える
            if (currentPhase == 0)
                EnterCharge(); // フェーズ1: タックル
            else
                EnterRoll();   // フェーズ2以降: ゴロゴロ
            return;
        }

        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;
        Vector3 playerPos = player.position;
        bossPos.y = 0f;
        playerPos.y = 0f;
        float dist = Vector3.Distance(bossPos, playerPos);

        if (dist > collideDistance)
        {
            Vector3 moveDir = GetAvoidanceDirection(toPlayer.normalized);
            Vector3 move = moveDir * chaseSpeed * Time.deltaTime;
            move.y = -9.8f * Time.deltaTime;
            bossController.Move(move);
        }

        LookAt(toPlayer);
    }

    // ====================================================================
    //  Charge（チャージ・フェーズ1）
    // ====================================================================

    private void EnterCharge()
    {
        state = BossState.Charge;
        chargeTimer = 0f;
        chargeTargetDir = (player.position - transform.position).normalized;
        chargeTargetDir.y = 0f;

        if (attackIndicator != null)
            attackIndicator.Show(chargeTargetDir);

        Debug.Log("[Boss_SB] チャージ開始！");
    }

    private void UpdateCharge()
    {
        chargeTimer += Time.deltaTime;
        LookAt(chargeTargetDir);

        if (chargeTimer >= chargeDuration)
            EnterTackle();
    }

    // ====================================================================
    //  Tackle（タックル・フェーズ1）
    // ====================================================================

    private void EnterTackle()
    {
        state = BossState.Tackle;
        tackleDirection = chargeTargetDir;
        tackleStartPos = transform.position;

        if (attackIndicator != null)
            attackIndicator.Hide();

        Debug.Log("[Boss_SB] タックル開始！");
    }

    private void UpdateTackle()
    {
        Vector3 tackleMove = tackleDirection * tackleSpeed * Time.deltaTime;
        tackleMove.y = -9.8f * Time.deltaTime;
        bossController.Move(tackleMove);
        LookAt(tackleDirection);

        if (IsOutOfArea())
        {
            ClampToArea();
            EnterStop();
            return;
        }

        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;
        Vector3 playerPos = player.position;
        bossPos.y = 0f;
        playerPos.y = 0f;
        float dist = Vector3.Distance(bossPos, playerPos);

        float travelDist = Vector3.Distance(transform.position, tackleStartPos);
        if (travelDist > tackleMaxDistance)
            EnterStop();
    }

    // ====================================================================
    //  Roll（ゴロゴロ・フェーズ2）
    //  ★ゴロゴロの挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterRoll()
    {
        state = BossState.Roll;
        rollTimer = 0f;
        rollXRotation = 0f;
        rollDirection = (player.position - transform.position).normalized;
        rollDirection.y = 0f;
        Debug.Log("[Boss_SB] ゴロゴロ開始！");
    }

    private void UpdateRoll()
    {
        rollTimer += Time.deltaTime;
        if (rollTimer >= rollDuration)
        {
            StartCoroutine(RollEndCoroutine());
            return;
        }

        // ホーミング（バウンド後は一時無効）
        if (bounceHomingDisableTimer > 0f)
        {
            bounceHomingDisableTimer -= Time.deltaTime;
        }
        else
        {
            Vector3 toPlayer = (player.position - transform.position).normalized;
            toPlayer.y = 0f;

            float distToPlayer = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(player.position.x, 0, player.position.z));

            float homingRate = Mathf.Lerp(
                rollMaxTurnAngle,
                rollMaxTurnAngleFar,
                Mathf.Clamp01(distToPlayer / rollHomingDistanceThreshold));

            rollDirection = Vector3.RotateTowards(
                rollDirection,
                toPlayer,
                homingRate * Mathf.Deg2Rad * Time.deltaTime,
                0f).normalized;
            rollDirection.y = 0f;
        }

        Vector3 moveDir = rollDirection;

        // フレームを間引いてRaycast
        rollAvoidFrameCount++;
        if (rollAvoidFrameCount >= rollAvoidRayInterval)
        {
            rollAvoidFrameCount = 0;
            rollAvoidMoveDir = rollDirection; // デフォルトは進行方向

            if (Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                rollDirection,
                rollAvoidRayLength,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 rightDir = Quaternion.Euler(0, 45f, 0) * rollDirection;
                if (!Physics.Raycast(
                    transform.position + Vector3.up * 0.5f,
                    rightDir,
                    rollAvoidRayLength,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                {
                    rollAvoidMoveDir = rightDir;
                }
                else
                {
                    rollAvoidMoveDir = Quaternion.Euler(0, -45f, 0) * rollDirection;
                }
            }
        }

        // キャッシュした方向で移動
        Vector3 move = rollAvoidMoveDir * rollSpeed * Time.deltaTime;
        move.y = -9.8f * Time.deltaTime;
        bossController.Move(move);

        // 親オブジェクトはY軸（左右）だけ回転
        if (rollDirection.sqrMagnitude > 0.01f)
        {
            float targetY = Quaternion.LookRotation(rollDirection).eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, targetY, 0f);
        }

        // 子オブジェクトはX軸（ゴロゴロ）だけ回転
        rollXRotation += rollRotateSpeed * Time.deltaTime;
        if (rollModelTransform != null)
            rollModelTransform.localRotation = Quaternion.Euler(rollXRotation, 0f, 0f);

        // エリア外に出たら停止
        if (IsOutOfArea())
        {
            ClampToArea();
            StartCoroutine(RollEndCoroutine());
            return;
        }
    }

    /// <summary>一定時間後にゴロゴロを終了する（外面バウンド時に使う）</summary>
    private IEnumerator RollEndAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (state == BossState.Roll)
            StartCoroutine(RollEndCoroutine());
    }

    /// <summary>ゴロゴロ終了：ジャンプ→着地→移動</summary>
    private IEnumerator RollEndCoroutine()
    {
        state = BossState.RollEnd;
        Debug.Log("[Boss_SB] ゴロゴロ終了。ジャンプ→着地");

        Vector3 startPos = transform.position;
        Vector3 jumpVelocity = rollDirection * rollSpeed; // 慣性として転がり方向の速度を引き継ぐ
        float elapsed = 0f;

        Quaternion startModelRot = rollModelTransform != null
        ? rollModelTransform.localRotation
        : Quaternion.identity;

        while (elapsed < rollEndJumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rollEndJumpDuration;

            // 慣性移動
            float inertiaRate = 1f - t;
            Vector3 inertiaMove = jumpVelocity * inertiaRate * Time.deltaTime;
            inertiaMove.y = 0f;

            float yOffset = Mathf.Sin(t * Mathf.PI) * rollEndJumpHeight;

            transform.position = new Vector3(
                startPos.x + inertiaMove.x * rollEndJumpDuration,
                startPos.y + yOffset,
                startPos.z + inertiaMove.z * rollEndJumpDuration);

            startPos = new Vector3(
                transform.position.x,
                startPos.y,
                transform.position.z);

            // ↓ ジャンプ中に回転を戻す
            if (rollModelTransform != null)
                rollModelTransform.localRotation = Quaternion.RotateTowards(
                    rollModelTransform.localRotation,
                    Quaternion.identity,
                    rollRotateSpeed * Time.deltaTime);

            yield return null;
        }

        transform.position = new Vector3(
            transform.position.x,
            startPos.y,
            transform.position.z);

        float resetDuration = 0.3f;
        elapsed = 0f;

        // 時間ではなく回転が戻りきったら終了
        while (rollModelTransform != null &&
               Quaternion.Angle(rollModelTransform.localRotation, Quaternion.identity) > 0.1f)
        {
            rollModelTransform.localRotation = Quaternion.RotateTowards(
                rollModelTransform.localRotation,
                Quaternion.identity,
                rollRotateSpeed * Time.deltaTime);

            yield return null;
        }

        if (rollModelTransform != null)
            rollModelTransform.localRotation = Quaternion.identity;

        rollXRotation = 0f;
        EnterChase();
    }
    // ====================================================================
    //  Stop（停止）
    // ====================================================================

    private void EnterStop()
    {
        state = BossState.Stop;
        stopTimer = 0f;
        Debug.Log("[Boss_SB] 停止");
    }

    private void UpdateStop()
    {
        stopTimer += Time.deltaTime;
        if (stopTimer >= stopDuration)
            EnterChase();
    }

    // ====================================================================
    //  Stun（スタン）
    // ====================================================================

    private void EnterStun()
    {
        state = BossState.Stun;
        stunTimer = 0f;
        Debug.Log("[Boss_SB] スタン！");

        StartCoroutine(StunRecoilCoroutine());
        StartCoroutine(StunTiltCoroutine());

        if (rollModelTransform != null)
        {
            rollModelTransform.localRotation = Quaternion.identity;
            rollXRotation = 0f;
        }
    }

    private void UpdateStun()
    {
        stunTimer += Time.deltaTime;

        if (stunTimer >= stunDuration)
        {
            Debug.Log("[Boss_SB] スタン解除。塗り引き継ぎ");
            EnterChase();
        }
    }

    /// <summary>
    /// 木箱に当たったがスタンしない場合（塗り量不足）
    /// タックルを止めるだけ
    /// </summary>
    public void NotifyHitCrateNoStun()
    {
        if (state != BossState.Tackle) return;
        Debug.Log("[Boss_SB] 木箱に当たったが塗り量不足。停止のみ");
        EnterStop();
    }

    // ====================================================================
    //  Roar（咆哮）
    // ====================================================================

    private void EnterRoar()
    {
        state = BossState.Roar;
        Debug.Log($"[Boss_SB] 咆哮！フェーズ{currentPhase + 1}");
        StartCoroutine(RoarCoroutine());
    }

    private IEnumerator RoarCoroutine()
    {
        yield return StartCoroutine(StandUpCoroutine());

        if (fieldCenter != null)
            yield return StartCoroutine(JumpToCenter());

        yield return new WaitForSeconds(0.5f);

        // ボス自身の墨をリセット
        foreach (var surface in bossSurfaces)
        {
            if (surface == null || !surface.enabled) continue;
            try { InkPaintService.ClearAll(surface); } catch { }
        }

        // フィールドを円形にリセット
        float roarRange = roarRanges[currentPhase];
        ResetFieldInRange(roarRange * 0.5f);

        // フェーズに応じてオブジェクトを飛ばして次のオブジェクトを生成
        if (currentPhase == 0)
        {
            // 木箱を飛ばす
            BlastObjects(crateObjects, crateBlastForce, crateDestroyDelay);

            // 灯籠を上から生成
            yield return StartCoroutine(SpawnObjectsFromAbove(
                lanternPrefab, lanternSpawnPoints));
        }
        else if (currentPhase == 1)
        {
            // 灯籠を飛ばす
            BlastObjects(lanternObjects, lanternBlastForce, lanternDestroyDelay);

            // フェーズ3オブジェクトを上から生成
            yield return StartCoroutine(SpawnObjectsFromAbove(
                phase3ObjectPrefab, phase3SpawnPoints));
        }

        Debug.Log($"[Boss_SB] 咆哮完了。フェーズ{currentPhase + 1}");

        yield return new WaitForSeconds(0.5f);

        currentPhase++;

        if (currentPhase >= 3)
            EnterDefeated();
        else
        {
            inkHitCount = 0;
            Debug.Log($"[Boss_SB] フェーズ{currentPhase + 1}へ移行");
            EnterChase();
        }
    }

    /// <summary>オブジェクトを上から順番に落とすコルーチン</summary>
    private IEnumerator SpawnObjectsFromAbove(
        GameObject prefab, List<Transform> spawnPoints)
    {
        if (prefab == null || spawnPoints == null) yield break;

        foreach (var point in spawnPoints)
        {
            if (point == null) continue;

            // 生成位置（XZはSpawnPoint・Yは上から）
            Vector3 spawnPos = new Vector3(
                point.position.x,
                point.position.y + spawnHeight,
                point.position.z);

            GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);

            // Rigidbodyがあれば重力で落ちる
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            Debug.Log($"[Boss_SB] 生成: {obj.name} at {spawnPos}");

            // 間隔をあけて順番に落とす
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private IEnumerator JumpToCenter()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = fieldCenter.position;
        float elapsed = 0f;
        float duration = Vector3.Distance(startPos, endPos) / roarJumpSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * roarJumpHeight;
            transform.position = pos;
            yield return null;
        }

        transform.position = endPos;
    }

    private void ResetFieldInRange(float radius)
    {
        var processed = new HashSet<PaintableSurface>();
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (var col in colliders)
        {
            var surface = col.GetComponent<PaintableSurface>()
                       ?? col.GetComponentInParent<PaintableSurface>();
            if (surface == null || !surface.enabled) continue;
            if (processed.Contains(surface)) continue;
            processed.Add(surface);
            try { InkPaintService.EraseAt(surface, transform.position, radius); } catch { }
        }
    }

    /// <summary>オブジェクトリストを飛ばす（木箱・灯籠共通）</summary>
    private void BlastObjects(List<GameObject> objects, Vector3 blastForce, float destroyDelay)
    {
        foreach (var obj in objects)
        {
            if (obj == null) continue;
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(blastForce, ForceMode.Impulse);
            }
            else
            {
                obj.SetActive(false);
            }
            StartCoroutine(DestroyAfterDelay(obj, destroyDelay));
        }
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
            obj.SetActive(false);
    }

    // ====================================================================
    //  Defeated（撃破）
    // ====================================================================

    private void EnterDefeated()
    {
        state = BossState.Defeated;
        Debug.Log("[Boss_SB] ボス撃破！");
        Destroy(gameObject, 1f);
    }

    // ====================================================================
    //  ノックバック
    // ====================================================================

    private void ApplyKnockbackToPlayer()
    {
        if (isPlayerKnockedBack) return;
        if (playerController == null) return;

        float knockbackDistance = attackPowers[currentPhase] * 0.5f;
        Vector3 knockDir = (player.position - transform.position).normalized;
        knockDir.y = 0f;

        knockbackVelocity = knockDir * knockbackDistance * 10f
                          + Vector3.up * knockbackUpForce;
        knockbackTimer = 0f;
        isPlayerKnockedBack = true;

        if (playerMove != null)
        {
            playerMove.SetExternalGravityEnabled(false);
            playerMove.ClearVerticalVelocity();
        }
    }

    private void UpdatePlayerKnockback()
    {
        if (!isPlayerKnockedBack) return;
        if (playerController == null) return;

        knockbackVelocity.y += Physics.gravity.y * Time.deltaTime;
        playerController.Move(knockbackVelocity * Time.deltaTime);
        knockbackTimer += Time.deltaTime;

        if (knockbackTimer >= knockbackDuration)
        {
            isPlayerKnockedBack = false;
            if (playerMove != null)
                playerMove.SetExternalGravityEnabled(true);
        }
    }

    // ====================================================================
    //  エリア制限
    // ====================================================================

    private void ClampToArea()
    {
        if (areaPointA == null || areaPointB == null) return;

        Vector3 min = Vector3.Min(areaPointA.position, areaPointB.position);
        Vector3 max = Vector3.Max(areaPointA.position, areaPointB.position);

        Vector3 checkPos = bodyTransform != null ? bodyTransform.position : transform.position;
        Vector3 offset = checkPos - transform.position;

        float clampedX = Mathf.Clamp(checkPos.x, min.x, max.x);
        float clampedZ = Mathf.Clamp(checkPos.z, min.z, max.z);

        transform.position = new Vector3(
            clampedX - offset.x,
            transform.position.y,
            clampedZ - offset.z);
    }

    private bool IsOutOfArea()
    {
        if (areaPointA == null || areaPointB == null) return false;

        Vector3 min = Vector3.Min(areaPointA.position, areaPointB.position);
        Vector3 max = Vector3.Max(areaPointA.position, areaPointB.position);

        Vector3 checkPos = bodyTransform != null ? bodyTransform.position : transform.position;
        return checkPos.x < min.x || checkPos.x > max.x ||
               checkPos.z < min.z || checkPos.z > max.z;
    }

    // ====================================================================
    //  障害物回避
    // ====================================================================

    private Vector3 GetAvoidanceDirection(Vector3 desiredDir)
    {
        if (!Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            desiredDir, avoidDistance, ~0,
            QueryTriggerInteraction.Ignore))
        {
            isAvoiding = false;
            return desiredDir;
        }

        // 右方向を確認
        Vector3 rightDir = Quaternion.Euler(0, 45f, 0) * desiredDir;
        if (!Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            rightDir, avoidDistance, ~0,
            QueryTriggerInteraction.Ignore))
        {
            isAvoiding = true;
            return rightDir;
        }

        // 左方向を確認
        Vector3 leftDir = Quaternion.Euler(0, -45f, 0) * desiredDir;
        if (!Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            leftDir, avoidDistance, ~0,
            QueryTriggerInteraction.Ignore))
        {
            isAvoiding = true;
            return leftDir;
        }

        return desiredDir;
    }

    // ====================================================================
    //  スタン演出
    // ====================================================================

    private IEnumerator StunRecoilCoroutine()
    {
        float elapsed = 0f;
        Vector3 recoilDir = state == BossState.Roll ? -rollDirection : -tackleDirection;

        while (elapsed < recoilDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / recoilDuration);
            float smoothT = t * t;
            Vector3 move = recoilDir * recoilSpeed * smoothT * Time.deltaTime;
            move.y = -9.8f * Time.deltaTime;
            bossController.Move(move);
            yield return null;
        }
    }

    private IEnumerator StunTiltCoroutine()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion tiltRot = startRot * Quaternion.Euler(-90f, 0f, 0f);

        while (elapsed < tiltDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tiltDuration;
            transform.rotation = Quaternion.Slerp(startRot, tiltRot, t);
            yield return null;
        }

        transform.rotation = tiltRot;
    }

    private IEnumerator StandUpCoroutine()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion upRot = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

        while (elapsed < tiltDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tiltDuration;
            transform.rotation = Quaternion.Slerp(startRot, upRot, t);
            yield return null;
        }

        transform.rotation = upRot;
    }

    // ====================================================================
    //  ユーティリティ
    // ====================================================================

    private void LookAt(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        direction.y = 0f;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            10f * Time.deltaTime);
    }

    // ====================================================================
    //  Gizmos
    // ====================================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bossPos, collideDistance);

        if (roarRanges != null && roarRanges.Length > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            float range = roarRanges[Mathf.Min(currentPhase, roarRanges.Length - 1)];
            Gizmos.DrawWireSphere(transform.position, range * 0.5f);
        }

        // エリア範囲
        if (areaPointA != null && areaPointB != null)
        {
            Vector3 min = Vector3.Min(areaPointA.position, areaPointB.position);
            Vector3 max = Vector3.Max(areaPointA.position, areaPointB.position);
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, size);
        }
    }
}