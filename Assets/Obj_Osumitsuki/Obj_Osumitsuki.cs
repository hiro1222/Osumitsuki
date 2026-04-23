using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class Obj_Osumitsuki : MonoBehaviour
{

    [Header("インクステータス")]
    //現在のインクの量
    protected float curInkAmount = 0;
    [SerializeField] private float maxInkCapa = 100;      //インクの最大量
    [SerializeField] private float InkRatio = 70;   //お墨付き

    private bool osumitsukiTrg = false; //お墨付きした時にtrueへ
    private bool osumitsukiFlg = false; //Action_Osumitsuki後にtrueへ


    //プロパティ
    public bool OsumiTrg => osumitsukiTrg;
    public bool OsumiFlg => osumitsukiFlg;  //お墨付きかどうか


    //お墨付き時のアクション
    public virtual void Action_Osumitsuki()
    {

    }
    //Action_Osumitsuki後にマイフレーム更新
    public virtual void Update_Osumitsuki()
    {

    }

    //塗られたときの処理
    public bool Painted(float _ink)
    {
        curInkAmount += _ink;

        if (curInkAmount > maxInkCapa)
            curInkAmount = maxInkCapa;

        if (InkRatio/100f <= curInkAmount / maxInkCapa)
        {
            osumitsukiTrg = true;
            Mng_Osumitsuki.instance.AddObject(this);
        }

        return osumitsukiTrg;
    }

    public void Action2Update()
    {
        osumitsukiFlg = true;
    }
}
