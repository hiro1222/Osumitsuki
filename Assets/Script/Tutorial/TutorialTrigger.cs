using UnityEngine;

/// <summary>
/// チュートリアル表示エリアの侵入判定
/// 2つの空のGameObject（PointA・PointB）で箱型エリアを定義する
/// Playerがエリア内に入ったら一度だけチュートリアル画像を表示する
///
/// 【セットアップ】
/// ① 空のGameObjectにこのスクリプトをアタッチ
/// ② 空のGameObjectを2つ作り（PointA・PointB）シーンに配置
/// ③ InspectorでPointA・PointB・tutorialSpriteを設定
/// ④ PointAとPointBをエリアの対角に置く
/// </summary>
public class TutorialTrigger : MonoBehaviour
{
    [Header("エリアの対角2点")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("表示するチュートリアル画像")]
    [SerializeField] private Sprite tutorialSprite;

    [Header("参照")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerLayerName = "Player";

    private bool hasTriggered = false;

    private void Start()
    {
        if (player == null)
        {
            int playerLayer = LayerMask.NameToLayer(playerLayerName);
            var allCC = FindObjectsOfType<CharacterController>();
            foreach (var cc in allCC)
            {
                if (cc.gameObject.layer == playerLayer)
                {
                    player = cc.transform;
                    Debug.Log($"[TutorialTrigger] プレイヤー取得: {cc.name}");
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (hasTriggered) return;
        if (player == null) return;
        if (pointA == null || pointB == null)
        {
            Debug.LogWarning($"[TutorialTrigger] pointA={pointA} pointB={pointB}");
            return;
        }

        if (IsInsideArea(player.position))
        {
            hasTriggered = true;
            Debug.Log($"[TutorialTrigger] エリア侵入検知！TutorialManager.Instance={TutorialManager.Instance}");
            TutorialManager.Instance?.ShowTutorial(tutorialSprite);
        }
    }

    private bool IsInsideArea(Vector3 pos)
    {
        Vector3 min = Vector3.Min(pointA.position, pointB.position);
        Vector3 max = Vector3.Max(pointA.position, pointB.position);

        return pos.x >= min.x && pos.x <= max.x &&
               pos.y >= min.y && pos.y <= max.y &&
               pos.z >= min.z && pos.z <= max.z;
    }

    private void OnDrawGizmos()
    {
        if (pointA == null || pointB == null) return;

        Vector3 min = Vector3.Min(pointA.position, pointB.position);
        Vector3 max = Vector3.Max(pointA.position, pointB.position);
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);

        Gizmos.DrawSphere(pointA.position, 0.3f);
        Gizmos.DrawSphere(pointB.position, 0.3f);
    }
}