using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;



public class Mng_Osumitsuki : MonoBehaviour
{
    public static Mng_Osumitsuki instance { get; private set; }

    private List<Obj_Osumitsuki> action_Objects;
    private List<Obj_Osumitsuki> update_Objects;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);  //お墨付きオブジェクトのみを必要とすることがあるかも？
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        action_Objects = new List<Obj_Osumitsuki>();
        update_Objects = new List<Obj_Osumitsuki>();
    }

    // Update is called once per frame
    void Update()
    {
        action_Objects.RemoveAll(obj => obj.OsumiFlg == true);

        foreach (Obj_Osumitsuki obj in update_Objects)
        {
            obj.Update_Osumitsuki();
        }

        foreach (Obj_Osumitsuki obj in action_Objects)
        {
            obj.Action_Osumitsuki();

            if (obj.OsumiFlg)
                update_Objects.Add(obj);
        }
    }


        public void AddObject(Obj_Osumitsuki _obj)
        {
            action_Objects.Add(_obj);
            Debug.Log(_obj.name + "、Osumitsuki!!");
        }
}
