using UnityEngine;

public class PlayerAnimatorDriver : MonoBehaviour
{
    private PlayerController controller;
    private Animator anim;

    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashGrounded = Animator.StringToHash("IsGrounded");
    private readonly int hashHane = Animator.StringToHash("Hane");
    private readonly int hashHarai = Animator.StringToHash("Harai");

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        anim = transform.Find("nazori").GetComponent<Animator>();
    }

    public void Tick()
    {
        Vector2 moveInput = controller.InputHandler.MoveInput;
        float speed = moveInput.magnitude;

        anim.SetFloat(hashSpeed, speed);
        anim.SetBool(hashGrounded, controller.Move.IsGrounded);

        Debug.Log(controller.InputHandler.MoveInput);

        if (controller.ActionManager.IsActA)
        {
            anim.SetTrigger(hashHarai);
        }

        if (controller.ActionManager.IsActB)
        {
            anim.SetTrigger(hashHane);
        }
    }
}