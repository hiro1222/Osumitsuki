using UnityEngine;

public class Boss_Door : MonoBehaviour
{

	[Header("ドア（開く対象）")]
	[SerializeField] private Transform leftDoor;
	[SerializeField] private Transform rightDoor;

	[Header("開く動き")]
	[SerializeField] private float leftOpenAngle = -90f;
	[SerializeField] private float rightOpenAngle = 90f;
	[SerializeField] private float openSpeed = 2f;
	private Quaternion leftOpenRot;
	private Quaternion rightOpenRot;


	[Header("ターゲット")]
    [SerializeField] Transform targetTransform;


    [Header("開き初め調整パラメータ")]
    [SerializeField] private Vector3 offset;
    [SerializeField] private Color color = Color.red;
	[SerializeField] private float radius = 2;

	[Header("お墨付きテクスチャ")]
    [SerializeField] private Material afferMat;

	[Header("アクティブ")]
	[SerializeField] private bool enabled = false;

	private bool flg = false;


	void SetEnabled(bool _on) { enabled = _on; }

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		if (targetTransform == null)
			targetTransform = GameObject.Find("player_v3").transform;

		leftOpenRot = leftDoor.localRotation * Quaternion.Euler(0, leftOpenAngle, 0);
		rightOpenRot = rightDoor.localRotation * Quaternion.Euler(0, rightOpenAngle, 0);
	}


	private void FixedUpdate()
	{
		if (!enabled) return;

        Vector3 dif = targetTransform.position - (transform.position + offset);

        if (dif.sqrMagnitude < radius * radius && !flg)
        {
			Transform[] children = GetComponentsInChildren<Transform>();

			foreach (Transform child in children)
			{
				if (transform == child) continue;
				Renderer rend = child.GetComponent<Renderer>();
				if (rend == null) continue;
				rend.material = afferMat;
			}

            flg = true;
        }

        if (flg) OpenDoor();
    }


    private void OpenDoor()
    {
		bool leftDone = true;
		bool rightDone = true;

		if (leftDoor != null)
		{
			leftDoor.localRotation = Quaternion.Slerp(
				leftDoor.localRotation, leftOpenRot,
				openSpeed * Time.deltaTime);
			leftDone = Quaternion.Angle(leftDoor.localRotation, leftOpenRot) < 0.5f;
		}
		if (rightDoor != null)
		{
			rightDoor.localRotation = Quaternion.Slerp(
				rightDoor.localRotation, rightOpenRot,
				openSpeed * Time.deltaTime);
			rightDone = Quaternion.Angle(rightDoor.localRotation, rightOpenRot) < 0.5f;
		}
	}


	private void OnDrawGizmos()
	{
		Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position + offset, radius);
	}
}
