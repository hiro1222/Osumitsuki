using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 演出終了後などにシーン遷移を行う。
/// SceneTransitionData に次シーン名を入れてから LoadingScene を経由する方式。
/// シーケンサーの On Sequence Complete から LoadNextScene() を呼ぶ。
/// </summary>
public class CutsceneSceneLoader : MonoBehaviour
{
    [Header("遷移先のシーン名")]
    [Tooltip("演出後に最終的に読み込みたいシーン名。例: Stage_B_light")]
    [SerializeField] private string nextSceneName = "Stage_B_light";

    [Header("経由するローディングシーン名")]
    [SerializeField] private string loadingSceneName = "LoadingScene";

    [Header("遷移前の待ち時間(秒)")]
    [Tooltip("演出の余韻を残したい場合に設定。0なら即遷移。")]
    [SerializeField] private float delayBeforeLoad = 0f;

    /// <summary>次シーンへ遷移する。On Sequence Complete から呼ぶ。</summary>
    public void LoadNextScene()
    {
        if (delayBeforeLoad > 0f)
            Invoke(nameof(DoLoad), delayBeforeLoad);
        else
            DoLoad();
    }

    private void DoLoad()
    {
        // 写真の方式に合わせて、次シーン名を保存してからローディングシーンへ
        SceneTransitionData.nextSceneName = nextSceneName;
        SceneManager.LoadScene(loadingSceneName);
    }
}