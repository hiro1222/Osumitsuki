using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤー操作（Phase 2: コリジョンメッシュ方式）
/// - WASD: 移動
/// - マウス: プレイヤーの向きをマウス方向に
/// - 左クリック: 斬撃を発射
/// - 右クリック: 足元に墨を塗る（Raycastで地面のPaintableSurfaceに塗る）
/// - Rキー: リスポーン
///
/// Phase 1からの変更点:
/// - InkManagerの参照を削除（足元判定はコリジョンメッシュが自動処理）
/// - 足元のCanWalk判定を削除（CharacterControllerが勝手にやる）
/// - 落下判定を削除（コリジョンメッシュがなければ自然に落ちる）
/// - 右クリックの足元塗りはInkPaintService経由に変更
///
/// ※ CapsuleColliderは自動で削除されます
/// ※ プレイヤーは自動で "Player" レイヤーに設定されます
/// </summary>
public class SimplePlayer : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InkSlashSystem slashSystem;

    [Header("移動")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("筆塗り（右クリック）")]
    [SerializeField] private float paintRadius = 0.8f;
    [SerializeField] private byte paintDensity = 180;

    [Header("斬撃（左クリック）")]
    [SerializeField] private float slashCooldown = 0.3f;

    [Header("リスポーン")]
    [SerializeField] private float respawnY = -10f;

    private CharacterController controller;
    private float lastSlashTime;
    private Vector3 spawnPosition;

    private void Awake()
    {
        // ── レイヤー設定 ──
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            gameObject.layer = playerLayer;
        }
        else
        {
            Debug.LogWarning("[SimplePlayer] 'Player' レイヤーが見つかりません。" +
                             "Edit > Project Settings > Tags and Layers で追加してください。");
        }

        // ── CapsuleColliderを削除 ──
        var capsuleCol = GetComponent<CapsuleCollider>();
        if (capsuleCol != null)
            DestroyImmediate(capsuleCol);

        // ── CharacterController設定 ──
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = Vector3.zero;
        controller.skinWidth = 0.08f;
    }

    private void Start()
    {
        // 初期位置を記憶
        transform.position = new Vector3(transform.position.x, 1f, transform.position.z);
        spawnPosition = transform.position;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        // ── WASD移動 ──
        Vector3 input = Vector3.zero;
        if (keyboard.wKey.isPressed) input.z += 1f;
        if (keyboard.sKey.isPressed) input.z -= 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;

        Vector3 move = Vector3.zero;
        if (input.sqrMagnitude > 0.01f)
        {
            input = input.normalized;
            move = input * moveSpeed * Time.deltaTime;
        }

        // 常に重力を加える
        move.y = -9.8f * Time.deltaTime;
        controller.Move(move);

        // Phase 2: 足元判定は不要。コリジョンメッシュがあれば歩ける、なければ落ちる。
        // CharacterControllerの標準物理がそのまま動作する。

        // ── 落下しすぎたらリスポーン ──
        if (transform.position.y < respawnY)
        {
            Respawn();
        }

        // ── マウス方向を向く ──
        LookAtMouse();

        // ── 斬撃パターン切り替え ──
        if (slashSystem != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) slashSystem.SelectPattern(0);
            if (keyboard.digit2Key.wasPressedThisFrame) slashSystem.SelectPattern(1);
            if (keyboard.digit3Key.wasPressedThisFrame) slashSystem.SelectPattern(2);
            if (keyboard.digit4Key.wasPressedThisFrame) slashSystem.SelectPattern(3);
            if (keyboard.qKey.wasPressedThisFrame) slashSystem.SelectPattern(slashSystem.CurrentPatternIndex - 1);
            if (keyboard.eKey.wasPressedThisFrame) slashSystem.NextPattern();
        }

        // ── 左クリック: 斬撃発射 ──
        if (mouse.leftButton.wasPressedThisFrame && Time.time - lastSlashTime > slashCooldown)
        {
            if (slashSystem != null)
            {
                Vector3 spawnPos = transform.position + transform.forward * 1.5f;
                slashSystem.CreateSlash(spawnPos, transform.forward);
                lastSlashTime = Time.time;
            }
        }

        // ── 右クリック: 足元に墨塗り（Phase 2: InkPaintService経由） ──
        if (mouse.rightButton.isPressed)
        {
            // 足元にRaycastしてPaintableSurfaceに塗る
            if (Physics.Raycast(transform.position, Vector3.down,
                                out RaycastHit footHit, 3f, ~0,
                                QueryTriggerInteraction.Collide))
            {
                InkPaintService.Paint(footHit, paintRadius, paintDensity);
            }
        }

        // ── Rキー: リスポーン ──
        if (keyboard.rKey.wasPressedThisFrame)
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        controller.enabled = false;
        transform.position = spawnPosition;
        controller.enabled = true;
    }

    private void LookAtMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float dist))
        {
            Vector3 point = ray.GetPoint(dist);
            Vector3 lookDir = point - transform.position;
            lookDir.y = 0;

            if (lookDir.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void OnGUI()
    {
        if (slashSystem == null) return;
        var current = slashSystem.CurrentPattern;
        if (current == null) return;

        GUI.Label(new Rect(10, 10, 500, 25),
            $"斬撃: [{slashSystem.CurrentPatternIndex + 1}] {current.patternName}" +
            $"  (DMG:{current.baseDamage} SPD:{current.speed} 墨:{current.inkDensity})");
        GUI.Label(new Rect(10, 30, 500, 25),
            "1-4: パターン / Q/E: 切替 / 左クリック: 発射 / 右クリック: 塗る / R: リスポーン");
    }
}