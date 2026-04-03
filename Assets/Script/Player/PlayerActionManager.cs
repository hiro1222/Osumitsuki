using UnityEngine;

public class PlayerActionManager : MonoBehaviour
{
    private PlayerController controller;
    private Act_A actA;

    public bool IsActing { get; private set; }

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        actA = GetComponent<Act_A>();

        if (actA != null)
        {
            actA.Initialize(controller, this);
        }
    }

    public void Tick()
    {
        IsActing = false;

        if (actA != null)
        {
            actA.Tick();
            if (actA.IsRunning)
            {
                IsActing = true;
            }
        }
    }
}