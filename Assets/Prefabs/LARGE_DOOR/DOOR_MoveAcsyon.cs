using UnityEngine;

/// <summary>
/// ドアのお墨付き処理（親オブジェクトに付ける）
/// 
/// ■ 配置:
/// - 親（例: Large_door_ver2）にこのスクリプト + PaintableSurfaceGroup
/// - 子（SM_Large_Door_xxx）には PaintableSurface のみ
/// 
/// ■ 動作:
/// 1. 子のどれかが塗られる → PaintableSurface.OnPainted発火
/// 2. PaintableSurfaceGroup が集約して OnAnyPainted を発火
/// 3. このスクリプトが受け取って Painted(0.5f) を呼ぶ
/// 4. 累積が閾値を超えたらお墨付きトリガー
/// 5. Mng_Osumitsuki から Update_Osumitsuki が呼ばれてドアが開く
/// </summary>
public class DOOR_MoveAcsyon : Obj_Osumitsuki
{
    [Header("ドア（開く対象）")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("開く動き")]
    [SerializeField] private float leftOpenAngle = -90f;
    [SerializeField] private float rightOpenAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    private PaintableSurfaceGroup group;
    private Quaternion leftClosedRot, leftOpenRot;
    private Quaternion rightClosedRot, rightOpenRot;

    private void Start()
    {
        // 同じGameObjectのPaintableSurfaceGroupを取得
        group = GetComponent<PaintableSurfaceGroup>();
        if (group != null)
        {
            group.OnAnyPainted += HandleAnyPainted;
        }
        else
        {
            Debug.LogWarning($"[DOOR_MoveAcsyon] {name}: PaintableSurfaceGroupが見つかりません");
        }

        // ドアの開閉角度を記録
        if (leftDoor != null)
        {
            leftClosedRot = leftDoor.localRotation;
            leftOpenRot = leftClosedRot * Quaternion.Euler(0, leftOpenAngle, 0);
        }
        if (rightDoor != null)
        {
            rightClosedRot = rightDoor.localRotation;
            rightOpenRot = rightClosedRot * Quaternion.Euler(0, rightOpenAngle, 0);
        }
    }

    private void OnDestroy()
    {
        if (group != null)
        {
            group.OnAnyPainted -= HandleAnyPainted;
        }
    }

    /// <summary>子のどこかが塗られたら呼ばれる</summary>
    private void HandleAnyPainted(PaintableSurface source, int cells, byte density)
    {
        // 親のObj_OsumitsukiにPaintedを通知（α版なので0.5固定）
        Painted(0.5f);
    }

    /// <summary>お墨付きになった瞬間に1回呼ばれる（Mng_Osumitsukiから）</summary>
    public override void Action_Osumitsuki()
    {
        // 演出開始（音、エフェクト等あればここ）
        Debug.Log($"[DOOR_MoveAcsyon] {name}: お墨付き達成、ドア開門開始");
    }

    /// <summary>毎フレームのドア開閉処理（Mng_Osumitsukiから呼ばれる想定）</summary>
    public override void Update_Osumitsuki()
    {
        bool leftDone = true;
        bool rightDone = true;

        if (leftDoor != null)
        {
            leftDoor.localRotation = Quaternion.Slerp(
                leftDoor.localRotation, leftOpenRot,
                openSpeed * Time.deltaTime);
            leftDone = Quaternion.Angle(leftDoor.localRotation, leftOpenRot) < 0.5f;
        }
        if (rightDoor != null)
        {
            rightDoor.localRotation = Quaternion.Slerp(
                rightDoor.localRotation, rightOpenRot,
                openSpeed * Time.deltaTime);
            rightDone = Quaternion.Angle(rightDoor.localRotation, rightOpenRot) < 0.5f;
        }

        // 両方開ききったら終了通知
        if (leftDone && rightDone && !OsumiFlg)
        {
            Action2Update();
        }
    }
}