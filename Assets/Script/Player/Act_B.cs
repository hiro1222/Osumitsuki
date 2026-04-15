using UnityEngine;

public class Act_B : MonoBehaviour
{
    private PlayerController controller;
    private PlayerActionManager actionManager;

    [SerializeField] private float actionDuration = 0.5f;
    private float timer = 0.0f;

    public bool IsRunning { get; private set; }

    public void Initialize(PlayerController owner, PlayerActionManager manager)
    {
        controller = owner;
        actionManager = manager;
    }

    public void Tick()
    {
        if (!IsRunning)
        {
            if (controller.InputHandler.RightClickPressed)
            {
                StartAction();
            }
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0.0f)
        {
            EndAction();
        }
    }

    private void StartAction()
    {
        IsRunning = true;
        timer = actionDuration;
    }

    private void EndAction()
    {
        IsRunning = false;
        timer = 0.0f;
    }
}