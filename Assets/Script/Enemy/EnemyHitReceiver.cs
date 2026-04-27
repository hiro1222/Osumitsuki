using UnityEngine;

/// <summary>
/// エネミーの被弾受付
/// FlyingSlash の Raycast が当たったとき、IF_Enemy.ReceiveInk() を呼ぶ
///
/// ■ セットアップ:
/// IF_Enemyを実装したスクリプト（Normal_SB等）と
/// 同じGameObjectにアタッチ。
/// Collider（CapsuleCollider等）が必要。
/// </summary>
public class EnemyHitReceiver : MonoBehaviour
{
    private IF_Enemy enemy;

    private void Awake()
    {
        enemy = GetComponent<IF_Enemy>();

        if (enemy == null)
        {
            Debug.LogWarning("[EnemyHitReceiver] IF_Enemyを実装したコンポーネントが見つかりません");
        }
    }

    /// <summary>
    /// FlyingSlash の Raycast が当たったときに呼ぶ
    /// </summary>
    public void ReceiveInkHit()
    {
        if (enemy == null) return;
        if (enemy.GetIsAlly()) return;

        enemy.ReceiveInk();
    }
}
