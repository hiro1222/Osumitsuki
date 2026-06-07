using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Move,
        Jump,
        Fall,
        Action,
        Damage
    }

    private PlayerController controller;

    [Header("Enemy Contact Damage")]
    [SerializeField] private bool enableEnemyContactDamage = true;

    [Tooltip("Enemy Layer‚ðŽw’èB–¢Ý’è‚Å‚àIF_EnemyŒŸo‚Å”»’è‚·‚é")]
    [SerializeField] private LayerMask enemyLayerMask;

    [SerializeField] private float damageCheckRadius = 1.2f;
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float invincibleTime = 1.0f;
    [SerializeField] private float damageStateTime = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogDamage = true;
    [SerializeField] private bool debugLogHitCheck = false;
    [SerializeField] private bool drawDamageGizmos = true;

    private float invincibleTimer;
    private float damageStateTimer;

    public PlayerState CurrentState { get; private set; }

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        CurrentState = PlayerState.Idle;
        invincibleTimer = 0.0f;
        damageStateTimer = 0.0f;
    }

    public void Tick()
    {
        UpdateTimers();
        if (controller.Move.KnockbackStartedThisFrame)
        {
            controller.Stats.Damage(contactDamage);

            damageStateTimer = damageStateTime;
            ChangeState(PlayerState.Damage);

            if (debugLogDamage)
            {
                Debug.Log(
                    "[PlayerStateMachine] Damage from Knockback / HP : " +
                    controller.Stats.CurrentHP
                );
            }

            return;
        }
    }

    private void UpdateTimers()
    {
        if (invincibleTimer > 0.0f)
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer < 0.0f) invincibleTimer = 0.0f;
        }

        if (damageStateTimer > 0.0f)
        {
            damageStateTimer -= Time.deltaTime;
            if (damageStateTimer < 0.0f) damageStateTimer = 0.0f;
        }
    }

    private bool CheckEnemyContactDamage()
    {
        if (controller == null) return false;
        if (controller.Stats == null) return false;
        if (invincibleTimer > 0.0f) return false;

        Collider[] hits = Physics.OverlapSphere(
            controller.transform.position,
            damageCheckRadius,
            enemyLayerMask.value == 0 ? ~0 : enemyLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            IF_Enemy enemy = FindEnemyInterface(hit.transform);
            if (enemy == null) continue;
            if (enemy.GetIsAlly()) continue;

            controller.Stats.Damage(contactDamage);

            invincibleTimer = invincibleTime;
            damageStateTimer = damageStateTime;

            ChangeState(PlayerState.Damage);

            if (debugLogDamage)
            {
                Debug.Log(
                    "[PlayerStateMachine] Damage from Enemy : " +
                    hit.name +
                    " / HP : " +
                    controller.Stats.CurrentHP
                );
            }

            return true;
        }

        if (debugLogHitCheck)
        {
            Debug.Log("[PlayerStateMachine] Collider found, but IF_Enemy not found.");
        }

        return false;
    }

    private IF_Enemy FindEnemyInterface(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                IF_Enemy enemy = behaviours[i] as IF_Enemy;
                if (enemy != null)
                {
                    return enemy;
                }
            }

            current = current.parent;
        }

        return null;
    }

    private void ChangeState(PlayerState nextState)
    {
        if (CurrentState == nextState) return;
        CurrentState = nextState;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDamageGizmos) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageCheckRadius);
    }
}