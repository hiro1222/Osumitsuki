using UnityEngine;

public class TORII_Osumi : Obj_Osumitsuki
{
    bool AnimationFG = false;

    [SerializeField] private PlayerStats CS_PlayerState;

    private void Start()
    {
        // 初期位置を記憶
        transform.position = new Vector3(transform.position.x, 1f, transform.position.z);
    }

    public override void Action_Osumitsuki()
    {
        AnimationFG = true;

        //Debug.Log($"[TORII_Osumi] {name}: お墨付き達成、リスポーン位置更新");
        transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        //Debug.Log(transform.position);
        CS_PlayerState.SetspawnPosition(transform.position);

        Action2Update();

    }

    public override void Update_Osumitsuki()
    {
        

        if (AnimationFG==false)
        {
            End();
        }

    }
}
