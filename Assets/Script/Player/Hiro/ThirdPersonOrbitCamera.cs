using UnityEngine;

public class ThirdPersonOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target; // 位置・距離・回転軸の基準。基本Player

    [Header("Look Target")]
    [SerializeField] private Transform lookTarget; // 向きだけ追従する対象
    [SerializeField] private bool useLookTarget = false;
    [SerializeField] private float targetLookHeight = 1.0f;
    [SerializeField] private float otherLookHeight = 1.0f;

    [Header("Distance")]
    [SerializeField] private float distance = 4.0f;
    [SerializeField] private float minDistance = 2.0f;
    [SerializeField] private float maxDistance = 8.0f;

    [Header("Collision")]
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float collisionOffset = 0.2f;

    [Header("Rotation")]
    [SerializeField] private float yaw;
    [SerializeField] private float pitch = 20.0f;
    [SerializeField] private float minPitch = -80.0f;
    [SerializeField] private float maxPitch = 80.0f;
    [SerializeField] private float lookSensitivity = 0.08f;

    [Header("Options")]
    [SerializeField] private bool lockCursor = true;
    [SerializeField] private bool invertY = false;

    public Vector3 CameraForwardFlat
    {
        get
        {
            Vector3 f = transform.forward;
            f.y = 0.0f;
            if (f.sqrMagnitude > 0.0001f) f.Normalize();
            return f;
        }
    }

    private void Start()
    {
        if (target != null)
        {
            Vector3 e = transform.eulerAngles;
            yaw = e.y;
            pitch = NormalizePitch(e.x);
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        UpdateRotation();
        UpdatePosition();
    }

    private void UpdateRotation()
    {
        Vector2 look = ReadLookInput();

        yaw += look.x * lookSensitivity;

        float yInput = invertY ? look.y : -look.y;
        pitch += yInput * lookSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdatePosition()
    {
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0.0f);

        // ここは絶対Player基準のまま
        Vector3 targetPos = target.position + Vector3.up * targetLookHeight;

        Vector3 desiredOffset = rot * new Vector3(0.0f, 0.0f, -distance);
        Vector3 desiredPos = targetPos + desiredOffset;

        Vector3 dir = desiredPos - targetPos;
        float dist = dir.magnitude;

        if (dist > 0.0001f)
        {
            dir.Normalize();

            if (Physics.SphereCast(
                targetPos,
                collisionRadius,
                dir,
                out RaycastHit hit,
                dist,
                cameraCollisionMask,
                QueryTriggerInteraction.Ignore))
            {
                float fixedDist = Mathf.Max(minDistance, hit.distance - collisionOffset);
                desiredPos = targetPos + dir * fixedDist;
            }
        }

        // 向きだけ別ターゲットにできる
        Vector3 lookPos = GetLookPosition(targetPos);

        transform.position = desiredPos;
        transform.LookAt(lookPos);
    }

    private Vector3 GetLookPosition(Vector3 defaultLookPos)
    {
        if (useLookTarget && lookTarget != null)
        {
            return lookTarget.position + Vector3.up * otherLookHeight;
        }

        return defaultLookPos;
    }

    public void SetLookTarget(Transform newLookTarget)
    {
        lookTarget = newLookTarget;
        useLookTarget = lookTarget != null;
    }

    public void ClearLookTarget()
    {
        lookTarget = null;
        useLookTarget = false;
    }

    private Vector2 ReadLookInput()
    {
        Vector2 look = Vector2.zero;

        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            look += UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
        }

        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            Vector2 stick = UnityEngine.InputSystem.Gamepad.current.rightStick.ReadValue();
            if (stick.sqrMagnitude > 0.0001f)
            {
                look += stick * 15.0f;
            }
        }

        return look;
    }

    private float NormalizePitch(float xAngle)
    {
        if (xAngle > 180.0f) xAngle -= 360.0f;
        return xAngle;
    }
}