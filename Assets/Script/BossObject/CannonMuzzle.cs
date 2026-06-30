using UnityEngine;
using System.Collections;

/// <summary>
/// 大砲の砲口判定スクリプト
/// ボスのヒップドロップが砲口に当たったを検知し、打ち上げる
/// </summary>
public class CannonMuzzle : MonoBehaviour
{
    [SerializeField] private Boss_SB boss;
    [SerializeField] private CannonAutoAim cannonAutoAim;


    [Header("── 打ち上げ ──")]
    [Tooltip("ボスが詰まってから打ち上げるまでの秒数")]
    [SerializeField] private float launchDelay = 1.5f;
    [Tooltip("打ち上げの強さ")]
    [SerializeField] private float launchForce = 20f;
    [Tooltip("打ち上げ時の前方向の力")]
    [SerializeField] private float launchForwardForce = 5f;

    [Header("── 打ち上げエフェクト ──")]
    [SerializeField] private GameObject launchEffect;

    [Header("── 着地 ──")]
    [Tooltip("地面のTransform（この位置のY座標を着地点にする）")]
    [SerializeField] private Transform groundTransform;
    [Tooltip("地面からのオフセット（めり込み調整用）")]
    [SerializeField] private float groundOffset = 0.5f;
    [Tooltip("打ち上げ時の落下速度を上げる力")]
    [SerializeField] private float extraGravity = 20f;
    [Tooltip("うつ伏せになるまでの時間（秒）")]
    [SerializeField] private float alignTime = 0.5f;
    [Tooltip("打ち上げ中の回転速度（度/秒）")]
    [SerializeField] private float rotateSpeed = 180f;
    [Tooltip("着地後の転がり速度")]
    [SerializeField] private float landingRollSpeed = 5f;
    [Tooltip("着地後の転がり時間（秒）")]
    [SerializeField] private float landingRollDuration = 0.5f;
    [Tooltip("着地後の回転速度（度/秒）")]
    [SerializeField] private float landingRotateSpeed = 720f;

    [Header("── カメラ ──")]
    [SerializeField] private ThirdPersonOrbitCamera cameraController;
    [Header("── プレイヤー制御 ──")]
    [SerializeField] private PlayerMove playerMove;

    [Header("── 扉判定（横方向発射時） ──")]
    [Tooltip("ゴール側の扉などの判定用オブジェクト（未設定ならタグ/名前で自動取得）")]
    [SerializeField] private Transform goalDoorTransform;
    [SerializeField] private string goalDoorTag = "GoalDoor";
    [Tooltip("Boss_Doorのradiusと同じ値を入れる")]
    [SerializeField] private float doorHitDistance = 2f; 
    [Tooltip("Boss_Doorのoffsetと同じ値を入れる")]
    [SerializeField] private Vector3 doorOffset = Vector3.zero;
    [SerializeField] private GameObject doorHitBigEffect;
    [Tooltip("扉に当たってからボスが消えるまでの時間（秒）")]
    [SerializeField] private float disappearDelay = 1f;

    [Header("── SE ──")]
    [SerializeField] private AudioClip cannonLaunchSE; // 大砲射出（1）.mp3


    private Collider muzzleCollider;
    private bool hasLaunched = false;
    private bool hasHitDoor = false;

    private int originalBossLayer;

    private void Start()
    {
        if (boss == null)
            boss = FindObjectOfType<Boss_SB>();

        if (cameraController == null)
            cameraController = FindObjectOfType<ThirdPersonOrbitCamera>();

        // 扉を自動取得（タグで検索）
        if (goalDoorTransform == null)
        {
            var doorObj = GameObject.FindGameObjectWithTag(goalDoorTag);
            if (doorObj != null)
            {
                goalDoorTransform = doorObj.transform;
                Debug.Log($"[CannonMuzzle] 扉自動取得: {doorObj.name}");
            }
            else
            {
                Debug.LogWarning($"[CannonMuzzle] タグ'{goalDoorTag}'の扉が見つかりません");
            }
        }

        // playerMoveを自動取得
        if (playerMove == null)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            var allObjects = FindObjectsOfType<PlayerMove>();
            foreach (var pm in allObjects)
            {
                if (pm.gameObject.layer == playerLayer)
                {
                    playerMove = pm;
                    break;
                }
            }
        }

        if (cameraController == null)
            cameraController = FindObjectOfType<ThirdPersonOrbitCamera>();

        if (groundTransform == null)
        {
            // レイヤー名で取得
            int groundLayer = LayerMask.NameToLayer("BossArea"); // レイヤー名を入れる
            var allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.layer == groundLayer)
                {
                    groundTransform = obj.transform;
                    Debug.Log($"[CannonMuzzle] 地面取得: {obj.name}");
                    break;
                }
            }
        }

        // それでもなければbossの位置のY=0を使う
        if (groundTransform == null)
            Debug.LogWarning("[CannonMuzzle] groundTransformが未設定！Y=0を使います");

        muzzleCollider = GetComponent<Collider>();
        if (muzzleCollider == null)
        {
            Debug.LogError($"[CannonMuzzle] {gameObject.name}: Collider が必要です");
            enabled = false;
        }

        if (muzzleCollider != null && !muzzleCollider.isTrigger)
            Debug.LogWarning($"[CannonMuzzle] {gameObject.name}: Is Trigger を ON にしてください");

        if (playerMove == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
                playerMove = go.GetComponent<PlayerMove>();
        }
    }

    private void PlaySE(AudioClip clip)
    {
        if (boss == null) return;
        var audioSource = boss.GetComponent<AudioSource>();
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (boss == null) return;
        if (hasLaunched) return;

        if (boss.GetCurrentPhase() != 2) return;

        // ヒップドロップ中 または フェーズ3木箱スタン中
        bool isHipDrop = boss.GetIsHipDropping();
        bool isBoxStun = boss.GetIsPhase3BoxStun();
        if (!isHipDrop && !isBoxStun) return;

        if (cannonAutoAim != null && !cannonAutoAim.IsAimedUp()) return;

        hasLaunched = true;
        Debug.Log("[CannonMuzzle] ボスが砲口に当たった！");
        boss.NotifyHitCannon();

        StartCoroutine(LaunchCoroutine());
    }

    private IEnumerator LaunchCoroutine()
    {
        originalBossLayer = boss.gameObject.layer;
        Debug.Log($"[CannonMuzzle] レイヤー変更前: {LayerMask.LayerToName(originalBossLayer)}");

        int projectileLayer = LayerMask.NameToLayer("BossProjectile");
        if (projectileLayer >= 0)
        {
            boss.gameObject.layer = projectileLayer;
            Debug.Log($"[CannonMuzzle] レイヤー変更後: {LayerMask.LayerToName(boss.gameObject.layer)}");
        }

        var children = boss.GetComponentsInChildren<Transform>();
        foreach (var child in children)
            child.gameObject.layer = projectileLayer;

        var bossCC = boss.GetComponent<CharacterController>();
        if (bossCC != null) bossCC.enabled = false;

        boss.SetLaunching(true);
        boss.transform.position = transform.position;

        boss.HideHipDropIndicator();

        if (cameraController != null)
            cameraController.SetLookTarget(boss.transform);

        Vector3 cannonForward = transform.forward;
        cannonForward.y = 0f;
        cannonForward.Normalize();

        yield return new WaitForSeconds(launchDelay);
        if (boss == null) yield break;

        var bossRb = boss.gameObject.AddComponent<Rigidbody>();
        bossRb.isKinematic = false;
        bossRb.useGravity = true;
        bossRb.freezeRotation = true;

        Vector3 launchVec = Vector3.up * launchForce
                          + cannonForward * launchForwardForce;
        bossRb.linearVelocity = launchVec;

        PlaySE(cannonLaunchSE);

        // 実際の発射方向を基準に回転軸を計算
        Vector3 launchDir = launchVec.normalized;
        Vector3 rotAxis = Vector3.Cross(Vector3.up, launchDir).normalized;
        if (rotAxis.sqrMagnitude < 0.001f)
            rotAxis = Vector3.Cross(Vector3.up, cannonForward).normalized;

        // うつ伏せの最終角度を、発射前のY軸角度で固定（回転中に変わらないように）
        float fixedYaw = boss.transform.eulerAngles.y;
        Quaternion finalRot = Quaternion.Euler(90f, fixedYaw, 0f);

        if (launchEffect != null)
        {
            launchEffect.SetActive(true);
            var ps = launchEffect.GetComponentsInChildren<ParticleSystem>();
            foreach (var p in ps) { p.Clear(); p.Play(); }
        }

        float timeout = 10f;
        float elapsed = 0f;
        float landedY = transform.position.y;

        float landY = groundTransform != null
            ? groundTransform.position.y
            : 0f;

        // 待機なし、発射直後から回転開始
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            bossRb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);

            boss.transform.Rotate(
                rotAxis,
                rotateSpeed * Time.deltaTime,
                Space.World);

            // 扉との距離をチェック
            if (!hasHitDoor && goalDoorTransform != null)
            {
                Vector3 doorCenter = goalDoorTransform.position + doorOffset;
                float distToDoor = Vector3.Distance(boss.transform.position, doorCenter);
                Debug.Log($"[CannonMuzzle] distToDoor={distToDoor}");
                if (distToDoor <= doorHitDistance)
                {
                    hasHitDoor = true;
                    OnHitDoor();
                }
            }

            //if (boss.transform.position.y <= landY)
            //{
            //    landedY = landY;
            //    Debug.Log($"[CannonMuzzle] ボス着地！posY={landedY}");
            //    break;
            //}

            yield return null;
        }

        if (bossRb != null) Destroy(bossRb);
        if (bossCC != null) bossCC.enabled = true;

        boss.transform.position = new Vector3(
            boss.transform.position.x,
            landedY,
            boss.transform.position.z);

        yield return StartCoroutine(LandingRollCoroutine(bossCC, launchDir, rotAxis));

        // fixedYawを使った最終角度に向かう
        float alignElapsed = 0f;

        while (alignElapsed < alignTime)
        {
            alignElapsed += Time.deltaTime;
            float t = alignElapsed / alignTime;
            float speedRate = 1f - t;

            boss.transform.rotation = Quaternion.RotateTowards(
                boss.transform.rotation,
                finalRot,
                rotateSpeed * speedRate * Time.deltaTime);

            yield return null;
        }

        boss.transform.rotation = finalRot;
        boss.SetLaunching(false);

        boss.EnableStunEffect();

        hasLaunched = false;

        if (cameraController != null)
            cameraController.SetLookTarget(null);
    }

    private IEnumerator LandingRollCoroutine(CharacterController bossCC, Vector3 rollDir, Vector3 rotAxis)
    {
        float elapsed = 0f;
        Quaternion targetRot = Quaternion.Euler(90f, boss.transform.eulerAngles.y, 0f);

        while (elapsed < landingRollDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / landingRollDuration;
            float speedRate = 1f - t;

            // 移動（徐々に減速）
            Vector3 move = rollDir * landingRollSpeed * speedRate * Time.deltaTime;
            move.y = -9.8f * Time.deltaTime;
            if (bossCC != null && bossCC.enabled)
                bossCC.Move(move);

            // 回転（空中と同じ軸・徐々に減速しながらうつ伏せへ）
            boss.transform.rotation = Quaternion.RotateTowards(
                boss.transform.rotation,
                targetRot,
                rotateSpeed * speedRate * Time.deltaTime);

            yield return null;
        }

        boss.transform.rotation = targetRot;
    }

    /// <summary>扉に当たったときの処理（大きいエフェクト再生）</summary>
    private void OnHitDoor()
    {
        Debug.Log("[CannonMuzzle] 扉に当たった！");

        if (doorHitBigEffect != null)
        {
            doorHitBigEffect.transform.position = boss.transform.position;

            doorHitBigEffect.SetActive(true);
            var ps = doorHitBigEffect.GetComponentsInChildren<ParticleSystem>();
            foreach (var p in ps) { p.Clear(); p.Play(); }
            StartCoroutine(DisableDoorEffectWhenDone());
        }

        StartCoroutine(MakeBossDisappear());
    }

    private IEnumerator DisableDoorEffectWhenDone()
    {
        if (doorHitBigEffect == null) yield break;

        var ps = doorHitBigEffect.GetComponentsInChildren<ParticleSystem>();

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

        if (doorHitBigEffect != null)
            doorHitBigEffect.SetActive(false);
    }

    private IEnumerator MakeBossDisappear()
    {
        yield return new WaitForSeconds(disappearDelay);

        if (boss != null)
        {
            Debug.Log("[CannonMuzzle] ボスが扉の奥へ消えた");
            boss.gameObject.SetActive(false); // 非表示にする（Destroyではなく安全のためSetActive）
        }

        if (cameraController != null)
            cameraController.SetLookTarget(null);
    }

}

