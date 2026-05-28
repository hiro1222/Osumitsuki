using UnityEngine;

public class PlayerInkActionPainter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InkSlashSystem slashSystem;

    [Header("Ground Paint Ray")]
    [SerializeField] private LayerMask paintRayMask = ~0;

    [SerializeField] private float rayStartHeight = 1.2f;
    [SerializeField] private float rayLength = 4.0f;

    private void Awake()
    {
        if (slashSystem == null)
        {
            slashSystem = FindObjectOfType<InkSlashSystem>();
        }
    }

    public void PaintGroundNearPlayer(
        Transform player,
        float forwardOffset,
        float radius,
        byte density
    )
    {
        if (player == null)
        {
            return;
        }

        Vector3 origin =
            player.position +
            player.forward * forwardOffset +
            Vector3.up * rayStartHeight;

        if (
            Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                rayLength,
                paintRayMask,
                QueryTriggerInteraction.Collide
            )
        )
        {
            InkPaintService.Paint(
                hit,
                radius,
                density
            );
        }
    }

    // ===========================
    // 従来版（PatternIndex指定）
    // ===========================

    public void FireSlashPattern(
        Transform player,
        int patternIndex,
        float forwardOffset,
        float heightOffset
    )
    {
        if (player == null)
        {
            return;
        }

        if (slashSystem == null)
        {
            return;
        }

        if (patternIndex >= 0)
        {
            slashSystem.SelectPattern(patternIndex);
        }

        Vector3 spawnPos =
            player.position +
            player.forward * forwardOffset +
            Vector3.up * heightOffset;

        slashSystem.CreateSlash(
            spawnPos,
            player.forward
        );
    }

    // ===========================
    // 新版（SlashPattern直接指定）
    // ===========================

    public void FireSlashPattern(
        Transform player,
        SlashPattern pattern,
        float forwardOffset,
        float heightOffset
    )
    {
        if (player == null)
        {
            return;
        }

        if (slashSystem == null)
        {
            return;
        }

        if (pattern == null)
        {
            Debug.LogWarning(
                "[PlayerInkActionPainter] SlashPattern が null"
            );

            return;
        }
        using UnityEngine;

public class PlayerInkActionPainter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InkSlashSystem slashSystem;

        [Header("Ground Paint Ray")]
        [SerializeField] private LayerMask paintRayMask = ~0;

        [SerializeField] private float rayStartHeight = 1.2f;
        [SerializeField] private float rayLength = 4.0f;

        private void Awake()
        {
            if (slashSystem == null)
            {
                slashSystem = FindObjectOfType<InkSlashSystem>();
            }
        }

        public InkSlashSystem GetSlashSystem()
        {
            if (slashSystem == null)
            {
                slashSystem = FindObjectOfType<InkSlashSystem>();
            }

            return slashSystem;
        }

        public void PaintGroundNearPlayer(
            Transform player,
            float forwardOffset,
            float radius,
            byte density
        )
        {
            if (player == null) return;

            Vector3 origin =
                player.position +
                player.forward * forwardOffset +
                Vector3.up * rayStartHeight;

            if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                rayLength,
                paintRayMask,
                QueryTriggerInteraction.Collide
            ))
            {
                InkPaintService.Paint(
                    hit,
                    radius,
                    density
                );
            }
        }

        public void FireSlashPattern(
            Transform player,
            int patternIndex,
            float forwardOffset,
            float heightOffset
        )
        {
            if (player == null) return;

            InkSlashSystem system = GetSlashSystem();
            if (system == null) return;

            if (patternIndex >= 0)
            {
                system.SelectPattern(patternIndex);
            }

            Vector3 spawnPos =
                player.position +
                player.forward * forwardOffset +
                Vector3.up * heightOffset;

            system.CreateSlash(
                spawnPos,
                player.forward
            );
        }

        public void FireSlashPattern(
            Transform player,
            SlashPattern pattern,
            float forwardOffset,
            float heightOffset
        )
        {
            if (player == null) return;

            InkSlashSystem system = GetSlashSystem();
            if (system == null) return;

            if (pattern == null)
            {
                Debug.LogWarning("[PlayerInkActionPainter] SlashPattern が null");
                return;
            }

            Vector3 spawnPos =
                player.position +
                player.forward * forwardOffset +
                Vector3.up * heightOffset;

            Vector3 direction =
                player.forward.normalized;

            system.CreateSlash(
                spawnPos,
                direction,
                pattern
            );
        }
    }
    Vector3 spawnPos =
            player.position +
            player.forward * forwardOffset +
            Vector3.up * heightOffset;

        Vector3 direction =
            player.forward.normalized;

        slashSystem.CreateSlash(
            spawnPos,
            direction,
            pattern
        );
    }
}