using UnityEngine;
using UnityEngine.Video;

public class VideoController : MonoBehaviour
{
	[SerializeField] private VideoPlayer player;

	void Start()
	{
		player.Play();
	}

	public void StopVideo()
	{
		player.Stop();
	}
}