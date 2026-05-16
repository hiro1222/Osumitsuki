using UnityEngine;

/// <summary>
/// 子オブジェクトのColliderにアタッチする中継スクリプト
/// FlyingSlashがEnemyHitReceiverを探す動作をそのまま利用する
/// </summary>
[RequireComponent(typeof(Collider))]
public class InkHitRelay : MonoBehaviour
{
    private InkReactObject inkReactObject;

    private void Awake()
    {
        // 親のInkReactObjectを取得
        inkReactObject = GetComponentInParent<InkReactObject>();

        if (inkReactObject == null)
            Debug.LogWarning("[InkHitRelay] 親にInkReactObjectが見つかりません");
    }

    /// <summary>
    /// FlyingSlashのEnemyHitReceiverと同名のメソッド
    /// FlyingSlashは GetComponent<EnemyHitReceiver>() で探すので
    /// このスクリプトではなく EnemyHitReceiver を使う → 下記参照
    /// </summary>
    public void ReceiveInkHit()
    {
        inkReactObject?.ReceiveInk();
    }
}