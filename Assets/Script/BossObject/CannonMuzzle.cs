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


    private Collider muzzleCollider;
    private bool hasLaunched = false;

    private void Start()
    {
        if (boss == null)
            boss = FindObjectOfType<Boss_SB>();

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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (boss == null) return;
        if (hasLaunched) return;

        if (boss.GetCurrentPhase() != 2) return;
        if (!boss.GetIsHipDropping()) return;

        // 砲身が上を向いていないときは判定しない
        if (cannonAutoAim != null && !cannonAutoAim.IsAimedUp()) return;

        hasLaunched = true;
        Debug.Log("[CannonMuzzle] ボスが砲口に当たった！");
        boss.NotifyHitCannon();

        StartCoroutine(LaunchCoroutine());
    }

    private IEnumerator LaunchCoroutine()
    {
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

        Vector3 rotAxis = Vector3.Cross(Vector3.up, cannonForward).normalized;

        yield return new WaitForSeconds(launchDelay);
        if (boss == null) yield break;

        var bossRb = boss.gameObject.AddComponent<Rigidbody>();
        bossRb.isKinematic = false;
        bossRb.useGravity = true;
        bossRb.freezeRotation = true;

        Vector3 launchVec = Vector3.up * launchForce
                          + cannonForward * launchForwardForce;
        bossRb.linearVelocity = launchVec;

        if (launchEffect != null)
        {
            launchEffect.SetActive(true);
            var ps = launchEffect.GetComponentsInChildren<ParticleSystem>();
            foreach (var p in ps) { p.Clear(); p.Play(); }
        }


        yield return new WaitForSeconds(0.5f);

        float timeout = 10f;
        float elapsed = 0f;
        float startY = transform.position.y;
        float landedY = startY;

        // 打ち上げ直後の誤検知防止（上昇中は判定しない）
        bool hasReachedPeak = false;
        float peakY = transform.position.y;

        float landY = groundTransform != null
    ? groundTransform.position.y
    : 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            bossRb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);

            boss.transform.Rotate(
                rotAxis,
                rotateSpeed * Time.deltaTime,
                Space.World);

            // 地面のY座標以下になったら着地
            if (boss.transform.position.y <= landY)
            {
                landedY = landY;
                Debug.Log($"[CannonMuzzle] ボス着地！posY={landedY}");
                break;
            }

            yield return null;
        }

        // Rigidbody削除と同時にCharacterController有効化（遅延なし）
        if (bossRb != null) Destroy(bossRb);
        if (bossCC != null) bossCC.enabled = true;

        boss.transform.position = new Vector3(
            boss.transform.position.x,
            landedY,
            boss.transform.position.z);

        yield return StartCoroutine(LandingRollCoroutine(bossCC, cannonForward, rotAxis));

        // 同じ回転軸でうつ伏せに
        Quaternion finalRot = Quaternion.Euler(90f, boss.transform.eulerAngles.y, 0f);
        float alignElapsed = 0f;

        while (alignElapsed < alignTime)
        {
            alignElapsed += Time.deltaTime;
            float t = alignElapsed / alignTime;
            float speedRate = 1f - t;

            // 同じ軸で回転しながらうつ伏せに向かう
            boss.transform.rotation = Quaternion.RotateTowards(
                boss.transform.rotation,
                finalRot,
                rotateSpeed * speedRate * Time.deltaTime);

            yield return null;
        }

        boss.transform.rotation = finalRot;
        boss.SetLaunching(false);

        // 着地後にスタンエフェクトを有効化
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
}