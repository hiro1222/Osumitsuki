using UnityEngine;

public class LineDrawer : MonoBehaviour
{
    public LineRenderer linePrefab; // 1で作ったPrefabをここに入れる
    private LineRenderer currentLine;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // 左クリックした瞬間
        {
            // 新しい線を作成
            currentLine = Instantiate(linePrefab);
            currentLine.positionCount = 0;
        }

        if (Input.GetMouseButton(0) && currentLine != null) // ドラッグ中
        {
            // マウスの位置を3D空間の座標に変換
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = 10f; // カメラからの距離
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

            // 線に新しい点を追加
            currentLine.positionCount++;
            currentLine.SetPosition(currentLine.positionCount - 1, worldPos);
        }
    }
}