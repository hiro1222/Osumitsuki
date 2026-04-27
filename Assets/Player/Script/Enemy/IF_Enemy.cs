/// <summary>
/// 敵対Enemyの共通インターフェース
/// Normal_SB・Wing_SB・Armor_SB はこれを実装する
///
/// 【外部から呼ぶ関数のみ定義する】
/// ・ReceiveInk()  : EnemyHitReceiverから呼ぶ
/// ・GetIsAlly()   : 外部からお墨付き状態を確認する
/// </summary>
public interface IF_Enemy
{
    /// <summary>
    /// 墨を塗られたときの処理
    /// EnemyHitReceiverから呼ぶ
    /// </summary>
    void ReceiveInk();

    /// <summary>
    /// お墨付き状態を返す
    /// 外部からの参照はこの関数経由のみ
    /// </summary>
    bool GetIsAlly();
}
