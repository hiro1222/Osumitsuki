using UnityEngine;

public class Test_Osumitsuki : Obj_Osumitsuki
{
    private float time = 0;


    public override void Action_Osumitsuki()
    {
        Debug.Log((int)(time/100) + "ïb");
        time += Time.deltaTime;

        if (time > 800f)
            Action2Update();
    }

    public override void Update_Osumitsuki()
    {
        Debug.Log("UpdateÅF" + gameObject.name);
    }

    private void FixedUpdate()
    {
        Painted(0.1f);
    }
}
