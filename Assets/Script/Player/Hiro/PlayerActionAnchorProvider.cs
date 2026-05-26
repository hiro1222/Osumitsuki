using UnityEngine;

public class PlayerActionAnchorProvider : MonoBehaviour
{
    public enum ActionAnchorType
    {
        Nazori,
        Harai,
        Hane,
        DerivedHarai,
        DerivedHane,
        Tome
    }

    [System.Serializable]
    public class AnchorGroup
    {
        public ActionAnchorType action;
        public Transform anchor0;
        public Transform anchor1;
        public Transform anchor2;
        public Transform anchor3;

        public Transform GetAnchor(int index)
        {
            switch (index)
            {
                case 0: return anchor0;
                case 1: return anchor1;
                case 2: return anchor2;
                case 3: return anchor3;
                default: return null;
            }
        }
    }

    [Header("Action Anchor Groups")]
    [SerializeField] private AnchorGroup[] groups;

    public Transform GetAnchor(ActionAnchorType action, int index)
    {
        AnchorGroup group = FindGroup(action);

        if (group == null)
        {
            Debug.LogWarning("Anchor group not found: " + action);
            return null;
        }

        Transform anchor = group.GetAnchor(index);

        if (anchor == null)
        {
            Debug.LogWarning("Anchor is null: " + action + " / " + index);
        }

        return anchor;
    }

    public Vector3 GetAnchorPosition(ActionAnchorType action, int index)
    {
        Transform anchor = GetAnchor(action, index);
        return anchor != null ? anchor.position : transform.position;
    }

    public Quaternion GetAnchorRotation(ActionAnchorType action, int index)
    {
        Transform anchor = GetAnchor(action, index);
        return anchor != null ? anchor.rotation : transform.rotation;
    }

    private AnchorGroup FindGroup(ActionAnchorType action)
    {
        if (groups == null) return null;

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].action == action)
            {
                return groups[i];
            }
        }

        return null;
    }
}