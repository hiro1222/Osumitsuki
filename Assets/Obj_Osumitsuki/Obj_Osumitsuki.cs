using UnityEngine;

public class Obj_Osumitsuki : MonoBehaviour
{

    [Header("インクステータス")]
    //現在のインクの量
    private float curInkAmount;
    [SerializeField] private float maxInkCapa = 100;      //インクの最大量
    [SerializeField] private float InkRatio = 70;   //お墨付き

    private bool osumitsukiFlg;




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        curInkAmount = 0;
        osumitsukiFlg = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
