using UnityEngine;

/// <summary>
/// 汎用攻撃予告スクリプト
/// ・徐々に伸びる線で攻撃方向を示す
/// ・障害物に当たったらそこで切れる
///
/// 【使い方】
/// ・Show(direction)          : 方向を指定して表示
/// ・ShowAt(origin, direction) : 位置と方向を指定して表示
/// ・Hide()                   : 非表示
/// </summary>
public class AttackIndicator : MonoBehaviour
{
    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("── LineRenderer ──")]
    [SerializeField] private LineRenderer lineRenderer;
    [Tooltip("線の最大長さ（m）")]
    [SerializeField] private float lineLength = 10f;
    [Tooltip("線が伸びる速度（m/秒）0以下なら即座に最大長さ）")]
    [SerializeField] private float extendSpeed = 15f;

    [Header("── 矢印タイプ ──")]
    [SerializeField] private GameObject arrowObject;

    [Header("── 障害物検知 ──")]
    [Tooltip("障害物として検知するレイヤー")]
    [SerializeField] private LayerMask obstacleLayer = ~0;
    [Tooltip("TriggerコライダーをRaycastで検知するか")]
    [SerializeField] private bool ignoreTriggers = true;

    [Header("── 共通設定 ──")]
    [Tooltip("表示時間（0以下なら手動でHide()を呼ぶまで表示）")]
    [SerializeField] private float autoHideDelay = 0f;

    [Header("── 位置調整 ──")]
    [Tooltip("線の高さオフセット（地面から浮かせる）")]
    [SerializeField] private float heightOffset = 0.1f;
    [Tooltip("線の回転オフセット（度）")]
    [SerializeField] private float rotationOffset = 0f;

    // ====================================================================
    //  内部状態
    // ====================================================================

    private bool isShowing = false;
    private float showTimer = 0f;
    private float currentLength = 0f; // 現在の線の長さ
    private Vector3 currentOrigin;
    private Vector3 currentDirection;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Awake()
    {
        HideAll();
    }

    // ====================================================================
    //  毎フレーム
    // ====================================================================

    private void Update()
    {
        if (!isShowing) return;

        // 自動非表示
        if (autoHideDelay > 0f)
        {
            showTimer += Time.deltaTime;
            if (showTimer >= autoHideDelay)
            {
                Hide();
                return;
            }
        }

        // 線を徐々に伸ばす
        UpdateLine();
    }

    // ====================================================================
    //  線の更新
    // ====================================================================

    private void UpdateLine()
    {
        if (lineRenderer == null) return;

        float maxLength = GetObstacleDistance();

        if (extendSpeed > 0f)
        {
            currentLength = Mathf.MoveTowards(
                currentLength, maxLength, extendSpeed * Time.deltaTime);
        }
        else
        {
            currentLength = maxLength;
        }

        Vector3 start = currentOrigin;
        Vector3 end = currentOrigin + currentDirection * currentLength;

        // ↓ 追加：Y座標を固定して地面に平行にする
        start.y = currentOrigin.y;
        end.y = currentOrigin.y;

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    /// <summary>障害物までの距離を返す（なければlineLength）</summary>
    private float GetObstacleDistance()
    {
        QueryTriggerInteraction triggerInteraction = ignoreTriggers
            ? QueryTriggerInteraction.Ignore
            : QueryTriggerInteraction.Collide;

        if (Physics.Raycast(
            currentOrigin,
            currentDirection,
            out RaycastHit hit,
            lineLength,
            obstacleLayer,
            triggerInteraction))
        {
            return hit.distance;
        }

        return lineLength;
    }

    // ====================================================================
    //  外部から呼ぶ関数
    // ====================================================================

    /// <summary>方向を指定して表示する</summary>
    public void Show(Vector3 direction)
    {
        ShowAt(transform.position, direction);
    }

    /// <summary>位置と方向を指定して表示する</summary>
    public void ShowAt(Vector3 origin, Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        direction.Normalize();

        // 回転オフセットを適用
        direction = Quaternion.Euler(0f, rotationOffset, 0f) * direction;

        // 高さオフセットを適用
        origin.y += heightOffset;

        currentOrigin = origin;
        currentDirection = direction;
        currentLength = 0f;
        isShowing = true;
        showTimer = 0f;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, origin);
        }

        if (arrowObject != null)
        {
            arrowObject.SetActive(true);
            arrowObject.transform.position = origin;
            arrowObject.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    /// <summary>非表示にする</summary>
    public void Hide()
    {
        isShowing = false;
        currentLength = 0f;
        HideAll();
    }

    /// <summary>現在表示中かどうかを返す</summary>
    public bool GetIsShowing() => isShowing;

    // ====================================================================
    //  内部処理
    // ====================================================================

    private void HideAll()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;

        if (arrowObject != null)
            arrowObject.SetActive(false);
    }
}