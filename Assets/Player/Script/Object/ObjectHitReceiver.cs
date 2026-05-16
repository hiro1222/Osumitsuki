using UnityEngine;

/// <summary>
/// ギミックオブジェクトの子Colliderにアタッチする被弾受付
/// FlyingSlashが触れたことをCollision/Triggerで自前検知し、
/// 親のInkReactObjectに通知する
///
/// FlyingSlashは完全ノータッチ
///
/// 【セットアップ】
/// SM_GROUND_FLOWER・SM_SAKURA_FLOWER・SM_SAKURA_TREE
/// それぞれにこのスクリプトをアタッチ
/// </summary>
public class ObjectHitReceiver : MonoBehaviour
{
    private InkReactObject inkReactObject;

    private void Awake()
    {
        inkReactObject = GetComponentInParent<InkReactObject>();

        if (inkReactObject == null)
            Debug.LogWarning($"[ObjectHitReceiver] {gameObject.name}: 親にInkReactObjectが見つかりません");
    }

    // FlyingSlashのColliderがTriggerの場合
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<FlyingSlash>() == null) return;
        inkReactObject?.ReceiveInk();
    }

    // FlyingSlashのColliderが非Triggerの場合
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<FlyingSlash>() == null) return;
        inkReactObject?.ReceiveInk();
    }
}