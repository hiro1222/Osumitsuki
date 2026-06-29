using System.Collections;
using UnityEngine;

/// <summary>
/// 演出中のUI表示制御。
/// 「普段隠れていて、シーケンサーで呼んだタイミングだけ表示」する使い方に対応。
///
/// 使い方:
///   - CutsceneManager 等にこのコンポーネントを付ける
///   - targets に制御したいUIを登録(複数可)
///   - hideOnStart にチェックを入れると、ゲーム開始時に自動で隠す
///   - ステップのActionで Show / Hide / ShowForSeconds を呼ぶ
/// </summary>
public class CutsceneUIToggle : MonoBehaviour
{
    [Header("制御するUI")]
    [SerializeField] private GameObject[] targets;

    [Header("ゲーム開始時に隠すか (普段隠す用途ならON)")]
    [SerializeField] private bool hideOnStart = true;

    [Header("ShowForSeconds で表示する秒数")]
    [SerializeField] private float defaultShowSeconds = 3.0f;

    private void Start()
    {
        if (hideOnStart) Hide();
    }

    /// <summary>登録UIを全部表示する。</summary>
    public void Show()
    {
        SetAll(true);
    }

    /// <summary>登録UIを全部隠す。</summary>
    public void Hide()
    {
        SetAll(false);
    }

    /// <summary>表示状態を反転。</summary>
    public void Toggle()
    {
        if (targets == null) return;
        foreach (var t in targets)
            if (t != null) t.SetActive(!t.activeSelf);
    }

    /// <summary>表示して、defaultShowSeconds 秒後に自動で隠す(UnityEvent用)。</summary>
    public void ShowForSeconds()
    {
        ShowForSeconds(defaultShowSeconds);
    }

    /// <summary>表示して、指定秒後に自動で隠す。</summary>
    public void ShowForSeconds(float seconds)
    {
        StopAllCoroutines();
        StartCoroutine(ShowThenHide(seconds));
    }

    private IEnumerator ShowThenHide(float seconds)
    {
        Show();
        if (seconds > 0f)
            yield return new WaitForSeconds(seconds);
        Hide();
    }

    private void SetAll(bool active)
    {
        if (targets == null) return;
        foreach (var t in targets)
            if (t != null) t.SetActive(active);
    }
}