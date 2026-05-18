using UnityEngine;

public class Test_Osumitsuki : Obj_Osumitsuki
{
    private float time = 0;


    public override void Action_Osumitsuki()
    {
        time += Time.deltaTime;

        if (time > 800f)
            Action2Update();
    }

    public override void Update_Osumitsuki()
    {}

    private void FixedUpdate()
    {
        Painted(0.1f);
    }
}
