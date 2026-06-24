using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Cannon_Osumitsuki))]
public class CannonAutoAim : MonoBehaviour
{
    [Header("── 回転対象 ──")]
    [SerializeField] private Transform cannon;

    [Header("── 必要友好Enemy数 ──")]
    [SerializeField] private int requiredAllies = 2;

    [Header("── 段階下向き設定 ──")]
    [SerializeField] private int maxAlliesForFullDown = 4;

    [Header("── 角度設定 ──")]
    [SerializeField] private Vector3 startLocalEuler = new Vector3(0f, 0f, 90f);
    [SerializeField] private Vector3 raisedLocalEuler = new Vector3(0f, 0f, 0f);

    [Header("── 回転速度 ──")]
    [SerializeField] private float rotateSpeed = 90f;

    [Header("── MuzzleTrigger ──")]
    [SerializeField] private GameObject muzzleTrigger;

    [Header("── デバッグ ──")]
    [SerializeField] private bool debugLog = false;

    private Obj_Osumitsuki osumitsuki;
    private Quaternion loweredRotation;
    private Quaternion raisedRotation;
    private bool isRaised = false;

    // ★ これまでに到達した最大のt値を記憶（後退を防ぐ）
    private float maxReachedT = 0f;

    private void Start()
    {
        osumitsuki = GetComponent<Obj_Osumitsuki>();

        if (cannon == null)
        {
            Debug.LogError("[CannonAutoAim] cannon（筒のTransform）が設定されていません");
            enabled = false;
            return;
        }

        loweredRotation = Quaternion.Euler(startLocalEuler);
        cannon.localRotation = loweredRotation;

        raisedRotation = Quaternion.Euler(raisedLocalEuler);

        if (muzzleTrigger != null)
            muzzleTrigger.SetActive(false);
    }

    private void Update()
    {
        if (osumitsuki == null || cannon == null) return;

        if (!isRaised)
        {
            // ★ 到着済みの味方数だけを使う
            int helperNum = GetArrivedHelperCount();
            float t = Mathf.Clamp01((float)helperNum / maxAlliesForFullDown);

            // ★ 一度到達したtより下がらないようにする
            if (t > maxReachedT)
                maxReachedT = t;

            Quaternion targetRot = Quaternion.Slerp(loweredRotation, raisedRotation, maxReachedT);

            cannon.localRotation = Quaternion.RotateTowards(
                cannon.localRotation,
                targetRot,
                rotateSpeed * Time.deltaTime);

            bool shouldRaise =
                osumitsuki.OsumiTrg &&
                helperNum >= requiredAllies;

            if (shouldRaise && IsAimedUp())
            {
                isRaised = true;
                osumitsuki.End();
                if (muzzleTrigger != null)
                    muzzleTrigger.SetActive(true);

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

                Debug.Log("[CannonAutoAim] 砲身発射準備完了。味方解放");
            }
        }

        if (debugLog)
            Debug.Log($"[CannonAutoAim] ArrivedHelper={GetArrivedHelperCount()} maxReachedT={maxReachedT} isRaised={isRaised}");
    }

    public bool IsAimedUp()
    {
        if (cannon == null) return false;
        return Quaternion.Angle(cannon.localRotation, raisedRotation) < 5f;
    }

    public void ResetCannon()
    {
        isRaised = false;
        maxReachedT = 0f; // ★ リセット時にtも初期化

        if (muzzleTrigger != null)
            muzzleTrigger.SetActive(false);

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

    private int GetArrivedHelperCount()
    {
        if (osumitsuki == null) return 0;

        var statesField = typeof(Obj_Osumitsuki).GetField(
            "helperEnemyStates",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (statesField == null) return 0;

        var states = statesField.GetValue(osumitsuki) as AllyEnemy.IAllyEnemyState[];
        if (states == null) return 0;

        int count = 0;
        foreach (var state in states)
        {
            if (state == null) continue;

            var getStateMethod = state.GetType().GetMethod("GetState");
            if (getStateMethod == null) continue;

            var stateValue = getStateMethod.Invoke(state, null);
            if (stateValue != null && stateValue.ToString() == "HELPER")
                count++;
        }

        return count;
    }
}