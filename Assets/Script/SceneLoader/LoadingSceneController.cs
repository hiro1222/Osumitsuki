using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : MonoBehaviour
{
	[SerializeField] private string targetSceneName;
	[SerializeField] private Slider progressBar;      // 任意：進捗バー
	[SerializeField] private RectTransform spinnerIcon; // 任意：くるくる回るアイコン

	private AsyncOperation loadOp;

	private void Start()
	{
		StartCoroutine(LoadTargetScene());
	}

	private System.Collections.IEnumerator LoadTargetScene()
	{
		loadOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
		loadOp.allowSceneActivation = false;

		while (!loadOp.isDone)
		{
			// progressは0〜0.9の範囲で変化する
			float displayProgress = Mathf.Clamp01(loadOp.progress / 0.9f);

			if (progressBar != null)
				progressBar.value = displayProgress;

			// ローディングアニメは毎フレーム動かし続ける
			if (spinnerIcon != null)
				spinnerIcon.Rotate(0, 0, -180f * Time.deltaTime);

			// ロードがほぼ完了したら、演出側の最低表示時間などを待ってから切り替え
			if (loadOp.progress >= 0.9f)
			{
				// 任意：最低1秒はローディング画面を見せる、等の演出待ち
				//yield return new WaitForSeconds(0.5f);

				yield return ActivateTargetScene();
			}

			yield return null;
		}
	}

	private System.Collections.IEnumerator ActivateTargetScene()
	{
		loadOp.allowSceneActivation = true;

		// シーンが実際にロードされ切るまで待つ
		while (!loadOp.isDone)
			yield return null;

		// 新しいシーンをアクティブシーンに設定
		Scene newScene = SceneManager.GetSceneByName(targetSceneName);
		SceneManager.SetActiveScene(newScene);

		// ローディングシーン自身をアンロード
		yield return SceneManager.UnloadSceneAsync("LoadingScene");
	}
}
