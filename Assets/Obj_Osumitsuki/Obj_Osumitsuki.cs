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
	[SerializeField] private float coolPaintTime = 0.5f;     //インクが塗られたときのクールタイム
	private float lastPaintedTime = 0;                     //最後に塗られた時間

	[Header("お墨付き後のテクスチャ")]
	[SerializeField] protected Material myMaterial;

	[Header("お助け用オブジェクト")]
	[SerializeField] private Transform[] allyEnemyTarget;    //AllyEnemy目標座標
	[SerializeField] private AllyEnemyManager allyEnemyManager;

	[SerializeField] private GameObject prefab_aura;
	[SerializeField] private GameObject prefab_flash;
	[SerializeField] private Vector3 offset_Aura;
	[SerializeField] private Vector3 scale_Aura = new Vector3(5, 5, 5);
	[SerializeField] private Vector3 offset_Flash;
	[SerializeField] private Vector3 scale_Flash = new Vector3(8, 8, 8);

	 private ParticleSystem auraEffect;
	 private ParticleSystem flashEffect;

	private AllyEnemy[] helperAllyEnemys;   //Osumitsuki_Objがお墨付き後移動補助やく墨袋
	private AllyEnemy.IAllyEnemyState[] helperEnemyStates;

	protected bool osumitsukiTrg = false;	//お墨付きした時にtrueへ
	protected bool osumitsukiFlg = false;	//Action_Osumitsuki後にtrueへ
	protected bool endFlg = false;			//終了フラグ
	protected bool firstSearchFlg = false;  //検索フラグ

    protected List<MaskedInkProgress> maskSystems;
	private PaintableSurface paintableSurface;


	public event System.Action<int, byte> OnAnyPainted;

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



	protected virtual void Awake()
	{
		paintableSurface = GetComponent<PaintableSurface>();
		if (paintableSurface == null)
		{
			Debug.LogError("Obj_OsumitsukiにPaintableSurfaceがアタッチされていません。");
		}

		//paintableSurface.OnPainted += (cells, density) => PaintedRaper(cells,density);

		if (allyEnemyTarget != null)
		{
			helperAllyEnemys = new AllyEnemy[allyEnemyTarget.Length];
			helperEnemyStates = new AllyEnemy.IAllyEnemyState[allyEnemyTarget.Length];
		}

        maskSystems = new List<MaskedInkProgress>();
        Transform[] allTransforms = GetComponentsInChildren<Transform>();
        foreach (Transform t in allTransforms)
        {
            var maskS = t.gameObject.GetComponent<MaskedInkProgress>();
            if (maskS != null)
                maskSystems.Add(maskS);
        }

		SpawnAuraEffect();
    }

	public void Action_Osumitsuki_Cover()
	{
		SearchOsumitsuki_Obj();
		Action_Osumitsuki();
	}
	public void Update_Osumitsuki_Cover()
	{
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

	private void PaintedRaper(int _cells,int _density)
	{
		Debug.Log("///////////alkdjfoa;isjhefjklnaisdf@ooafd");
		Painted(5f);
	}


	//塗られたときの処理
	public bool Painted(float _ink)
	{
		if (Time.time - lastPaintedTime < coolPaintTime) return osumitsukiTrg;
		lastPaintedTime = Time.time;

		Debug.Log("AddInkAmount : " + _ink);
		curInkAmount += _ink;
		Debug.Log("--------------------------------------------------");
		Debug.Log(name + "Painted関数呼び出し");
		Debug.Log("--------------------------------------------------");

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
                for (int i = 0; i < all.Count; i++)
                {
                    all[i].gameObject.layer = LayerMask.NameToLayer("PlayerVSObject");
                }
            }
			Debug.Log("la;knv;uajnesp;ivna;ilkvn;ailKNl/iaknedF?LIckＥＤｆ");
			StopAuraEffect();
			SpawnFlashEffect();

			return osumitsukiTrg;
		}

		if (maskSystems.Count > 0)
		{
			float curRatio = curInkAmount / maxInkCapa;

			foreach (MaskedInkProgress ms in maskSystems)
            {
                float curBlock = ms.CurrentStep + 1;
                float allBlockNum = ms.StepCount;
				float oneBlockAmount = maxInkCapa / allBlockNum;
				float curBlockTopAmount = oneBlockAmount * curBlock;

				Debug.Log("maskNum" + maskSystems.Count);
				Debug.Log("curRatio : " + curRatio * 100f + "％");
				Debug.Log("allBlockNum : " + allBlockNum);
				Debug.Log("oneBlcokAmount : " + oneBlockAmount);
				Debug.Log("curBlockIndex : " + curBlock);
				Debug.Log("curInkAmount : curBlockTopAmount =" + curInkAmount + " : " + curBlockTopAmount);

				if (curInkAmount >= curBlockTopAmount)
                {
					Debug.Log(name + " : 次マスクへ");
					ms.Advance();
				}

            }
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

        if (helperAllyEnemys == null) return;
        if (allyEnemyTarget == null) return;

        for (int i = 0; i < helperAllyEnemys.Length; i++)
        {
            if (helperAllyEnemys[i] != null)
                helperAllyEnemys[i].ClearExternalState();
        }
		if (allyEnemyTarget.Length > 0)
		{
			helperAllyEnemys = new AllyEnemy[allyEnemyTarget.Length];
			helperEnemyStates = new AllyEnemy.IAllyEnemyState[allyEnemyTarget.Length];
		}
	}

	/**
    * @brief    プレイヤーからAllyEnemyを参照して保持する
    */
	protected void SearchOsumitsuki_Obj()
	{
		//目標座標がなければ終了
		if (allyEnemyTarget.Length == 0) return;
        if (allyEnemyManager == null) return;

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

	private void SpawnAuraEffect()
	{
		if (prefab_aura != null)
		{
			GameObject instance = Instantiate(prefab_aura, transform);
			instance.transform.position += offset_Aura;
			instance.transform.localScale += scale_Aura;

			auraEffect = instance.GetComponent<ParticleSystem>();
		}
	}
	private void StopAuraEffect()
	{
		if (auraEffect != null)
			auraEffect.Stop();
	}

	private void SpawnFlashEffect()
	{
		if (prefab_flash != null)
		{
			GameObject instance = Instantiate(prefab_flash, transform);
			instance.transform.position += offset_Flash;
			instance.transform.localScale += scale_Flash;

			flashEffect = instance.GetComponent<ParticleSystem>();
		}
	}

	private void StopParticle(ParticleSystem ritPS)
	{
		ritPS.Stop();
		Debug.Log("ああああああああああい；じゃお；いｓｈｄなｄんふぃぁｋｊｓｍｆぃかｊｄｆ");
	}

	/**
    * @brief    お助けエネミーを開放する
    */
	private void ReleaseHelperEnemy()
	{
		End();
	}

}

