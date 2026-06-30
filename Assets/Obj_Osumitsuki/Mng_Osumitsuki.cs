using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;



public class Mng_Osumitsuki : MonoBehaviour
{
    public static Mng_Osumitsuki instance { get; private set; }

    private List<Obj_Osumitsuki> all_Objects;
    private List<bool> all_ObjectsOsumiFlg;
    private List<Obj_Osumitsuki> action_Objects;
    private List<Obj_Osumitsuki> update_Objects;

    private AudioSource audioSource;
    private int idCnt = 0;
    private int flameCnt = 0;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
		all_Objects = new List<Obj_Osumitsuki>();
        all_ObjectsOsumiFlg = new List<bool>();
		action_Objects = new List<Obj_Osumitsuki>();
		update_Objects = new List<Obj_Osumitsuki>();
		DontDestroyOnLoad(gameObject);  //お墨付きオブジェクトのみを必要とすることがあるかも？
        audioSource = GetComponent<AudioSource>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    private void FixedUpdate()
    {
        action_Objects.RemoveAll(obj => obj.OsumiFlg || obj.EndFlg);
        update_Objects.RemoveAll(obj => obj.EndFlg);

        foreach (Obj_Osumitsuki obj in update_Objects)
        {
            obj.Update_Osumitsuki_Cover();
        }

        foreach (Obj_Osumitsuki obj in action_Objects)
        {
            obj.Action_Osumitsuki_Cover();

            if (obj.OsumiFlg)
                update_Objects.Add(obj);
        }

        flameCnt++;
    }


    public void AddObject(Obj_Osumitsuki _obj)
    {
        var ansObject = action_Objects.Find(obj => obj.name == _obj.name);
        if (ansObject != null) return;

        if (all_ObjectsOsumiFlg[_obj.OsumiID] == false)
        {
            all_ObjectsOsumiFlg[_obj.OsumiID] = true;
            audioSource.Play();
        }

        action_Objects.Add(_obj);
        Debug.Log(_obj.name + "、Osumitsuki!!");
    }

    public void AddAllList(Obj_Osumitsuki _obj)
    {
        _obj.SetID(idCnt);
        idCnt++;
        all_Objects.Add(_obj);
        all_ObjectsOsumiFlg.Add(false);
    }

    public void AllOsumitsuki()
    {
        foreach (Obj_Osumitsuki obj in all_Objects)
        {
            obj.StopAuraEffect();
            obj.Osumitsuki_Tex();
        }
    }

    public void AllClear()
    {
        all_Objects.Clear();
        all_ObjectsOsumiFlg.Clear();
        action_Objects.Clear();
        update_Objects.Clear();
    }

}
