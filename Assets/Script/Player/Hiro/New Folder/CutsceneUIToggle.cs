using UnityEngine;

/// <summary>
/// 演出中にUIを隠す/戻すためのトグル。
/// シーケンサーの UnityEvent から Hide() / Show() を呼ぶだけで使える。
///
/// 使い方:
///   - CutsceneManager 等にこのコンポーネントを付ける
///   - targets に隠したいUI(HealthUI, InkGaugeUI など)を登録(複数可)
///     ※ irisImage(アイリス暗転用)は登録しないこと。隠すと暗転も消える。
///   - ステップのActionで Hide / Show を呼ぶ
///     例: 演出開始ステップ → Hide / On Sequence Complete → Show
/// </summary>
public class CutsceneUIToggle : MonoBehaviour
{
    [Header("演出中に隠すUI (irisImageは入れない)")]
    [SerializeField] private GameObject[] targets;

    /// <summary>登録UIを全部隠す。</summary>
    public void Hide()
    {
        if (targets == null) return;
        foreach (var t in targets)
            if (t != null) t.SetActive(false);
    }

    /// <summary>登録UIを全部戻す。</summary>
    public void Show()
    {
        if (targets == null) return;
        foreach (var t in targets)
            if (t != null) t.SetActive(true);
    }

    /// <summary>表示状態を反転(任意用途)。</summary>
    public void Toggle()
    {
        if (targets == null) return;
        foreach (var t in targets)
            if (t != null) t.SetActive(!t.activeSelf);
    }
}
