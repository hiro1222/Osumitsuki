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

	[Header("お墨付き後のテクスチャ")]
	[SerializeField] protected Material myMaterial;

	[Header("お助け用オブジェクト")]
	[SerializeField] private Transform[] allyEnemyTarget;    //AllyEnemy目標座標
	[SerializeField] private AllyEnemyManager allyEnemyManager;

	private AllyEnemy[] helperAllyEnemys;   //Osumitsuki_Objがお墨付き後移動補助やく墨袋
	private AllyEnemy.IAllyEnemyState[] helperEnemyStates;

	protected bool osumitsukiTrg = false;	//お墨付きした時にtrueへ
	protected bool osumitsukiFlg = false;	//Action_Osumitsuki後にtrueへ
	protected bool endFlg = false;			//終了フラグ
	protected bool firstSearchFlg = false;  //検索フラグ

	private MaskedInkProgress maskSys;

	//プロパティ
	public bool OsumiTrg => osumitsukiTrg;
	public bool OsumiFlg => osumitsukiFlg;  //お墨付きかどうか
	public bool EndFlg => endFlg;           //処理が終了したかどうか
	
	
	public int GetHelperNum()
	{
		int answer = 0;

		for (int i = 0; i < allyEnemyTarget.Length; i++)
		{
			if (helperAllyEnemys[i] != null)
				answer++;
		}

		return answer;
	}
	public void SetEndFlg(bool _is) { endFlg = _is; }


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
			switch (state)
			{
				case AllyEnemy_Base_ObjOsumi_State.CHASER: Chase_Update(owner, dt); return true;
				case AllyEnemy_Base_ObjOsumi_State.HELPER: Help_Update(owner, dt); return true;
				case AllyEnemy_Base_ObjOsumi_State.END: owner.ClearExternalState(); return true;
			}

			return false;
		}

		/// <summary>ステート終了時に1回呼ばれる</summary>
		public void OnExit(AllyEnemy owner)
		{
		}

		private void Chase_Update(AllyEnemy _owner, float _deltaTime)
		{
			if (target == null) return;
			_owner.transform.position = Vector3.MoveTowards(
			_owner.transform.position,
			target.position,
			speed * _deltaTime
			);

			if (Vector3.Distance(_owner.transform.position, target.position) < 0.01f)
				state = AllyEnemy_Base_ObjOsumi_State.HELPER;
		}
		private void Help_Update(AllyEnemy _owner, float _deltaTime)
		{
			_owner.transform.position = target.position;
			_owner.transform.rotation = target.rotation;
		}
	}



	private void Awake()
	{
		if (allyEnemyTarget != null)
		{
			helperAllyEnemys = new AllyEnemy[allyEnemyTarget.Length];
			helperEnemyStates = new AllyEnemy.IAllyEnemyState[allyEnemyTarget.Length];
		}

		maskSys = GetComponent<MaskedInkProgress>();
		if (maskSys == null)
		{
			Debug.Log(name + "：MaskedInkProgress.csがないです。動的塗りを適用");
		}
	}

	public void Action_Osumitsuki_Cover()
	{
		Debug.Log(gameObject.name + "：お墨付きアクション");
		SearchOsumitsuki_Obj();
		Action_Osumitsuki();
	}
	public void Update_Osumitsuki_Cover()
	{
		Debug.Log(gameObject.name + "：お墨付きアップデート");
		Update_Osumitsuki();
	}

	//お墨付き時のアクション
	public virtual void Action_Osumitsuki()
	{
		Action2Update();
	}
	//Action_Osumitsuki後にマイフレーム更新
	public virtual void Update_Osumitsuki()
	{
		End();
	}

	//塗られたときの処理
	public bool Painted(float _ink)
	{
		curInkAmount += _ink;

		if (maxInkCapa <= curInkAmount && !osumitsukiTrg)
		{
			//本来のマテリアルに変更
			var meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer != null)
				meshRenderer.material = myMaterial;

			//お墨付きマネージャーに渡す
			osumitsukiTrg = true;
			Mng_Osumitsuki.instance.AddObject(this);

			gameObject.layer = LayerMask.NameToLayer("PlayerVSObject");
			//インクコライダーを削除する
			var all = new List<Transform>();
			GetAllChildren(transform, all);
			if (all.Count > 0)
			{
				GameObject[] childrenObj = new GameObject[all.Count];
				for (int i = 0; i < all.Count; i++)
				{
					childrenObj[i] = all[i].gameObject;
					all[i].gameObject.layer = LayerMask.NameToLayer("PlayerVSObject");
				}
				DestroyInkCollider(childrenObj);
			}

			return osumitsukiTrg;
		}

		if (maskSys != null)
		{
			float curRatio = curInkAmount / maxInkCapa;
			float curStep = maskSys.CurrentStep + 1;
			int numStep = 3 + 1;

			float curStepInkAmount = maxInkCapa / numStep * curStep;

			if (curInkAmount >= curStepInkAmount)
				maskSys.AdvanceBy(1);
		}

		return osumitsukiTrg;
	}

	void GetAllChildren(Transform _parent, List<Transform> _result)
	{
		foreach (Transform child in _parent)
		{
			_result.Add(child);
			GetAllChildren(child, _result); // 再帰
		}
	}

	public bool Action2Update()
	{
		//AllyEnemyの助けが不必要
		if (allyEnemyTarget.Length == 0)
		{
			osumitsukiFlg = true;
			return osumitsukiFlg;
		}

		//AllyEnemyの助けの数が足りていない
		if (helperAllyEnemys.Length < allyEnemyTarget.Length) return false;


		//到着しているお助け墨袋をカウント
		int arrivalCnt = 0;
		for (int i = 0; i < helperEnemyStates.Length; i++)
		{
			if (helperEnemyStates[i] == null) break;
			AllyEnemy_Func_Base_Obj_Osumitsuki func = (AllyEnemy_Func_Base_Obj_Osumitsuki)helperEnemyStates[i];

			if (func.GetState() == AllyEnemy_Func_Base_Obj_Osumitsuki.AllyEnemy_Base_ObjOsumi_State.HELPER)
				arrivalCnt++;
		}

		//全て到着していたら
		if (arrivalCnt == helperEnemyStates.Length)
			osumitsukiFlg = true;

		return osumitsukiFlg;
	}

	public virtual void End()
	{
		endFlg = true;

		if (helperAllyEnemys == null)
			return;

		for (int i = 0; i < helperAllyEnemys.Length; i++)
			helperAllyEnemys[i].ClearExternalState();

		if (allyEnemyTarget != null)
		{
			helperAllyEnemys = new AllyEnemy[allyEnemyTarget.Length];
			helperEnemyStates = new AllyEnemy.IAllyEnemyState[allyEnemyTarget.Length];
		}
	}


	/**
    * @brief    お墨付き前についているインク当たり判定を削除
    * @param    GameObject[]    _gameObjects    子要素配列
    */
	private void DestroyInkCollider(GameObject[] _gameObjects)
	{

		int childrenCount = _gameObjects.Length;
		Collider[] colliders = new Collider[childrenCount];
		for (int i = 0; i < childrenCount; i++)
		{
			colliders[i] = _gameObjects[i].GetComponent<Collider>();

			if (colliders[i].gameObject.name == $"{gameObject.name}_InkCollision")
				Destroy(colliders[i].gameObject);
		}

		for (int i = 0; i < colliders.Length; i++)
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
	protected void SearchOsumitsuki_Obj()
	{
		//目標座標がなければ終了
		if (allyEnemyTarget.Length == 0) return;

		IReadOnlyList<AllyEnemy> allyEnemys = allyEnemyManager.GetAllyEnemy();
		//AllyEnemyがいなければ終了
		if (allyEnemys.Count == 0) return;

		//お助け墨袋
		int cnt = 0;
		for (int i = 0; i < helperAllyEnemys.Length; i++)
		{
			if (helperAllyEnemys[i] != null) cnt++;
		}
		if (cnt >= allyEnemyTarget.Length) return;

		//まだ空の目標座標を検索して、AllyEnemyを割り当てる
		for (int i = 0; i < allyEnemyTarget.Length; i++)
		{
			if (helperEnemyStates[i] != null) continue;
			if (allyEnemys.Count <= i) break;

			AllyEnemy.IAllyEnemyState newState = new AllyEnemy_Func_Base_Obj_Osumitsuki();
			allyEnemys[i].SetExternalState(newState);
			helperEnemyStates[i] = newState;
			helperAllyEnemys[i] = allyEnemys[i];
			Transform targetTrf = allyEnemyTarget[i].transform;

			AllyEnemy_Func_Base_Obj_Osumitsuki func = (AllyEnemy_Func_Base_Obj_Osumitsuki)newState;
			func.SetTarget(targetTrf);
		}
	}

	/**
    * @brief    お助けエネミーを開放する
    */
	private void ReleaseHelperEnemy()
	{
		End();
	}

}

