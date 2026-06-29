using UnityEngine;

/// <summary>
/// プレイヤーがエリア(Trigger Collider)に入ると演出を発火する。
/// 空のGameObjectにColliderを付けてIsTrigger ONにし、これを付ける。
/// </summary>
[RequireComponent(typeof(Collider))]
public class CutsceneAreaTrigger : MonoBehaviour
{
    [Header("発火する演出")]
    [SerializeField] private CutsceneSequencer sequencer;

    [Header("反応するタグ")]
    [SerializeField] private string playerTag = "Player";

    [Header("一度だけ発火するか")]
    [SerializeField] private bool triggerOnce = true;

    private bool _fired;

    void Reset()
    {
        // 付けた瞬間にColliderをトリガー化
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (sequencer == null) return;
        if (triggerOnce && _fired) return;
        if (!other.CompareTag(playerTag)) return;

        _fired = true;
        sequencer.Play();
    }
}