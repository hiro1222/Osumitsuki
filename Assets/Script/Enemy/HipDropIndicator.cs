using UnityEngine;

/// <summary>
/// ヒップドロップの着地予告サークル
/// 徐々に大きくなり、着地時に消える
/// </summary>
public class HipDropIndicator : MonoBehaviour
{
    [Tooltip("最大スケール")]
    [SerializeField] private float maxScale = 3f;
    [Tooltip("拡大速度")]
    [SerializeField] private float expandSpeed = 2f;

    private bool isShowing = false;

    private void Update()
    {
        if (!isShowing) return;

        float current = transform.localScale.x;
        float next = Mathf.MoveTowards(current, maxScale, expandSpeed * Time.deltaTime);

        // ★ YはそのままでX・Zだけ大きくする
        transform.localScale = new Vector3(next, transform.localScale.y, next);
    }

    /// <summary>表示開始（位置を指定）</summary>
    public void Show(Vector3 position)
    {
        transform.SetParent(null);
        transform.position = new Vector3(position.x, position.y + 0.05f, position.z);
        // ★ YはそのままでX・Zだけ0にリセット
        transform.localScale = new Vector3(0f, transform.localScale.y, 0f);
        isShowing = true;
        gameObject.SetActive(true);
    }


    /// <summary>非表示</summary>
    public void Hide()
    {
        isShowing = false;
        gameObject.SetActive(false);
    }
}