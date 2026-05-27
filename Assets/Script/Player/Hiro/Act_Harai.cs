using UnityEngine;

public class Act_Harai : PlayerActionBase
{
    [System.Serializable]
    public class ExtraShotSetting
    {
        [Header("Unlock")]
        [Tooltip("この塗レベル以上で発射される")]
        [Range(1, 4)]
        public int unlockLevel = 1;

        [Header("Slash Pattern")]
        public SlashPattern slashPattern;

        [Header("Local Offset")]
        public Vector3 localPositionOffset = Vector3.zero;
        public Vector3 localEulerOffset = Vector3.zero;

        [Header("Spawn Offset")]
        public float forwardOffset = 1.5f;
        public float heightOffset = 0.4f;
    }

    [Header("Harai")]
    [SerializeField] private string animationName = "harai";
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    [Header("Base Slash")]
    [SerializeField] private bool enableSlash = true;
    [SerializeField] private SlashPattern baseSlashPattern;
    [SerializeField] private float baseForwardOffset = 1.5f;
    [SerializeField] private float baseHeightOffset = 0.4f;

    [Header("Paint Level")]
    [SerializeField, Range(0, 4)] private int paintLevel = 0;

    [Header("Extra Shots")]
    [SerializeField] private ExtraShotSetting[] extraShots = new ExtraShotSetting[4];

    private PlayerInkActionPainter inkPainter;
    private Transform shotAnchor;

    public override string ActionName => "はらい";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Harai;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public void SetPaintLevel(int level)
    {
        paintLevel = Mathf.Clamp(level, 0, 4);
    }

    public int GetPaintLevel()
    {
        return paintLevel;
    }

    public override void Initialize(PlayerController owner, PlayerActionManager actionManager)
    {
        base.Initialize(owner, actionManager);

        inkPainter = owner.GetComponent<PlayerInkActionPainter>();
        if (inkPainter == null)
        {
            inkPainter = owner.gameObject.AddComponent<PlayerInkActionPainter>();
        }

        GameObject anchorObj = new GameObject("HaraiShotAnchor");
        anchorObj.transform.SetParent(owner.transform);
        shotAnchor = anchorObj.transform;
    }

    public override bool CanStart()
    {
        return !manager.IsActing;
    }

    protected override void OnStartEffect()
    {
        if (!enableSlash) return;
        if (inkPainter == null) return;

        FireBaseShot();
        FireExtraShots();
    }

    private void FireBaseShot()
    {
        if (baseSlashPattern == null) return;

        inkPainter.FireSlashPattern(
            controller.transform,
            baseSlashPattern,
            baseForwardOffset,
            baseHeightOffset
        );
    }

    private void FireExtraShots()
    {
        if (extraShots == null) return;

        int level = Mathf.Clamp(paintLevel, 0, 4);

        for (int i = 0; i < extraShots.Length; ++i)
        {
            ExtraShotSetting shot = extraShots[i];

            if (shot == null) continue;
            if (shot.slashPattern == null) continue;
            if (level < shot.unlockLevel) continue;

            FireExtraShot(shot);
        }
    }

    private void FireExtraShot(ExtraShotSetting shot)
    {
        Transform baseTf = controller.transform;

        shotAnchor.position =
            baseTf.position +
            baseTf.right * shot.localPositionOffset.x +
            baseTf.up * shot.localPositionOffset.y +
            baseTf.forward * shot.localPositionOffset.z;

        shotAnchor.rotation =
            baseTf.rotation *
            Quaternion.Euler(shot.localEulerOffset);

        inkPainter.FireSlashPattern(
            shotAnchor,
            shot.slashPattern,
            shot.forwardOffset,
            shot.heightOffset
        );
    }

    protected override void OnTickEffect(float dt) { }
    protected override void OnEndEffect() { }
}