using UnityEngine;

public class Goal_Osumitsuki : Obj_Osumitsuki
{

	[SerializeField] private CutsceneSequencer cutscene;


	public override void Action_Osumitsuki()
	{
		if (cutscene != null) cutscene.Play();
	}


}
