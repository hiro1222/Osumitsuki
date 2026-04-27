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

        if (move.sqrMagnitude > 0.0001f && controller.ActionManager.CurrentMoveSpeedRate > 0.0f)
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

        velocity.y += stats.gravity * Time.deltaTime;

        Vector3 finalMove = move * speed;
        finalMove.y = velocity.y;

        characterController.Move(finalMove * Time.deltaTime);

        // ジャンプ入力直後の1フレームで、CharacterControllerがまだ接地扱いを返すことがある。
        // そのフレームだけは空中扱いを維持してjumpアニメを即開始させる。
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
