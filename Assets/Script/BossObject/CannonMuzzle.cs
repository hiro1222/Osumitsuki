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

    [Header("── 着地 ──")]
    [Tooltip("地面のTransform（この位置のY座標を着地点にする）")]
    [SerializeField] private Transform groundTransform;
    [Tooltip("地面からのオフセット（めり込み調整用）")]
    [SerializeField] private float groundOffset = 0.5f;
    [Tooltip("打ち上げ時の落下速度を上げる力")]
    [SerializeField] private float extraGravity = 20f;
    [Tooltip("うつ伏せになるまでの時間（秒）")]
    [SerializeField] private float alignTime = 0.5f;


    private Collider muzzleCollider;
    private bool hasLaunched = false;

    private void Start()
    {
        if (boss == null)
            boss = FindObjectOfType<Boss_SB>();

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

        yield return new WaitForSeconds(launchDelay);

        if (boss == null) yield break;

        Debug.Log("[CannonMuzzle] ボスを打ち上げ！");

        var bossRb = boss.gameObject.AddComponent<Rigidbody>();
        bossRb.isKinematic = false;
        bossRb.useGravity = true;
        bossRb.freezeRotation = true;

        Vector3 launchVec = Vector3.up * launchForce
                          + transform.forward * launchForwardForce;
        bossRb.AddForce(launchVec, ForceMode.Impulse);

        yield return new WaitForSeconds(0.5f);

        float timeout = 10f;
        float elapsed = 0f;

        float landY = groundTransform != null
            ? groundTransform.position.y + groundOffset
            : groundOffset;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            // ★ 下降中は追加重力で落下速度を上げる
            if (bossRb.linearVelocity.y <= 0f)
                bossRb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);

            if (bossRb.linearVelocity.y <= 0f &&
                boss.transform.position.y <= landY)
            {
                Debug.Log("[CannonMuzzle] ボス着地！");
                break;
            }

            yield return null;
        }

        if (bossRb != null) Destroy(bossRb);

        // ★ 着地後にY座標固定
        boss.transform.position = new Vector3(
            boss.transform.position.x,
            landY,
            boss.transform.position.z);

        // ★ 着地後にうつ伏せに滑らかに回転
        Quaternion targetRot = Quaternion.Euler(
            90f,
            boss.transform.eulerAngles.y,
            0f);

        elapsed = 0f;
        Quaternion startRot = boss.transform.rotation;

        while (elapsed < alignTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / alignTime;
            boss.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        boss.transform.rotation = targetRot;

        if (bossCC != null) bossCC.enabled = true;

        boss.SetLaunching(false);
        boss.NotifyHitCannon();
        hasLaunched = false;
    }
}