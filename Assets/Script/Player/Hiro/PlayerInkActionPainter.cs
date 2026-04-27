using UnityEngine;

public class PlayerInkActionPainter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InkSlashSystem slashSystem;

    [Header("Ground Paint Ray")]
    [SerializeField] private LayerMask paintRayMask = ~0;
    [SerializeField] private float rayStartHeight = 1.2f;
    [SerializeField] private float rayLength = 4.0f;

    public void PaintGroundNearPlayer(Transform player, float forwardOffset, float radius, byte density)
    {
        if (player == null) return;

        Vector3 origin = player.position + player.forward * forwardOffset + Vector3.up * rayStartHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, paintRayMask, QueryTriggerInteraction.Collide))
        {
            InkPaintService.Paint(hit, radius, density);
        }
    }

    public void FireSlashPattern(Transform player, int patternIndex, float forwardOffset, float heightOffset)
    {
        if (player == null) return;
        if (slashSystem == null) return;

        if (patternIndex >= 0)
        {
            slashSystem.SelectPattern(patternIndex);
        }

        Vector3 spawnPos = player.position + player.forward * forwardOffset + Vector3.up * heightOffset;
        slashSystem.CreateSlash(spawnPos, player.forward);
    }
}
