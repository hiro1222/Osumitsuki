using System.Collections;
using UnityEngine;

/// <summary>
/// 演出用カメラ。指定したターゲット(空のGameObjectのTransform)の位置・向きへ
/// カメラを移動/固定する。Cinemachine不要。
///
/// 使い方:
///   - 演出用に Camera を1台用意(またはMainを流用)
///   - 映したい構図ごとに空のGameObjectを置き、位置と向きを調整(CamPoint_A など)
///   - 演出ステップから MoveTo(CamPoint) / SnapTo(CamPoint) を呼ぶ
///
/// 通常カメラ(自前追従)と併用する場合は、演出開始時に通常カメラを無効化し、
/// この演出カメラを有効化する(CameraModeController 等で制御)。
/// </summary>
public class CutsceneCamera : MonoBehaviour
{
    [Header("動かす対象カメラ (未指定なら自分のTransform)")]
    [SerializeField] private Transform cameraTransform;

    [Header("移動カーブ")]
    [SerializeField] private AnimationCurve ease =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    void Awake()
    {
        if (cameraTransform == null)
            cameraTransform = transform;
    }

    /// <summary>指定ターゲットの位置・向きへ瞬間移動(カット)。</summary>
    public void SnapTo(Transform target)
    {
        if (target == null || cameraTransform == null) return;
        cameraTransform.SetPositionAndRotation(target.position, target.rotation);
    }

    /// <summary>指定ターゲットへ duration 秒かけてなめらかに移動。</summary>
    public IEnumerator MoveTo(Transform target, float duration)
    {
        if (target == null || cameraTransform == null) yield break;

        if (duration <= 0f)
        {
            SnapTo(target);
            yield break;
        }

        Vector3 startPos = cameraTransform.position;
        Quaternion startRot = cameraTransform.rotation;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / duration));
            cameraTransform.position = Vector3.LerpUnclamped(startPos, target.position, k);
            cameraTransform.rotation = Quaternion.SlerpUnclamped(startRot, target.rotation, k);
            yield return null;
        }
        cameraTransform.SetPositionAndRotation(target.position, target.rotation);
    }

    // --- UnityEvent から引数なしで呼びたい場合用のラッパー ---
    // ターゲットをインスペクターで固定しておき、ボタン一つで移動させたいとき。

    [Header("UnityEvent用: 即時移動のターゲット")]
    [SerializeField] private Transform quickSnapTarget;

    /// <summary>インスペクター指定ターゲットへ瞬間移動(UnityEvent用)。</summary>
    public void SnapToQuickTarget() => SnapTo(quickSnapTarget);
}
