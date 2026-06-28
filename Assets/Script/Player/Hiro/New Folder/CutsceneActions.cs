using UnityEngine;

/// <summary>
/// シーケンサーの UnityEvent から呼ぶヘルパー。
/// スカイボックス変更・花火Prefab生成・自前カメラの有効/無効切替をまとめる。
/// カメラ移動は CutsceneCamera が担当するため、ここにカメラ依存は無い。
/// </summary>
public class CutsceneActions : MonoBehaviour
{
    [Header("スカイボックス")]
    [SerializeField] private Material newSkybox;

    [Header("花火 (Prefab方式)")]
    [SerializeField] private GameObject fireworksPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float autoDestroySeconds = 5f;

    [Header("演出中に止める通常カメラ(自前追従スクリプト)")]
    [SerializeField] private Behaviour normalCameraScript;

    [Header("演出中に止めるプレイヤー操作スクリプト")]
    [Tooltip("移動/入力/アニメ制御など、演出中にアニメを上書きしてしまうスクリプトを登録。複数可。")]
    [SerializeField] private Behaviour[] playerControlScripts;

    /// <summary>スカイボックスを差し替える。アイリスで暗転中に呼ぶこと。</summary>
    public void ChangeSkybox()
    {
        if (newSkybox != null)
        {
            RenderSettings.skybox = newSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }

    /// <summary>花火Prefabを生成する。</summary>
    public void PlayFireworks()
    {
        if (fireworksPrefab == null) return;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            SpawnAt(transform.position, transform.rotation);
            return;
        }
        foreach (var p in spawnPoints)
        {
            if (p == null) continue;
            SpawnAt(p.position, p.rotation);
        }
    }

    private void SpawnAt(Vector3 pos, Quaternion rot)
    {
        GameObject fx = Instantiate(fireworksPrefab, pos, rot);
        if (autoDestroySeconds > 0f)
            Destroy(fx, autoDestroySeconds);
    }

    // --- カメラ ---
    public void DisableNormalCamera()
    {
        if (normalCameraScript != null) normalCameraScript.enabled = false;
    }

    public void EnableNormalCamera()
    {
        if (normalCameraScript != null) normalCameraScript.enabled = true;
    }

    // --- プレイヤー操作 ---
    /// <summary>演出開始時に呼ぶ。プレイヤー操作を止めてアニメ上書きを防ぐ。</summary>
    public void DisablePlayerControl()
    {
        if (playerControlScripts == null) return;
        foreach (var s in playerControlScripts)
            if (s != null) s.enabled = false;
    }

    /// <summary>演出終了時に呼ぶ。プレイヤー操作を戻す。</summary>
    public void EnablePlayerControl()
    {
        if (playerControlScripts == null) return;
        foreach (var s in playerControlScripts)
            if (s != null) s.enabled = true;
    }

    // --- カメラ+プレイヤーまとめて ---
    /// <summary>演出開始: カメラとプレイヤー操作を両方止める。</summary>
    public void BeginCutsceneControl()
    {
        DisableNormalCamera();
        DisablePlayerControl();
    }

    /// <summary>演出終了: カメラとプレイヤー操作を両方戻す。</summary>
    public void EndCutsceneControl()
    {
        EnableNormalCamera();
        EnablePlayerControl();
    }
}
