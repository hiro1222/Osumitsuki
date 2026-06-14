using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大砲の砲身を自動で真上に向けるスクリプト（フェーズ3）
///
/// 友好Enemyが必要数そろうと、cannon（筒の部分）の角度を徐々に真上へ向ける。
/// 友好Enemyが減ると元の横向きに戻る。
///
/// 【セットアップ】
/// ① 大砲オブジェクト（Cannon_Osumitsukiがついているもの）にアタッチ
/// ② Inspectorで cannon（筒のTransform）をドラッグ
/// ③ 必要な友好Enemy数・真上角度・回転軸を設定
///
/// 【注意】
/// Cannon_Osumitsuki は触らずに、このスクリプトで角度だけ制御する。
/// </summary>
[RequireComponent(typeof(Cannon_Osumitsuki))]
public class CannonAutoAim : MonoBehaviour
{
    [Header("── 回転対象 ──")]
    [Tooltip("回転させる筒のTransform（cannon）")]
    [SerializeField] private Transform cannon;

    [Header("── 必要友好Enemy数 ──")]
    [Tooltip("砲身を真上に向けるのに必要な友好Enemyの数")]
    [SerializeField] private int requiredAllies = 2;

    [Header("── 真上を向く角度 ──")]
    [Tooltip("真上を向いたときのローカルEuler角度（横→真上に必要な角度を入れる）")]
    [SerializeField] private Vector3 raisedLocalEuler = new Vector3(0f, 0f, 90f);

    [Header("── 回転速度 ──")]
    [Tooltip("角度変化の速度（度/秒）")]
    [SerializeField] private float rotateSpeed = 90f;

    [Header("── MuzzleTrigger ──")]
    [Tooltip("砲身が真上を向いたときに有効化するMuzzleTrigger")]
    [SerializeField] private GameObject muzzleTrigger;

    [Header("── デバッグ ──")]
    [SerializeField] private bool debugLog = false;

    private Obj_Osumitsuki osumitsuki;
    private Quaternion loweredRotation; // 最初の横向き角度
    private Quaternion raisedRotation;  // 真上向き角度
    private bool isRaised = false;      // 現在真上を向いているか

    private void Start()
    {
        osumitsuki = GetComponent<Obj_Osumitsuki>();

        if (cannon == null)
        {
            Debug.LogError("[CannonAutoAim] cannon（筒のTransform）が設定されていません");
            enabled = false;
            return;
        }

        // 最初の角度を「横向き」として記憶
        loweredRotation = cannon.localRotation;
        // 真上向き角度を計算
        raisedRotation = Quaternion.Euler(raisedLocalEuler);

        if (muzzleTrigger != null)
            muzzleTrigger.SetActive(false);
    }

    private void Update()
    {
        if (osumitsuki == null || cannon == null) return;

        if (!isRaised)
        {
            bool shouldRaise =
                osumitsuki.OsumiTrg &&
                osumitsuki.GetHelperNum() >= requiredAllies;

            Quaternion targetRot = shouldRaise ? raisedRotation : loweredRotation;

            cannon.localRotation = Quaternion.RotateTowards(
                cannon.localRotation,
                targetRot,
                rotateSpeed * Time.deltaTime);

            if (IsAimedUp())
            {
                isRaised = true;
                osumitsuki.End();
                if (muzzleTrigger != null)
                    muzzleTrigger.SetActive(true);

                // Cannon_Osumitsuki の cannon 参照を切って回転を止める
                var cannonScript = GetComponent<Cannon_Osumitsuki>();
                if (cannonScript != null)
                {
                    var cannonField = typeof(Cannon_Osumitsuki).GetField(
                        "cannon",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (cannonField != null)
                        cannonField.SetValue(cannonScript, null);
                }

                Debug.Log("[CannonAutoAim] 砲身真上。味方解放");
            }
        }

        if (debugLog)
            Debug.Log($"[CannonAutoAim] Helper={osumitsuki.GetHelperNum()} isRaised={isRaised}");
    }

    /// <summary>砲身が真上を向いているか（ほぼ真上ならtrue）</summary>
    public bool IsAimedUp()
    {
        if (cannon == null) return false;
        return Quaternion.Angle(cannon.localRotation, raisedRotation) < 5f;
    }

    /// <summary>大砲を初期状態に戻す（タックル衝突時に呼ぶ）</summary>
    public void ResetCannon()
    {
        isRaised = false;

        // MuzzleTriggerを無効化
        if (muzzleTrigger != null)
            muzzleTrigger.SetActive(false);

        // 砲身をシームレスに元の角度に戻す
        StartCoroutine(ResetCannonCoroutine());
    }

    private IEnumerator ResetCannonCoroutine()
    {
        while (Quaternion.Angle(cannon.localRotation, loweredRotation) > 0.1f)
        {
            cannon.localRotation = Quaternion.RotateTowards(
                cannon.localRotation,
                loweredRotation,
                rotateSpeed * Time.deltaTime);
            yield return null;
        }
        cannon.localRotation = loweredRotation;
        Debug.Log("[CannonAutoAim] 砲身リセット完了");
    }

}
