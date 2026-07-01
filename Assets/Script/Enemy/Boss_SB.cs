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
        HipDropCharge, // ヒップドロップのチャージ（フェーズ3）
        HipDrop,    // ヒップドロップ降下（フェーズ3）
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

    [Header("── エフェクト ──")]
    [SerializeField] private GameObject tackleEffect;
    [SerializeField] private GameObject stunEffect;
    [SerializeField] private GameObject stunHitEffect;
    [SerializeField] private GameObject roarEffect;
    [SerializeField] private GameObject rollEffect;
    [SerializeField] private GameObject chargeEffect;

    [Header("── ヒットエフェクト ──")]
    [SerializeField] private HitEffectPlayer hitEffectPlayer;

    [Header("── 撃破時の大きいヒットエフェクト ──")]
    [Tooltip("HPを削り切った瞬間に出す大きいエフェクト（未設定ならstunHitEffectを拡大して使う）")]
    [SerializeField] private GameObject finalHitBigEffect;
    [Tooltip("finalHitBigEffectが未設定の場合のスケール倍率")]
    [SerializeField] private float finalHitScaleMultiplier = 2f;

    [Header("── ヒップドロップ予告 ──")]
    [SerializeField] private HipDropIndicator hipDropIndicator;

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
    [Tooltip("灯籠が地面に埋まる深さ（m）")]
    [SerializeField] private float buriedDepth = 1f;

    [Header("── フェーズ3オブジェクト生成 ──")]
    [Tooltip("フェーズ3オブジェクトのPrefab")]
    [SerializeField] private GameObject phase3ObjectPrefab;
    [Tooltip("フェーズ3オブジェクトの生成位置")]
    [SerializeField] private List<Transform> phase3SpawnPoints = new List<Transform>();
    [Tooltip("フェーズ3オブジェクトの生成時の角度")]
    [SerializeField] private Vector3 phase3ObjectRotation = Vector3.zero;
    [Header("── フェーズ3木箱生成 ──")]
    [Tooltip("フェーズ3木箱のPrefab")]
    [SerializeField] private GameObject phase3CratePrefab;
    [Tooltip("フェーズ3木箱の生成位置")]
    [SerializeField] private List<Transform> phase3CrateSpawnPoints = new List<Transform>();

    [Header("── タックル（フェーズ1）──")]
    [Tooltip("タックル速度")]
    [SerializeField] private float tackleSpeed = 12f;
    [Tooltip("タックルの最大移動距離（m）")]
    [SerializeField] private float tackleMaxDistance = 20f;

    [Header("── チャージ ──")]
    [Tooltip("チャージ時間（秒）")]
    [SerializeField] private float chargeDuration = 1f;
    [Tooltip("チャージ中のホーミング速度（0=追わない）")]
    [SerializeField] private float chargeHomingSpeed = 5f;

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

    [Header("── ゴロゴロ予備動作 ──")]
    [Tooltip("後ろに傾く角度（度）")]
    [SerializeField] private float rollWindupTiltAngle = 20f;
    [Tooltip("傾く時間（秒）")]
    [SerializeField] private float rollWindupTiltDuration = 0.3f;
    [Tooltip("戻る時間（秒）")]
    [SerializeField] private float rollWindupReturnDuration = 0.15f;

    [Header("── 灯籠セット（フェーズ2）──")]
    [SerializeField] private Boss_LanternSet lanternSet;
    [Tooltip("スタンに必要な灯籠バウンド回数")]
    [SerializeField] private int requiredBounceCount = 3;

    [Header("── ヒップドロップ（フェーズ3）──")]
    [Tooltip("HipDropを選ぶ確率（0〜1）残りがTackle")]
    [SerializeField] private float hipDropChance = 0.7f;
    [Tooltip("ジャンプの高さ")]
    [SerializeField] private float hipDropJumpHeight = 6f;
    [Tooltip("チャージ時間（ホーミングする時間・秒）")]
    [SerializeField] private float hipDropChargeDuration = 1.5f;
    [Tooltip("チャージ中のホーミング速度（小さいほど避けやすい）")]
    [SerializeField] private float hipDropHomingSpeed = 4f;
    [Tooltip("降下速度")]
    [SerializeField] private float hipDropFallSpeed = 25f;
    [Tooltip("位置確定から降下開始までの待機時間（秒）")]
    [SerializeField] private float hipDropDelayBeforeFall = 0.4f;
    [Tooltip("連続ヒップドロップ回数")]
    [SerializeField] private int hipDropCount = 3;
    [Tooltip("着地後の硬直時間（秒）")]
    [SerializeField] private float hipDropRecoverTime = 0.3f;

    [Header("── フェーズ3 スタン中の押し ──")]
    [Tooltip("攻撃が当たったときにボスを押す距離（m）")]
    [SerializeField] private float pushDistance = 0.3f;
    [Tooltip("押し動作にかける時間（秒）")]
    [SerializeField] private float pushDuration = 0.3f;
    [Tooltip("この距離以内に大砲があれば補正をかける（m）")]
    [SerializeField] private float cannonCorrectionRadius = 5f;
    [Tooltip("大砲が近くにあるときの補正の強さ（0=補正なし、1=完全に大砲方向）")]
    [SerializeField, Range(0f, 1f)] private float pushTowardCannonBlend = 0.4f;
    [SerializeField] private Transform cannonEntrancePoint;

    [Header("── 地面 ──")]
    [SerializeField] private Transform groundTransform;

    [Header("── 大砲タックル ──")]
    [Tooltip("大砲衝突後の後退速度")]
    [SerializeField] private float cannonBounceSpeed = 5f;
    [Tooltip("大砲衝突後の後退時間（秒）")]
    [SerializeField] private float cannonBounceDuration = 0.5f;


    [Header("── 攻撃タイミング（共通）──")]
    [Tooltip("追従開始から攻撃までの待機時間（最小・秒）")]
    [SerializeField] private float attackDelayMin = 3f;
    [Tooltip("追従開始から攻撃までの待機時間（最大・秒）")]
    [SerializeField] private float attackDelayMax = 8f;

    [Header("── 停止（攻撃後） ──")]
    [Tooltip("攻撃後の停止時間（秒）")]
    [SerializeField] private float stopDuration = 3f;

    [Header("── スタン ──")]
    [Tooltip("スタン時間（秒）")]
    [SerializeField] private float stunDuration = 10f;
    [Tooltip("攻撃ヒット時にタイマーをリセットするか")]
    [SerializeField] private bool resetStunTimerOnHit = true;
    [Tooltip("フェーズ3スタン時のエフェクトZ位置")]
    [SerializeField] private float stunEffectPhase3Z = -1f;
    [Header("── HP削り切り後の余韻 ──")]
    [Tooltip("HPを削り切ってから起き上がる（咆哮）までの待機時間（秒）")]
    [SerializeField] private float defeatStunLingerTime = 1.5f;

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

    [Header("── 撃破演出 ──")]
    [SerializeField] private GameObject defeatEffect;
    [Tooltip("撃破時のスタン時間（秒）")]
    [SerializeField] private float defeatStunDuration = 5f;
    [Tooltip("倒れてからスタンエフェクトが出るまでの時間（秒）")]
    [SerializeField] private float defeatStunEffectDelay = 3f;

    [Header("── ボスエリア ──")]
    [Tooltip("ボスエリアのPointA")]
    [SerializeField] private Transform areaPointA;
    [Tooltip("ボスエリアのPointB")]
    [SerializeField] private Transform areaPointB;

    [Header("── 大砲位置 ──")]
    [SerializeField] private Transform cannonMuzzleTransform;
    [SerializeField] private float cannonSafeRadius = 2f;

    [Header("── 攻撃予告 ──")]
    [SerializeField] private AttackIndicator attackIndicator;

    [Header("── 地面追従 ──")]
    [SerializeField] private float groundFollowSpeed = 10f;

    [System.Serializable]
    public class SoundEffect
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = false;
    }

    [Header("── SE ──")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private SoundEffect chargeSE;
    [SerializeField] private SoundEffect tackleSE;
    [SerializeField] private SoundEffect rollSE;
    [SerializeField] private SoundEffect jumpSE;
    [SerializeField] private SoundEffect hitCrateSE;
    [SerializeField] private SoundEffect pinballSE;
    [SerializeField] private SoundEffect cannonEnterSE;
    [SerializeField] private SoundEffect stunSE;
    [SerializeField] private SoundEffect roarSE;
    [SerializeField] private SoundEffect defeatSE;
    [SerializeField] private SoundEffect stunHitSE;
    [SerializeField] private SoundEffect stunHitFinalSE;

    [Header("── ローリングSE（クロスフェードループ用） ──")]
    [SerializeField] private AudioSource rollAudioSourceA;
    [SerializeField] private AudioSource rollAudioSourceB;
    [Tooltip("ループの何秒前から次の再生を重ねて始めるか")]
    [SerializeField] private float rollCrossfadeOverlap = 0.2f;

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
    private bool isRollWindup = false;

    private Coroutine rollLoopCoroutine;

    /// <summary>ゴロゴロ中かどうかを返す</summary>
    public bool GetIsRolling() => state == BossState.Roll;

    // HipDrop用（フェーズ3）
    private int hipDropCurrentCount = 0;     // 現在何回目の降下か
    private float hipDropChargeTimer = 0f;
    private Vector3 hipDropTargetPos;        // 確定した降下先
    private bool isHitCannon = false;        // 砲口に当たったか
    /// <summary>ヒップドロップ中（降下中）かを返す</summary>
    public bool GetIsHipDropping() => state == BossState.HipDrop;
    private bool isLaunching = false; // 打ち上げ中フラグ

    private int rollAvoidFrameCount = 0;
    private Vector3 rollAvoidMoveDir; // キャッシュした移動方向

    private bool isPushing = false; // 押し処理が重複しないように

    /// <summary>フェーズ3で木箱によりスタンしているか（大砲ヒットによるスタンではない）</summary>
    public bool GetIsPhase3BoxStun() => currentPhase == 2 && state == BossState.Stun && !isHitCannon;

    // Stop用
    private float stopTimer = 0f;

    private bool pendingHipDrop = false;
    private BossState prevState = BossState.Idle;

    // Stun用
    private float stunTimer = 0f;
    private float lastInkTime = -999f;
    private float inkCooldown = 0.5f;
    private bool isWaitingToRoar = false;

    // ノックバック
    private CharacterController playerController;
    private PlayerMove playerMove;

    private CharacterController bossController;
    private PlayerStats playerStats;

    float tackleSEPreDelay = 0.3f;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

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
            playerStats = player.GetComponent<PlayerStats>();
            if (playerStats == null)
                playerStats = player.GetComponentInChildren<PlayerStats>();
            if (playerStats == null)
                playerStats = player.GetComponentInParent<PlayerStats>();
        }

        if (cannonMuzzleTransform == null)
        {
            var muzzle = FindObjectOfType<CannonMuzzle>();
            if (muzzle != null)
                cannonMuzzleTransform = muzzle.transform;
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

        if (!isLaunching)
            ClampToArea();
        else
            Debug.Log("[Boss_SB] 打ち上げ中なのでClampToAreaスキップ");

        ClampToArea();
        CheckPlayerCollision();
        UpdateEffects();

        switch (state)
        {
            case BossState.Idle: break;
            case BossState.Chase: UpdateChase(); break;
            case BossState.Charge: UpdateCharge(); break;
            case BossState.Tackle: UpdateTackle(); break;
            case BossState.Roll: UpdateRoll(); break;
            case BossState.RollEnd: break; // コルーチンで処理
            case BossState.HipDropCharge: UpdateHipDropCharge(); break;
            case BossState.HipDrop: UpdateHipDrop(); break;
            case BossState.Stop: UpdateStop(); break;
            case BossState.Stun: UpdateStun(); break;
            case BossState.Roar: break;
            case BossState.Defeated: break;
        }
    }

    private void PlaySE(SoundEffect se)
    {
        if (audioSource == null || se == null || se.clip == null) return;

        if (se.loop)
        {
            audioSource.clip = se.clip;
            audioSource.volume = se.volume;
            audioSource.loop = true;
            audioSource.Play();
        }
        else
        {
            audioSource.PlayOneShot(se.clip, se.volume);
        }
    }

    private IEnumerator PlaySEDelayed(SoundEffect se, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlaySE(se);
    }

    /// <summary>現在再生中のSEを全て停止する</summary>
    private void StopAllSE()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    /// <summary>状態に応じてエフェクトを切り替える</summary>
    private bool tackleEffectPlaying = false;

    private void UpdateEffects()
    {
        // 撃破演出中は何もしない
        if (state == BossState.Defeated) return;

        // タックルエフェクト
        if (tackleEffect != null)
            tackleEffect.SetActive(state == BossState.Tackle);
        // スタンエフェクト
        if (stunEffect != null)
        {
            // フェーズ3かつ大砲ヒットによるスタンのみ手動制御（CannonMuzzleがEnableStunEffectを呼ぶ）
            if (currentPhase >= 2 && isHitCannon)
            {
                if (state != BossState.Stun && state != BossState.Defeated)
                    stunEffect.SetActive(false);
            }
            else
            {
                // それ以外（フェーズ1・2、またはフェーズ3の木箱スタン）は自動制御
                stunEffect.SetActive(state == BossState.Stun && !isWaitingToRoar);
            }
        }

        // ゴロゴロエフェクト
        if (rollEffect != null)
            rollEffect.SetActive(state == BossState.Roll);

        // チャージエフェクト
        if (chargeEffect != null)
            chargeEffect.SetActive(state == BossState.Charge || isRollWindup);
    }

    private IEnumerator DisableEffectWhenDone(GameObject effect)
    {
        if (effect == null) yield break;

        var ps = effect.GetComponentsInChildren<ParticleSystem>();

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
            effect.SetActive(false);
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
        if (isWaitingToRoar) return;

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

        // フェーズ3はマスク進行ではなく「押す」処理
        if (currentPhase == 2)
        {
            PushBossOnAttack();

            PlaySE(stunHitSE);

            // 被弾エフェクト再生
            if (stunHitEffect != null)
            {
                stunHitEffect.SetActive(true);
                var ps = stunHitEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var p in ps) { p.Clear(); p.Play(); }
                StartCoroutine(DisableEffectWhenDone(stunHitEffect));
            }

            Debug.Log("[Boss_SB] ボスを押した（フェーズ3）");
            return;
        }

        inkHitCount++;
        bool isFinalHit = inkHitCount >= required;

        if (isFinalHit)
        {
            PlaySE(stunHitFinalSE);

            // 最後の一発は大きいエフェクトのみ（stunHitEffectは絶対出さない）
            if (finalHitBigEffect != null)
            {
                finalHitBigEffect.transform.rotation = player.rotation;

                finalHitBigEffect.SetActive(true);
                var ps = finalHitBigEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var p in ps) { p.Clear(); p.Play(); }
                StartCoroutine(DisableEffectWhenDone(finalHitBigEffect));
            }
        }
        else
        {
            PlaySE(stunHitSE);

            // 通常ヒットのみstunHitEffectを再生
            if (stunHitEffect != null)
            {
                stunHitEffect.transform.rotation = player.rotation;

                stunHitEffect.SetActive(true);
                var ps = stunHitEffect.GetComponentsInChildren<ParticleSystem>();
                foreach (var p in ps) { p.Clear(); p.Play(); }
                StartCoroutine(DisableEffectWhenDone(stunHitEffect));
            }
        }

        var maskProgress = GetComponentsInChildren<MaskedInkProgress>();
        foreach (var mask in maskProgress)
            mask.Advance();

        if (resetStunTimerOnHit)
            stunTimer = 0f;

        if (isFinalHit)
        {
            inkHitCount = 0;
            StartCoroutine(EnterRoarWithDelay());
        }
    }
    private IEnumerator DisableEffectWhenDoneAndResetScale(GameObject effect, Vector3 originalScale)
    {
        if (effect == null) yield break;

        var ps = effect.GetComponentsInChildren<ParticleSystem>();

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
        {
            effect.SetActive(false);
            effect.transform.localScale = originalScale; // ★ スケールを元に戻す
        }
    }

    private void PushBossOnAttack()
    {
        if (bossController == null || !bossController.enabled) return;
        if (player == null) return;
        if (isPushing) return; // ★ 既に押し中なら無視（または下記コメントの方式に変更可）

        Vector3 pushDir = (transform.position - player.position).normalized;
        pushDir.y = 0f;

        StartCoroutine(PushBossCoroutine(pushDir));
    }

    private IEnumerator PushBossCoroutine(Vector3 direction)
    {
        isPushing = true;
        float elapsed = 0f;

        while (elapsed < pushDuration)
        {
            if (bossController == null || !bossController.enabled) break;

            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / pushDuration); // ★ 徐々に減速
            float smoothT = t; // 線形でもOK、t*tにすると急減速

            Vector3 move = direction * (pushDistance / pushDuration) * smoothT * Time.deltaTime;
            move.y = -9.8f * Time.deltaTime;

            bossController.Move(move);

            yield return null;
        }

        isPushing = false;
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
        if (currentPhase != 0 && currentPhase != 2) return;
        if (state != BossState.Tackle) return;

        StopAllSE();

        PlaySE(hitCrateSE);

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

        PlaySE(pinballSE);

        // 方向を更新
        rollDirection = newDirection.normalized;
        rollDirection.y = 0f;

        bounceHomingDisableTimer = bounceHomingDisableDuration;

        if (isInner)
        {
            // 内面バウンド → フラグ成立
            bounceCount++;
            Debug.Log($"[Boss_SB] 灯籠内面バウンド！フラグ成立: {bounceCount}/3");

            if (bounceCount >= requiredBounceCount)
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
            state == BossState.HipDropCharge ||
            state == BossState.Defeated ||
            isLaunching ||
            isHitCannon) return;

        if (state == BossState.HipDrop && cannonMuzzleTransform != null)
        {
            float distToCannon = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(cannonMuzzleTransform.position.x, 0, cannonMuzzleTransform.position.z));
            if (distToCannon <= cannonSafeRadius)
            {
                Debug.Log($"[Boss_SB] 大砲近くなので判定スキップ dist={distToCannon}");
                return;
            }
        }

        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;
        Vector3 playerPos = player.position;
        bossPos.y = 0f;
        playerPos.y = 0f;
        float dist = Vector3.Distance(bossPos, playerPos);


        if (dist <= collideDistance)
        {
            if (isHitCannon || isLaunching) return;

            Debug.Log($"[Boss_SB] 衝突発生！bodyTransform.posY={bodyTransform?.position.y} transform.posY={transform.position.y} dist={dist}");
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

        LookAt(toPlayer);

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackDelay)
        {
            // フェーズによって攻撃パターンを切り替える
            if (currentPhase == 0)
            {
                EnterCharge(); // フェーズ1: タックル
            }
            else if (currentPhase == 1)
            {
                EnterRoll();   // フェーズ2: ゴロゴロ
            }
            else
            {
                // フェーズ3: ランダムでタックル or ヒップドロップ
                if (Random.value < hipDropChance)
                    EnterHipDropCharge(); // ヒップドロップ
                else
                    EnterCharge();        // タックル
            }
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

        if (chargeSE?.clip != null)
        {
            audioSource.clip = chargeSE.clip;
            audioSource.volume = chargeSE.volume;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (currentPhase != 1)
        {
            float preDelay = Mathf.Max(0f, chargeDuration - tackleSEPreDelay);
            StartCoroutine(PlaySEDelayed(tackleSE, preDelay));
        }

        Debug.Log("[Boss_SB] チャージ開始！");
    }

    private void UpdateCharge()
    {
        chargeTimer += Time.deltaTime;

        // チャージ中にプレイヤーの方向を追う
        if (chargeHomingSpeed > 0f)
        {
            Vector3 toPlayer = (player.position - transform.position).normalized;
            toPlayer.y = 0f;
            chargeTargetDir = Vector3.RotateTowards(
                chargeTargetDir,
                toPlayer,
                chargeHomingSpeed * Mathf.Deg2Rad * Time.deltaTime,
                0f).normalized;

            if (attackIndicator != null)
                attackIndicator.UpdateDirection(transform.position, chargeTargetDir);
        }

        LookAt(chargeTargetDir);

        if (chargeTimer >= chargeDuration)
            EnterTackle();
    }

    // ====================================================================
    //  Tackle（タックル・フェーズ1）
    // ====================================================================

    private void EnterTackle()
    {
        //if (audioSource.isPlaying && audioSource.loop)
        //{
        //    audioSource.loop = false;
        //    audioSource.Stop();
        //}

        prevState = state;
        state = BossState.Tackle;

        tackleDirection = chargeTargetDir;
        tackleStartPos = transform.position;

        if (attackIndicator != null)
            attackIndicator.Hide();
        Debug.Log("[Boss_SB] タックル開始！");
    }

    private IEnumerator PlaySEDelayed(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
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
            if (currentPhase == 2)
                EnterHipDropCharge();
            else
                EnterStop();
            return;
        }

        foreach (var crate in crateObjects)
        {
            if (crate == null || !crate.activeSelf) continue;
            float dist = Vector3.Distance(
                transform.position,
                crate.transform.position);

            Debug.Log($"[Boss_SB] crate={crate.name} dist={dist}");
            if (dist < 3.5f)
            {
                var woodBox = crate.GetComponentsInChildren<Boss_WoodBox>();
                if (woodBox != null)
                {
                    int inkLayer = LayerMask.NameToLayer("PlayerVSObject");
                    if (crate.layer == inkLayer)
                        NotifyHitCrate();
                    else
                        NotifyHitCrateNoStun();
                    return;
                }
            }
        }

        float travelDist = Vector3.Distance(transform.position, tackleStartPos);
        if (travelDist > tackleMaxDistance)
        {
            // フェーズ3なら直接ヒップドロップ
            if (currentPhase == 2)
                EnterHipDropCharge();
            else
                EnterStop();
        }
    }

    /// <summary>お墨付き大砲にタックルで当たったときに呼ぶ</summary>
    public void NotifyHitCannonTackle(CannonAutoAim cannonAutoAim, Obj_Osumitsuki cannonOsumi, Cannon_Osumitsuki cannon)
    {
        if (state != BossState.Tackle) return;

        StopAllSE();

        Debug.Log("[Boss_SB] お墨付き大砲に衝突！");

        PlaySE(pinballSE);

        // 大砲リセット
        if (cannonAutoAim != null)
            cannonAutoAim.ResetCannon();

        // PaintableSurfaceの塗りもリセット
        var surfaces = cannonOsumi.GetComponentsInChildren<PaintableSurface>();
        foreach (var surface in surfaces)
        {
            if (surface != null)
                surface.ClearAll();
        }

        // レイヤーも元に戻す
        cannonOsumi.gameObject.layer = LayerMask.NameToLayer("SumiVSObject");
        var children = cannonOsumi.GetComponentsInChildren<Transform>();
        foreach (var child in children)
            child.gameObject.layer = LayerMask.NameToLayer("SumiVSObject");

        var cannonSetup = cannonOsumi.GetComponent<CannonSetup>();
        if (cannonSetup == null)
            cannonSetup = cannonOsumi.GetComponentInParent<CannonSetup>();
        if (cannonSetup != null)
            cannonSetup.ResetMaterials();

        var changeFlgField = typeof(Cannon_Osumitsuki).GetField(
    "changeFlg",
    System.Reflection.BindingFlags.NonPublic |
    System.Reflection.BindingFlags.Instance);

        changeFlgField?.SetValue(cannon, false);

        var endFlgField = typeof(Obj_Osumitsuki).GetField(
    "endFlg",
    System.Reflection.BindingFlags.NonPublic |
    System.Reflection.BindingFlags.Instance);
        endFlgField?.SetValue(cannonOsumi, false);

        var mng = Mng_Osumitsuki.instance;
        if (mng != null)
        {
            var actionField = typeof(Mng_Osumitsuki).GetField(
                "action_Objects",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var updateField = typeof(Mng_Osumitsuki).GetField(
                "update_Objects",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            var actionList = actionField?.GetValue(mng) as List<Obj_Osumitsuki>;
            var updateList = updateField?.GetValue(mng) as List<Obj_Osumitsuki>;

            actionList?.Remove(cannonOsumi);
            updateList?.Remove(cannonOsumi);
        }

        // お墨付き状態をReflectionでリセット
        if (cannonOsumi != null)
        {
            var trgField = typeof(Obj_Osumitsuki).GetField(
                "osumitsukiTrg",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var flgField = typeof(Obj_Osumitsuki).GetField(
                "osumitsukiFlg",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            var inkField = typeof(Obj_Osumitsuki).GetField(
                "curInkAmount",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            trgField?.SetValue(cannonOsumi, false);
            flgField?.SetValue(cannonOsumi, false);
            inkField?.SetValue(cannonOsumi, 0f);
        }

        StartCoroutine(CannonBounceCoroutine());
    }

    private IEnumerator CannonBounceCoroutine()
    {
        state = BossState.Stop;

        // 後ろにバウンド（仰向けにならない）
        Vector3 bounceDir = -tackleDirection;
        float elapsed = 0f;

        while (elapsed < cannonBounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / cannonBounceDuration);
            float smoothT = t * t;

            Vector3 move = bounceDir * cannonBounceSpeed * smoothT * Time.deltaTime;
            move.y = -9.8f * Time.deltaTime;
            bossController.Move(move);

            yield return null;
        }

        // ヒップドロップはしない→Chaseへ
        EnterChase();
    }

    // ====================================================================
    //  Roll（ゴロゴロ・フェーズ2）
    //  ゴロゴロの挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterRoll()
    {
        state = BossState.Roll;
        rollTimer = 0f;
        rollXRotation = 0f;
        rollDirection = (player.position - transform.position).normalized;
        rollDirection.y = 0f;
        StartRollLoopSE();
        StartCoroutine(RollWindupCoroutine());
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

        // 移動
        Vector3 move = rollDirection * rollSpeed * Time.deltaTime;
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
    private void StartRollLoopSE()
    {
        if (rollSE?.clip == null) return;
        rollLoopCoroutine = StartCoroutine(RollLoopCoroutine());
    }

    private void StopRollLoopSE()
    {
        if (rollLoopCoroutine != null)
        {
            StopCoroutine(rollLoopCoroutine);
            rollLoopCoroutine = null;
        }
        if (rollAudioSourceA != null) rollAudioSourceA.Stop();
        if (rollAudioSourceB != null) rollAudioSourceB.Stop();
    }

    private IEnumerator RollLoopCoroutine()
    {
        AudioSource current = rollAudioSourceA;
        AudioSource next = rollAudioSourceB;

        float clipLength = rollSE.clip.length;

        current.clip = rollSE.clip;
        current.volume = rollSE.volume;
        current.Play();

        while (true)
        {
            // クリップ終了の少し前に、次のAudioSourceで再生開始（重ねる）
            yield return new WaitForSeconds(clipLength - rollCrossfadeOverlap);

            next.clip = rollSE.clip;
            next.volume = rollSE.volume;
            next.Play();

            // 入れ替え
            var temp = current;
            current = next;
            next = temp;
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
        StopRollLoopSE();

        state = BossState.RollEnd;

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

    private IEnumerator RollWindupCoroutine()
    {
        BossState prevState = state;
        state = BossState.Stop;
        isRollWindup = true; // 開始

        Quaternion startRot = transform.rotation;
        Quaternion tiltRot = startRot * Quaternion.Euler(rollWindupTiltAngle, 0f, 0f);
        float elapsed = 0f;

        while (elapsed < rollWindupTiltDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rollWindupTiltDuration;
            transform.rotation = Quaternion.Slerp(startRot, tiltRot, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < rollWindupReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rollWindupReturnDuration;
            transform.rotation = Quaternion.Slerp(tiltRot, startRot, t);
            yield return null;
        }

        transform.rotation = startRot;
        isRollWindup = false; // 終了

        state = BossState.Roll;
    }

    /// <summary>動的生成された灯籠を登録する（LanternSetSetupから呼ぶ）</summary>
    public void RegisterLanternObject(GameObject lantern)
    {
        if (!lanternObjects.Contains(lantern))
            lanternObjects.Add(lantern);
        Debug.Log($"[Boss_SB] 灯籠登録: {lantern.name} 合計{lanternObjects.Count}個");
    }

    // ====================================================================
    //  HipDrop（ヒップドロップ・フェーズ3）
    // ====================================================================

    private void EnterHipDropCharge()
    {
        state = BossState.HipDropCharge;
        hipDropChargeTimer = 0f;
        hipDropCurrentCount = 0;
        isHitCannon = false;

        if (hipDropIndicator != null)
            hipDropIndicator.Show(new Vector3(
                transform.position.x, 0f, transform.position.z));

        Debug.Log("[Boss_SB] ヒップドロップ開始！");
        StartCoroutine(HipDropSequence());
    }

    /// <summary>ヒップドロップを連続で行うコルーチン</summary>
    private IEnumerator HipDropSequence()
    {
        for (int i = 0; i < hipDropCount; i++)
        {

            if (isHitCannon)
            {
                EnterStun();
                yield break;
            }

            hipDropCurrentCount = i;

            // ジャンプ＋チャージ（ホーミング）
            yield return StartCoroutine(HipDropChargeCoroutine());

            if (isHitCannon)
            {
                EnterStun();
                yield break;
            }

            // 降下
            yield return StartCoroutine(HipDropFallCoroutine());

            // 砲口に当たったらスタンへ（中断）
            if (isHitCannon)
            {
                Debug.Log("[Boss_SB] 砲口ヒット！スタンへ");
                EnterStun();
                yield break;
            }

            // 着地後の硬直
            yield return new WaitForSeconds(hipDropRecoverTime);
        }

        // 3回終わったら停止→移動
        EnterStop();
    }

    /// <summary>ジャンプして空中でホーミングしながらチャージ</summary>
    private IEnumerator HipDropChargeCoroutine()
    {
        state = BossState.HipDropCharge;
        Debug.Log($"[Boss_SB] HipDropCharge開始 isHitCannon={isHitCannon}");
        Vector3 startPos = transform.position;
        Vector3 apexPos = startPos + Vector3.up * hipDropJumpHeight;

        PlaySE(jumpSE);

        float groundY = groundTransform != null ? groundTransform.position.y : 0f;

        // ジャンプ開始と同時にサークル表示
        if (hipDropIndicator != null)
            hipDropIndicator.Show(new Vector3(
                transform.position.x, groundY, transform.position.z));

        // ジャンプ（上昇）
        float jumpTime = 0.3f;
        float elapsed = 0f;
        while (elapsed < jumpTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jumpTime;
            transform.position = Vector3.Lerp(startPos, apexPos, t);

            // ジャンプ中も追従
            if (hipDropIndicator != null)
                hipDropIndicator.Show(new Vector3(
                    transform.position.x, groundY, transform.position.z));

            yield return null;
        }

        if (attackIndicator != null)
            attackIndicator.ShowAt(transform.position, Vector3.down);

        hipDropChargeTimer = 0f;
        while (hipDropChargeTimer < hipDropChargeDuration)
        {
            hipDropChargeTimer += Time.deltaTime;

            float homingEndTime = hipDropChargeDuration * 0.5f;
            if (hipDropChargeTimer < homingEndTime)
            {
                Vector3 targetXZ = new Vector3(player.position.x, transform.position.y, player.position.z);
                transform.position = Vector3.MoveTowards(
                    transform.position, targetXZ, hipDropHomingSpeed * Time.deltaTime);
            }

            Vector3 lookDir = player.position - transform.position;
            lookDir.y = 0f;
            LookAt(lookDir);

            // ボスの真下に追従
            if (hipDropIndicator != null)
                hipDropIndicator.Show(new Vector3(
                    transform.position.x, groundY, transform.position.z));

            yield return null;
        }

        // 降下先を確定
        hipDropTargetPos = transform.position;

        // 確定後も固定
        if (hipDropIndicator != null)
            hipDropIndicator.Show(new Vector3(
                hipDropTargetPos.x, groundY, hipDropTargetPos.z));

        if (attackIndicator != null)
            attackIndicator.Hide();

        yield return new WaitForSeconds(hipDropDelayBeforeFall);
    }

    /// <summary>降下する（ホーミングなし）</summary>
    private IEnumerator HipDropFallCoroutine()
    {
        state = BossState.HipDrop;

        while (true)
        {
            if (!bossController.enabled)
            {
                Debug.Log($"[Boss_SB] HipDropFall終了(controller無効) posY={transform.position.y}");
                yield break;
            }
            if (isLaunching)
            {
                Debug.Log($"[Boss_SB] HipDropFall終了(isLaunching) posY={transform.position.y}");
                yield break;
            }
            if (isHitCannon)
            {
                Debug.Log($"[Boss_SB] HipDropFall終了(isHitCannon) posY={transform.position.y}");
                yield break;
            }

            Vector3 move = Vector3.down * hipDropFallSpeed * Time.deltaTime;
            bossController.Move(move);

            if (bossController.isGrounded)
            {
                Debug.Log("[Boss_SB] ヒップドロップ着地");
                if (hipDropIndicator != null)
                    hipDropIndicator.Hide();
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>ヒップドロップインジケーターを非表示にする（CannonMuzzleから呼ぶ）</summary>
    public void HideHipDropIndicator()
    {
        if (hipDropIndicator != null)
            hipDropIndicator.Hide();
    }

    /// <summary>UpdateHipDropChargeはコルーチンで処理するため空</summary>
    private void UpdateHipDropCharge() { }

    /// <summary>UpdateHipDropはコルーチンで処理するため空</summary>
    private void UpdateHipDrop() { }

    /// <summary>
    /// 大砲の砲口に当たったときに呼ぶ（フェーズ3・CannonMuzzleから呼ぶ）
    /// ヒップドロップ中のみ有効
    public void NotifyHitCannon()
    {
        if (currentPhase != 2) return;
        if (state != BossState.HipDrop && state != BossState.Stun) return;

        PlaySE(cannonEnterSE);

        Debug.Log($"[Boss_SB] 砲口に当たった！ bodyTransform.posY={bodyTransform?.position.y} transform.posY={transform.position.y}");
        isHitCannon = true;

        stunTimer = 0f;

        if (bossController != null)
            bossController.enabled = false;
    }

    /// <summary>打ち上げ中フラグのセット（CannonMuzzleから呼ぶ）</summary>
    public void SetLaunching(bool value)
    {
        isLaunching = value;
    }

    // ====================================================================
    //  Stop（停止）
    // ====================================================================

    private void EnterStop()
    {
        prevState = state;
        state = BossState.Stop;
        stopTimer = 0f;

        if (currentPhase == 2 && prevState == BossState.Tackle)
            pendingHipDrop = true;

        Debug.Log("[Boss_SB] 停止");
    }

    private void UpdateStop()
    {
        stopTimer += Time.deltaTime;
        if (stopTimer >= stopDuration)
        {
            // ヒップドロップ待機中なら直接ヒップドロップへ
            if (pendingHipDrop)
            {
                pendingHipDrop = false;
                EnterHipDropCharge();
            }
            else
            {
                EnterChase();
            }
        }
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

        // フェーズ3はCannonMuzzleで向きを設定するのでSkip
        if (currentPhase != 2 || !isHitCannon)
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

            if (stunEffect != null)
                stunEffect.SetActive(false);

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

        StopAllSE();

        Debug.Log("[Boss_SB] 木箱に当たったが塗り量不足。停止のみ");
        if (currentPhase == 2)
            EnterHipDropCharge();
        else
            EnterStop();
    }

    /// <summary>スタンエフェクトを有効化する（CannonMuzzleから呼ぶ）</summary>
    public void EnableStunEffect()
    {
        if (stunEffect != null)
        {
            if (currentPhase == 2) // ==2のみフェーズ3の向き
            {
                stunEffect.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                stunEffect.transform.localPosition = new Vector3(
                    stunEffect.transform.localPosition.x,
                    stunEffect.transform.localPosition.y,
                    stunEffectPhase3Z);
            }
            else if (currentPhase == 3) // ==2のみフェーズ3の向き
            {
                stunEffect.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                stunEffect.transform.localPosition = new Vector3(
                    stunEffect.transform.localPosition.x,
                    stunEffect.transform.localPosition.y,
                    stunEffectPhase3Z + 2.0f);
            }
            else // ★ フェーズ1・2・撃破時は通常の向き
            {
                stunEffect.transform.localRotation = Quaternion.identity;
                stunEffect.transform.localPosition = Vector3.zero;
            }

            stunEffect.SetActive(true);
        }
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

    private IEnumerator EnterRoarWithDelay()
    {
        isWaitingToRoar = true; // フラグON

        if (stunEffect != null)
            stunEffect.SetActive(false);

        if (stunHitEffect != null)
            stunHitEffect.SetActive(false);

        yield return new WaitForSeconds(defeatStunLingerTime);

        isWaitingToRoar = false; // フラグOFF
        EnterRoar();
    }

    private IEnumerator PlayRoarEffect()
    {
        Debug.Log($"[Boss_SB] PlayRoarEffect開始 phase={currentPhase}");
        roarEffect.SetActive(true);
        yield return null;

        Debug.Log($"[Boss_SB] roarEffect activeInHierarchy={roarEffect.activeInHierarchy}");

        var ps = roarEffect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in ps)
        {
            p.Clear();
            p.Play(true);
            Debug.Log($"[Boss_SB] Play: {p.gameObject.name} isPlaying={p.isPlaying}");
        }
        StartCoroutine(DisableEffectWhenDone(roarEffect));
    }

    private IEnumerator RoarCoroutine()
    {
        yield return StartCoroutine(StandUpCoroutine());

        if (fieldCenter != null)
            yield return StartCoroutine(JumpToCenter());

        if (roarEffect != null)
            StartCoroutine(PlayRoarEffect());

        PlaySE(roarSE);

        yield return new WaitForSeconds(0.5f);

        // ボス自身の墨をリセット
        foreach (var surface in bossSurfaces)
        {
            if (surface == null || !surface.enabled) continue;
            try { InkPaintService.ClearAll(surface); } catch { }
        }

        // マスクをリセット
        var maskProgress = GetComponentsInChildren<MaskedInkProgress>();
        foreach (var mask in maskProgress)
            mask.ResetProgress();

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
            if (lanternSet != null)
                lanternSet.ResetLanterns();

            Debug.Log($"[Boss_SB] 灯籠を飛ばす: {lanternObjects.Count}個");
            BlastObjects(lanternObjects, lanternBlastForce, lanternDestroyDelay);
            lanternObjects.Clear();

            // 大砲を生成
            yield return StartCoroutine(SpawnObjectsFromAbove(
                phase3ObjectPrefab, phase3SpawnPoints));

            // 木箱も生成
            yield return StartCoroutine(SpawnObjectsFromAbove(
                phase3CratePrefab, phase3CrateSpawnPoints));
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

        if (currentPhase >= 3)
            EnterDefeated();
    }

    /// <summary>オブジェクトを上から順番に落とすコルーチン</summary>
    private IEnumerator SpawnObjectsFromAbove(
GameObject prefab, List<Transform> spawnPoints)
    {
        if (prefab == null || spawnPoints == null) yield break;

        foreach (var point in spawnPoints)
        {
            if (point == null) continue;

            Vector3 spawnPos = new Vector3(
                point.position.x,
                point.position.y + spawnHeight,
                point.position.z);

            GameObject obj = Instantiate(
                        prefab,
                        spawnPos,
                        currentPhase == 1
                        ? Quaternion.Euler(phase3ObjectRotation)
                        : Quaternion.identity);

            if (currentPhase == 0)
                RegisterLanternObject(obj);

            // フェーズ3木箱の場合はcrateObjectsに登録
            if (prefab == phase3CratePrefab)
            {
                crateObjects.Add(obj);
                Debug.Log($"[Boss_SB] フェーズ3木箱登録: {obj.name}");
            }

            var muzzle = obj.GetComponentInChildren<CannonMuzzle>(true);
            if (muzzle != null)
            {
                cannonMuzzleTransform = muzzle.transform;
                Debug.Log($"[Boss_SB] CannonMuzzle取得成功: {muzzle.transform.position}");
            }

            var rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rigidbodies)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            StartCoroutine(FreezeAfterLanding(obj, point.position));

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>着地後にRigidbodyをKinematicに固定する</summary>
    private IEnumerator FreezeAfterLanding(GameObject obj, Vector3 landPos)
    {
        if (obj == null) yield break;

        var rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
        Rigidbody mainRb = rigidbodies.Length > 0 ? rigidbodies[0] : null;

        if (mainRb == null) yield break;

        // 着地を待つ（速度がほぼゼロになるまで待つ）
        float timeout = 2.1f; // 最大10秒待つ
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            // 速度がほぼゼロ = 着地した
            if (mainRb.linearVelocity.magnitude < 0.1f && elapsed > 0.5f)
            {
                Debug.Log($"[Boss_SB] 着地検知！velocity={mainRb.linearVelocity.magnitude}");
                break;
            }

            yield return null;
        }

        if (obj == null) yield break;

        // 着地位置のY座標を取得
        float groundY = obj.transform.position.y;
        if (Physics.Raycast(
            obj.transform.position + Vector3.up * 1f,
            Vector3.down,
            out RaycastHit hit,
            5f,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
        }

        // Rigidbodyを固定
        foreach (var rb in rigidbodies)
            rb.isKinematic = true;

        // 地面からburiedDepth分だけ下に固定
        obj.transform.position = new Vector3(
            obj.transform.position.x,
            groundY - buriedDepth,
            obj.transform.position.z);

        Debug.Log($"[Boss_SB] 灯籠固定: {obj.transform.position}");
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

            // ★ 子オブジェクト全部のRigidbodyを取得して飛ばす
            var rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
            if (rigidbodies.Length > 0)
            {
                foreach (var rb in rigidbodies)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.AddForce(blastForce, ForceMode.Impulse);
                    Debug.Log($"[Boss_SB] 灯籠飛ばし: {rb.gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[Boss_SB] Rigidbodyなし: {obj.name}");
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
        StartCoroutine(DefeatedCoroutine());
    }

    private IEnumerator DefeatedCoroutine()
    {
        Debug.Log("[Boss_SB] 撃破演出開始");

        // うつ伏せになりきってから
        yield return StartCoroutine(StunTiltCoroutine());
        Debug.Log("[Boss_SB] うつ伏せ完了");

        // うつ伏せ完了後にスタンエフェクト表示
        yield return new WaitForSeconds(defeatStunEffectDelay);
        EnableStunEffect();

        // お墨付き完了エフェクト
        if (defeatEffect != null)
        {
            defeatEffect.SetActive(true);
            var ps = defeatEffect.GetComponentsInChildren<ParticleSystem>();
            foreach (var p in ps) p.Play();
            StartCoroutine(DisableEffectWhenDone(defeatEffect));
        }

        yield return new WaitForSeconds(defeatStunDuration);

        Debug.Log("[Boss_SB] 撃破！");
        //Destroy(gameObject);
    }

    // ====================================================================
    //  ノックバック
    // ====================================================================

    private void ApplyKnockbackToPlayer()
    {
        if (playerMove == null) return;

        Debug.Log($"[Boss_SB] ApplyKnockback hitEffectPlayer={hitEffectPlayer}");

        if (hitEffectPlayer != null)
            hitEffectPlayer.PlayHitEffect();

        float knockbackPower = attackPowers[currentPhase] * 0.5f;

        Vector3 knockDir;

        // HipDrop中は真上から落ちるのでプレイヤーの向きの逆に飛ばす
        if (state == BossState.HipDrop)
        {
            knockDir = -player.forward;
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude < 0.01f)
                knockDir = Vector3.back;
            knockDir.Normalize();
        }
        else
        {
            knockDir = player.position - transform.position;
            knockDir.y = 0f;
            knockDir.Normalize();
        }

        Vector3 knockbackVelocity = knockDir * knockbackPower * 10f
                                  + Vector3.up * knockbackUpForce;

        playerMove.ApplyKnockback(knockbackVelocity, knockbackDuration);

        if (playerStats != null)
            playerStats.Damage(1);
    }

    // ====================================================================
    //  エリア制限
    // ====================================================================

    private void ClampToArea()
    {
        if (areaPointA == null || areaPointB == null) return;
        if (isLaunching) return;

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
        if (isLaunching) return false;
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

        Vector3 recoilDir;
        if (state == BossState.Roll)
            recoilDir = -rollDirection;
        else if (state == BossState.HipDrop)
            recoilDir = -transform.forward;
        else
            recoilDir = -tackleDirection;

        while (elapsed < recoilDuration)
        {
            // コントローラーが無効ならMoveしない
            if (bossController == null || !bossController.enabled)
            {
                yield return null;
                continue;
            }

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