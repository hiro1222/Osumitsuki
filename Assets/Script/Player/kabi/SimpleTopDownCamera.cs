using UnityEngine;

/// <summary>
/// シンプルな見下ろしカメラ
/// プレイヤーを追従し、斜め上から見下ろす
///
/// ■ セットアップ:
/// Main Camera にこのスクリプトをアタッチし、Target にプレイヤーをドラッグ
/// </summary>
public class SimpleTopDownCamera : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private Transform target;

    [Header("カメラ設定")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -10f);
    [SerializeField] private float followSpeed = 8f;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
        transform.LookAt(target.position);
    }
}
