using UnityEngine;

public class Obj_Osumitsuki : MonoBehaviour
{

<<<<<<< HEAD
    [Header("インクステータス")]
    //現在のインクの量
    private float curInkAmount;
    [SerializeField] private float maxInkCapa = 100;      //インクの最大量
    [SerializeField] private float InkRatio = 70;   //お墨付き

    private bool osumitsukiFlg;
=======

    
    [SerializeField] private float InkRatio = 50;


>>>>>>> fe7106dfbefcf6e06ec7fbc76d83114d5b2a8b83



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
<<<<<<< HEAD
        curInkAmount = 0;
        osumitsukiFlg = false;
=======
        
>>>>>>> fe7106dfbefcf6e06ec7fbc76d83114d5b2a8b83
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
