using UnityEditor.SceneManagement;
using UnityEngine;

public class Stairs_Osumitsuki : Movement_Osumitsuki
{


    private void Start()
    {
        SetupBaseData();
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
