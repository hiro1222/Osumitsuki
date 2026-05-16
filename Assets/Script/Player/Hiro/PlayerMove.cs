using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private PlayerController controller;
    private CharacterController characterController;
    private Transform tf;

    [Header("Move Settings")]
    [SerializeField] private float rotateSpeed = 12.0f;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;

    private Vector3 velocity;
    private bool jumpRequestedThisFrame;

    private bool externalPositionLock;
    private bool externalGravityEnabled = true;
    private bool lockHeightOnly;
    private Vector3 lockedPosition;

    public bool IsGrounded { get; private set; }
    public float VerticalVelocity => velocity.y;

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        characterController = owner.CharacterController;
        tf = transform;
    }

    public void Tick()
    {
        UpdateMove();
    }

    public void FaceCameraDirectionInstant()
    {
        if (cameraTransform == null) return;

        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0.0f;

        if (camForward.sqrMagnitude <= 0.0001f) return;

        camForward.Normalize();
        tf.rotation = Quaternion.LookRotation(camForward, Vector3.up);
    }

    public void SetExternalPositionLock(bool enabled, Vector3 position, bool heightOnly)
    {
        externalPositionLock = enabled;
        lockedPosition = position;
        lockHeightOnly = heightOnly;
    }

    public void SetExternalGravityEnabled(bool enabled)
    {
        externalGravityEnabled = enabled;
    }

    public void ClearVerticalVelocity()
    {
        velocity.y = 0.0f;
    }

    private void UpdateMove()
    {
        PlayerStats stats = controller.Stats;

        jumpRequestedThisFrame = false;
        IsGrounded = characterController.isGrounded;

        if (IsGrounded && velocity.y < 0.0f)
        {
            velocity.y = stats.groundedY;
        }

        Vector2 moveInput = controller.InputHandler.MoveInput;
        Vector3 move = BuildCameraRelativeMove(moveInput);

        if (move.sqrMagnitude > 1.0f)
        {
            move.Normalize();
        }

        float speed = stats.walkSpeed * controller.ActionManager.CurrentMoveSpeedRate;

        if (move.sqrMagnitude > 0.0001f &&
            controller.ActionManager.CurrentMoveSpeedRate > 0.0f &&
            (!controller.ActionManager.IsActing ||
              controller.ActionManager.IsNazori))
        {
            Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
            tf.rotation = Quaternion.Slerp(tf.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }

        if (controller.InputHandler.JumpPressed &&
            IsGrounded &&
            !controller.ActionManager.IsActing)
        {
            velocity.y = stats.jumpPower;
            jumpRequestedThisFrame = true;
            IsGrounded = false;
        }

        if (externalGravityEnabled)
        {
            velocity.y += stats.gravity * Time.deltaTime;
        }
        else
        {
            velocity.y = 0.0f;
        }

        Vector3 finalMove = move * speed;
        finalMove.y = velocity.y;

        characterController.Move(finalMove * Time.deltaTime);

        if (externalPositionLock)
        {
            Vector3 pos = transform.position;

            if (lockHeightOnly)
            {
                pos.y = lockedPosition.y;
            }
            else
            {
                pos = lockedPosition;
            }

            transform.position = pos;
        }

        if (!jumpRequestedThisFrame)
        {
            IsGrounded = characterController.isGrounded;
        }
    }

    private Vector3 BuildCameraRelativeMove(Vector2 moveInput)
    {
        Transform reference = cameraTransform != null ? cameraTransform : tf;

        Vector3 forward = reference.forward;
        Vector3 right = reference.right;

        forward.y = 0.0f;
        right.y = 0.0f;

        if (forward.sqrMagnitude > 0.0001f) forward.Normalize();
        if (right.sqrMagnitude > 0.0001f) right.Normalize();

        return forward * moveInput.y + right * moveInput.x;
    }
}