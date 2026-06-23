using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{

    private AsyncOperation preloadOp;

    public void StartPreload(string _nextSceneName)
    {
        StartCoroutine(PreloadScene(_nextSceneName));
    }

    private System.Collections.IEnumerator PreloadScene(string _sceneName)
    {
        preloadOp = SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
        preloadOp.allowSceneActivation = false; //90％でロードを止めておく

        // progressは0.9で止まる
        while (preloadOp.progress < 0.9f)
        {
            Debug.Log("ロード進捗：" + (preloadOp.progress * 100f) + "%");
            yield return null;
        }

        Debug.Log("ロードほぼ完了。アクティブ化待ち");
    }

	//任意のタイミングで呼ぶ
    public void ActivateLoadedScene()
    {
        if (preloadOp != null)
        {
            preloadOp.allowSceneActivation = true;
        }
    }


	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
