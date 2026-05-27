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

    [Header("Step / Ground Assist")]
    [Tooltip("地面に足がついている時に自動で乗り越えられる段差高さ")]
    [SerializeField] private float stepHeight = 0.35f;

    [Tooltip("空中時のstepOffset。基本0でOK")]
    [SerializeField] private float airStepHeight = 0.0f;

    [Tooltip("階段や小さい段差を降りる時、地面へ吸着する距離")]
    [SerializeField] private float groundSnapDistance = 0.45f;

    [Tooltip("接地を失ってもこの秒数以内ならまだ接地扱いにする")]
    [SerializeField] private float groundedGraceTime = 0.15f;

    [Tooltip("この下向き速度より遅い落下は落下モーション扱いにしにくくする")]
    [SerializeField] private float fallAnimationVelocityThreshold = -2.0f;

    [Tooltip("Ground Snapで使うRayの開始高さ")]
    [SerializeField] private float groundSnapRayStartHeight = 0.2f;

    [Tooltip("Ground Snapで判定するLayer")]
    [SerializeField] private LayerMask groundSnapMask = ~0;

    private Vector3 velocity;
    private bool jumpRequestedThisFrame;
    private float lastGroundedTime;

    private bool externalPositionLock;
    private bool externalGravityEnabled = true;
    private bool lockHeightOnly;
    private Vector3 lockedPosition;

    public bool IsGrounded { get; private set; }

    // アニメーションやStateMachine側はこっちを見るのがおすすめ
    public bool IsGroundedBuffered
    {
        get
        {
            return Time.time - lastGroundedTime <= groundedGraceTime;
        }
    }

    public bool ShouldPlayFallAnimation
    {
        get
        {
            return !IsGroundedBuffered &&
                   velocity.y <= fallAnimationVelocityThreshold;
        }
    }

    public float VerticalVelocity => velocity.y;

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        characterController = owner.CharacterController;
        tf = transform;

        if (characterController != null)
        {
            characterController.stepOffset = stepHeight;
        }

        lastGroundedTime = Time.time;
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

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
        }

        ApplyStepOffset();

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
            tf.rotation = Quaternion.Slerp(
                tf.rotation,
                targetRot,
                rotateSpeed * Time.deltaTime
            );
        }

        if (controller.InputHandler.JumpPressed &&
            IsGroundedBuffered &&
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

        if (!jumpRequestedThisFrame)
        {
            SnapToGroundIfNeeded();
        }

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

            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
            }
        }
    }

    private void ApplyStepOffset()
    {
        if (characterController == null) return;

        if (IsGrounded || IsGroundedBuffered)
        {
            characterController.stepOffset = stepHeight;
        }
        else
        {
            characterController.stepOffset = airStepHeight;
        }
    }

    private void SnapToGroundIfNeeded()
    {
        if (characterController == null) return;

        if (characterController.isGrounded) return;

        if (velocity.y > 0.0f) return;

        // 大きく落下している時は吸着させない
        if (!IsGroundedBuffered &&
            velocity.y <= fallAnimationVelocityThreshold)
        {
            return;
        }

        Vector3 origin =
            transform.position +
            Vector3.up * groundSnapRayStartHeight;

        float rayDistance =
            groundSnapRayStartHeight + groundSnapDistance;

        if (Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            groundSnapMask,
            QueryTriggerInteraction.Ignore))
        {
            float snapDistance =
                hit.distance - groundSnapRayStartHeight;

            if (snapDistance > 0.0f &&
                snapDistance <= groundSnapDistance)
            {
                characterController.Move(
                    Vector3.down * snapDistance
                );

                if (velocity.y < 0.0f)
                {
                    velocity.y = controller.Stats.groundedY;
                }

                IsGrounded = true;
                lastGroundedTime = Time.time;
            }
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