using UnityEngine;

public class ActB : MonoBehaviour
{
    [Header("Anchor")]
    [SerializeField] private PlayerActionAnchorProvider anchorProvider;
    [SerializeField]
    private PlayerActionAnchorProvider.ActionAnchorType actionType =
        PlayerActionAnchorProvider.ActionAnchorType.Harai;

    [Header("Follow Objects ç≈ëÂ4ëÃ")]
    [SerializeField] private Transform followObject0;
    [SerializeField] private Transform followObject1;
    [SerializeField] private Transform followObject2;
    [SerializeField] private Transform followObject3;

    private Transform[] followObjects;

    private void Awake()
    {
        if (anchorProvider == null)
        {
            anchorProvider = GetComponent<PlayerActionAnchorProvider>();
        }

        followObjects = new Transform[4];
        followObjects[0] = followObject0;
        followObjects[1] = followObject1;
        followObjects[2] = followObject2;
        followObjects[3] = followObject3;
    }

    private void LateUpdate()
    {
        FollowAnchors();
    }

    private void FollowAnchors()
    {
        if (anchorProvider == null) return;

        for (int i = 0; i < followObjects.Length; i++)
        {
            Transform obj = followObjects[i];
            if (obj == null) continue;

            Transform anchor = anchorProvider.GetAnchor(actionType, i);
            if (anchor == null) continue;

            obj.position = anchor.position;
            obj.rotation = anchor.rotation;
        }
    }
}