using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class sl : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

	private void FixedUpdate()
	{
        if (Input.GetKeyUp(KeyCode.P))
        {
            SceneManager.LoadScene("Stage_B_light");
        }
	}
}
