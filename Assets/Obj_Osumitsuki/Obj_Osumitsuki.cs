using NUnit.Framework;
using Unity.IO.LowLevel.Unsafe;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class Obj_Osumitsuki : MonoBehaviour
{

    [Header("インクステータス")]
    //現在のインクの量
    protected float curInkAmount = 0;
    [SerializeField] private float maxInkCapa = 100;    //インクの最大量
    [SerializeField] private float InkRatio = 70;       //お墨付き

    [Header("お墨付き後のテクスチャ")]
    [SerializeField] private Material myMaterial;

    [Header("お助け用オブジェクト（PlayerからAllyEnemy情報を取得する方法に変えたい）")]
    [SerializeField] private Transform[] allyEnemyTargetPos = null;    //AllyEnemy目標座標
    [SerializeField] private GameObject player;
    [SerializeField] private AllyEnemyManager allyEnemyManager;

    private AllyEnemy[] helperAllyEnemys = null;   //Osumitsuki_Objがお墨付き後移動補助やく墨袋
    private AllyEnemy.IAllyEnemyState[] helperEnemyStates = null;

	private bool osumitsukiTrg = false; //お墨付きした時にtrueへ
    private bool osumitsukiFlg = false; //Action_Osumitsuki後にtrueへ
    private bool endFlg = false;        //終了フラグ


    //プロパティ
    public bool OsumiTrg => osumitsukiTrg;
    public bool OsumiFlg => osumitsukiFlg;  //お墨付きかどうか
    public bool EndFlg => endFlg;           //処理が終了したかどうか


	private class AllyEnemy_Func_Base_Obj_Osumitsuki : AllyEnemy.IAllyEnemyState
    {
        public AllyEnemy_Base_ObjOsumi_State state;
        private Transform target;
        private float speed = 10f;
        
        public enum AllyEnemy_Base_ObjOsumi_State
        {
            CHASER,
            HELPER,
            END,
        }

        public void SetState(AllyEnemy_Base_ObjOsumi_State _state) { state = _state; }
        public void SetTarget(Transform _transform) { target = _transform; }
        public AllyEnemy_Base_ObjOsumi_State GetState() { return state; }

		/// <summary>ステート開始時に1回呼ばれる</summary>
		public void OnEnter(AllyEnemy owner)
        {
			state = AllyEnemy_Base_ObjOsumi_State.CHASER;
        }

		/// <summary>毎フレーム呼ばれる。falseを返すとステート終了→Followに戻る</summary>
		public bool OnTick(AllyEnemy owner, float dt)
        {
            Debug.Log("*****************************************************************");
			Debug.Log("*****************************************************************");
			Debug.Log("*****************************************************************");
			Debug.Log("*****************************************************************");
			Debug.Log("*****************************************************************");
			Debug.Log("*****************************************************************");
			Debug.Log("*****************************************************************");
			switch (state)
            {
                case AllyEnemy_Base_ObjOsumi_State.CHASER: Chase_Update(owner, dt); return false;
                case AllyEnemy_Base_ObjOsumi_State.HELPER: Help_Update(owner, dt); return false;
                case AllyEnemy_Base_ObjOsumi_State.END: owner.ClearExternalState(); return false;
                default: return true;
            }
        }

		/// <summary>ステート終了時に1回呼ばれる</summary>
		public void OnExit(AllyEnemy owner)
        {
        }

        private void Chase_Update(AllyEnemy _owner, float _deltaTime)
        {
            if (target == null) return;
            Debug.Log("11111111111111111111111111111111111111111111111111111");
            _owner.transform.position = Vector3.MoveTowards(
			_owner.transform.position,
            target.position,
            speed * _deltaTime
            );

            if (Vector3.Distance(_owner.transform.position, target.position) < 0.01f )
                state = AllyEnemy_Base_ObjOsumi_State.HELPER;
        }
        private void Help_Update(AllyEnemy _owner, float _deltaTime)
        {
            _owner.transform.position = target.transform.position;
            _owner.transform.rotation = target.transform.rotation;
        }
	}

	private void Awake()
	{
		allyEnemyManager = player.GetComponent<AllyEnemyManager>();
		helperAllyEnemys = new AllyEnemy[allyEnemyTargetPos.Length];
		helperEnemyStates = new AllyEnemy.IAllyEnemyState[allyEnemyTargetPos.Length];
	}

	public void Action_Osumitsuki_Cover()
    {
        Debug.Log("お墨付きアクション");
        SearchOsumitsuki_Obj();
        Action_Osumitsuki();
    }
    public void Update_Osumitsuki_Cover()
    {
        Debug.Log("お墨付きアップデート");
        Update_Osumitsuki();
    }

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
        //AllyEnemyの助けが不必要
        if (allyEnemyTargetPos == null)
        {
            osumitsukiFlg = true;
            return;
        }

        //AllyEnemyの助けが必要
        if (helperAllyEnemys == null) return;
        if (helperAllyEnemys.Length < 2) return;

        int cnt = 0;
        for (int i = 0; i < helperEnemyStates.Length; i++)
        {
            if (helperEnemyStates[i] == null) break;
            AllyEnemy_Func_Base_Obj_Osumitsuki func = (AllyEnemy_Func_Base_Obj_Osumitsuki)helperEnemyStates[i];

            if (func.GetState() == AllyEnemy_Func_Base_Obj_Osumitsuki.AllyEnemy_Base_ObjOsumi_State.HELPER)
                cnt++;
        }

        if (cnt == helperEnemyStates.Length)
            osumitsukiFlg = true;
    }

    public virtual void End()
    {
        for (int i = 0; i < helperAllyEnemys.Length; i++)
            helperAllyEnemys[i].ClearExternalState();
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
    private void SearchOsumitsuki_Obj()
    {
		//目標座標がなければ終了
		if (allyEnemyTargetPos == null) return;

        //お助け墨袋
        if (helperAllyEnemys.Length >= 2) return;

        Debug.Log("お墨付きオブジェクト、検索中");
		IReadOnlyList<AllyEnemy> allyEnemys = allyEnemyManager.GetAllyEnemy();

		//AllyEnemyがいなければ終了
		if (allyEnemys == null) return;

        //まだ空の目標座標を検索して、AllyEnemyを割り当てる
        for (int i = 0; i < allyEnemyTargetPos.Length; i++)
        {
            if (helperEnemyStates[i] != null) continue;
            if (allyEnemys.Count <= i) break;

            AllyEnemy.IAllyEnemyState newState = new AllyEnemy_Func_Base_Obj_Osumitsuki();
            allyEnemys[i].SetExternalState(newState);
			//helperEnemyStates[i] = newState;
            helperAllyEnemys[i] = allyEnemys[i];
            Transform helperTrf = helperAllyEnemys[i].transform;

			AllyEnemy_Func_Base_Obj_Osumitsuki func = (AllyEnemy_Func_Base_Obj_Osumitsuki)newState;
			func.SetTarget(helperTrf);
		}

        Debug.Log("検索完了、運びに移行");
	}


}
