using UnityEngine;

public class Stairs_Osumitsuki : Movement_Osumitsuki
{

	private void Start()
    {

        SetupBaseData();
        if (Mng_Osumitsuki.instance == null)
        {
            Debug.Log("Instance‚ªNULL‚Å‚·");
        }
        else
        {
            Mng_Osumitsuki.instance.AddAllList(this);
        }
    }

    public override void Action_Osumitsuki()
    {
		Action2Update();
    }

    public override void Update_Osumitsuki()
    {
		Update_RotateMove();
    }

}
