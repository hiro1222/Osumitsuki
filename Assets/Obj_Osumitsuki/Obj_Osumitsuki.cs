using NUnit.Framework;
using Unity.IO.LowLevel.Unsafe;
using System.Collections.Generic;
using UnityEngine;

public class Obj_Osumitsuki : MonoBehaviour
{

    [Header("インクステータス")]
    //現在のインクの量
    protected float curInkAmount = 0;
    [SerializeField] private float maxInkCapa = 100;      //インクの最大量
    [SerializeField] private float InkRatio = 70;   //お墨付き

    [Header("お墨付き後のテクスチャ")]
    [SerializeField] private Material myMaterial;

    [Header("お助け用オブジェクト（PlayerからAllyEnemy情報を取得する方法に変えたい）")]
    [SerializeField] private List<Transform> allyEnemyTargetPos = new List<Transform>();
    [SerializeField] private AllyEnemyManager allyEnemyManager;

    private bool osumitsukiTrg = false; //お墨付きした時にtrueへ
    private bool osumitsukiFlg = false; //Action_Osumitsuki後にtrueへ
    private bool endFlg = false;        //終了フラグ


    //プロパティ
    public bool OsumiTrg => osumitsukiTrg;
    public bool OsumiFlg => osumitsukiFlg;  //お墨付きかどうか
    public bool EndFlg => endFlg;           //処理が終了したかどうか


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

        if (InkRatio/100f <= curInkAmount / maxInkCapa && !osumitsukiTrg)
        {
			gameObject.layer = LayerMask.NameToLayer("PlayerVSObject");
			GetComponent<MeshRenderer>().material = myMaterial;
            osumitsukiTrg = true;
            Mng_Osumitsuki.instance.AddObject(this);

            int childrenCount = transform.childCount;
            if (childrenCount > 0)
            {
			    GameObject[] childrenObj = new GameObject[childrenCount];
			    for (int i = 0; i < childrenCount; i++)
                {
                    Transform chiledTransform = transform.GetChild(i);
                    childrenObj[i] = chiledTransform.gameObject;
			    }
                DestroyInkCollider(childrenObj);
            }
		}

        return osumitsukiTrg;
    }

    public void Action2Update()
    {
        osumitsukiFlg = true;
    }

    public virtual void End()
    {
        endFlg = true;
    }


    /**
    * @brief    お墨付き前についているインク当たり判定を削除
    * @param    GameObject[]    _gameObjects    子要素配列
    */ 
    private void DestroyInkCollider(GameObject[] _gameObjects)
    {

        int childrenCount = _gameObjects.Length;
        Collider[] colliders = new Collider[childrenCount];
        for (int i =0; i < childrenCount; i++)
        { 
            colliders[i] = _gameObjects[i].GetComponent<Collider>();

			if (colliders[i].gameObject.name == $"{gameObject.name}_InkCollision")
			    Destroy(colliders[i].gameObject);
		}

        for (int i = 0;  i < colliders.Length; i++)
        {
            var collider = colliders[i];
			collider.gameObject.layer = LayerMask.NameToLayer("PlayerVSObject");
            GameObject grandChild = collider.gameObject.transform.GetChild(0).gameObject;

			if (grandChild.name == $"{collider.gameObject.name}_InkCollision")
                Destroy(grandChild);
        }
    }


    /**
    * @brief    プレイヤーからAllyEnemyを参照して保持する
    */
    void HelperEnemy()
    {

    }


}
