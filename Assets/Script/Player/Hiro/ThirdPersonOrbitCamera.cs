using UnityEngine;

public class ThirdPersonOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Distance")]
    [SerializeField] private float distance = 4.0f;
    [SerializeField] private float minDistance = 2.0f;
    [SerializeField] private float maxDistance = 8.0f;

    [Header("Rotation")]
    [SerializeField] private float yaw;
    [SerializeField] private float pitch = 20.0f;
    [SerializeField] private float minPitch = -80.0f;
    [SerializeField] private float maxPitch = 80.0f;
    [SerializeField] private float lookSensitivity = 0.08f;

    [Header("Options")]
    [SerializeField] private bool lockCursor = true;
    [SerializeField] private bool invertY = false;

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
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0.0f);

        Vector3 offset = rot * new Vector3(0.0f, 0.0f, -distance);
        Vector3 cameraPos = target.position + offset;

        transform.position = cameraPos;
        transform.LookAt(target.position);
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