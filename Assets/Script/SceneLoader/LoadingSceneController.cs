using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : MonoBehaviour
{
	[SerializeField] private Slider progressBar;      // 任意：進捗バー
	[SerializeField] private RectTransform spinnerIcon; // 任意：くるくる回るアイコン
	[SerializeField] private float minimumShowTime = 1.0f;	//最小描画時間

	private AsyncOperation loadOp;

	private void Start()
	{
		StartCoroutine(LoadTargetScene());
	}

	private System.Collections.IEnumerator LoadTargetScene()
	{

		if (SceneTransitionData.nextSceneName == "")
		{
			Debug.Log("次シーンがわかりません");
			yield return null;
		}

		float startTime = Time.time;

		loadOp = SceneManager.LoadSceneAsync(SceneTransitionData.nextSceneName, LoadSceneMode.Additive);
		loadOp.allowSceneActivation = false;

		while (!loadOp.isDone)
		{
			UpdateVisuals(loadOp.progress / 0.9f);
			yield return null;
		}
		while (Time.time - startTime < minimumShowTime)
		{
			UpdateVisuals(1f);
			yield return null;
		}
			// ロードがほぼ完了したら、演出側の最低表示時間などを待ってから切り替え
			
		yield return ActivateTargetScene();
	}

	private System.Collections.IEnumerator ActivateTargetScene()
	{
		loadOp.allowSceneActivation = true;

		// シーンが実際にロードされ切るまで待つ
		while (!loadOp.isDone)
			yield return null;

		// 新しいシーンをアクティブシーンに設定
		Scene newScene = SceneManager.GetSceneByName(SceneTransitionData.nextSceneName);
		SceneManager.SetActiveScene(newScene);

		// ローディングシーン自身をアンロード
		yield return SceneManager.UnloadSceneAsync("LoadingScene");
	}

	private void UpdateVisuals(float _ratio)
	{
		if (progressBar != null)
			progressBar.value = _ratio;

		if (spinnerIcon != null)
			spinnerIcon.Rotate(0, 0, -180f * Time.deltaTime);
	}
}
