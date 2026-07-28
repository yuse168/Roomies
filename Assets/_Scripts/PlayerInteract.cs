using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using TMPro;

public class PlayerInteract : NetworkBehaviour
{
    [Header("インタラクト設定")]
    public float interactDistance = 3f;
    public Transform cameraTransform;

    [Header("UI")]
    public TextMeshProUGUI interactText;

    [Header("持つ設定")]
    public float holdDistance = 2f;
    public float holdRightOffset = 0.6f;
    public float holdDownOffset = 0.4f;
    public float holdRadius = 0.35f;
    public float wallOffset = 0.25f;
    public float minHoldHeightFromGround = 0.6f;

    [Header("壁・床判定")]
    public LayerMask holdBlockMask;

    private CarryableObject heldObject;
    private NetworkFurniture heldFurniture;
    private SmugglingPlayer smugglingPlayer;
    private float furnitureYawOffset;
    private float furniturePickupPendingUntil;
    private float furnitureNextSyncTime;

    // インタラクト表示チップ（InteractTextを包む角丸背景）とクロスヘア
    private GameObject interactChip;
    private UnityEngine.UI.Graphic crosshair;
    private Vector3 crosshairBaseScale = Vector3.one;

    // クロスヘアの色（通常時 / 対象あり）
    private static readonly Color CrosshairIdle   = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color CrosshairActive = new Color(1f, 0.62f, 0.12f);

    void Start()
    {
        if (!IsOwner) return;

        smugglingPlayer = GetComponent<SmugglingPlayer>();

        // 自分のプレハブ内から探す
        // （GameObject.Findだと他プレイヤーのプレハブのUIを掴む可能性がある）
        foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (t.gameObject.name == "InteractText")
            {
                interactText = t;
                break;
            }
        }

        // フォールバック：シーン側に置かれている場合
        if (interactText == null)
        {
            GameObject uiObject = GameObject.Find("InteractText");
            if (uiObject != null) interactText = uiObject.GetComponent<TextMeshProUGUI>();
        }

        if (interactText != null)
        {
            // 角丸チップに載せ替えて、表示切り替えはチップごと行う
            interactChip = UITheme.BuildInteractChip(interactText);
            interactChip.SetActive(false);
        }
        else
        {
            Debug.LogWarning("InteractText が見つかりません");
        }

        // クロスヘア（対象に照準が合うと色が変わる）
        foreach (var g in GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            if (g.gameObject.name == "Crosshair")
            {
                crosshair = g;
                crosshairBaseScale = g.transform.localScale;
                g.color = CrosshairIdle;
                break;
            }
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (smugglingPlayer != null && smugglingPlayer.IsControlLocked)
        {
            SetInteractVisible(false);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        UpdateInteractUI();
        UpdateSlotBetInput();
        UpdateBlackjackBetInput();

        if (heldObject != null)
        {
            UpdateHeldObjectPosition();
        }

        if (heldFurniture != null)
        {
            UpdateHeldFurniturePosition();
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            TryUse();
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            if (heldFurniture != null)
                furnitureYawOffset = Mathf.Repeat(furnitureYawOffset + 45f, 360f);
            else
                TryStandBlackjack();
        }

        if (keyboard.fKey.wasPressedThisFrame)
        {
            if (heldFurniture != null)
            {
                DropFurniture();
            }
            else if (heldObject != null)
            {
                DropObject();
            }
            else
            {
                TryPickup();
            }
        }
    }

    void UpdateInteractUI()
    {
        if (interactText == null) return;

        string label = null;

        // 持ち中
        if (heldObject != null)
        {
            label = Key("F") + " 離す";
        }
        else if (heldFurniture != null)
        {
            label = Key("F") + " 設置   " + Key("R") + " 45°回転";
        }
        else if (cameraTransform != null)
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                // 持てる物
                SmugglingInteractable smuggling = hit.collider.GetComponentInParent<SmugglingInteractable>();
                if (smuggling != null && smuggling.CanInteract(smugglingPlayer))
                {
                    label = Key("E") + " " + smuggling.GetInteractionLabel(smugglingPlayer);
                }
                // 持てる物
                else if (hit.collider.GetComponentInParent<CarryableObject>() != null)
                {
                    label = Key("F") + " 持つ";
                }
                // 家具
                else if (hit.collider.GetComponentInParent<NetworkFurniture>() is NetworkFurniture furniture)
                {
                    label = furniture.IsHeld
                        ? "ほかの人が移動中"
                        : Key("F") + " " + furniture.DisplayName + "を持つ";
                }
                // ドア
                else if (hit.collider.GetComponentInParent<DoorInteract>() != null)
                {
                    label = Key("E") + " 使用";
                }
                // 納品ボタン
                else if (hit.collider.GetComponentInParent<DeliveryButton>() != null)
                {
                    label = Key("E") + " 納品";
                }
                // スロット
                else if (hit.collider.GetComponentInParent<SlotMachine>() is SlotMachine slot)
                {
                    label = Key("E") + " SPIN   " + Key("WHEEL") + " BET " + slot.CurrentBetLabel;
                }
                // ブラックジャック
                else if (hit.collider.GetComponentInParent<BlackjackTable>() is BlackjackTable blackjack)
                {
                    label = Key("E") + " " + blackjack.GetInteractionLabel(OwnerClientId);
                }
            }
        }

        bool show = label != null;

        if (show) interactText.text = label;

        // チップごと表示切り替え（チップ未生成時はテキスト単体で切り替え）
        if (interactChip != null)
        {
            if (interactChip.activeSelf != show) interactChip.SetActive(show);
        }
        else
        {
            interactText.gameObject.SetActive(show);
        }

        // クロスヘア：対象に照準が合うとオレンジ＋少し拡大
        if (crosshair != null)
        {
            crosshair.color = Color.Lerp(
                crosshair.color,
                show ? CrosshairActive : CrosshairIdle,
                Time.deltaTime * 14f);

            Vector3 targetScale = crosshairBaseScale * (show ? 1.25f : 1f);
            crosshair.transform.localScale = Vector3.Lerp(
                crosshair.transform.localScale, targetScale, Time.deltaTime * 14f);
        }
    }

    private void SetInteractVisible(bool show)
    {
        if (interactChip != null) interactChip.SetActive(show);
        else if (interactText != null) interactText.gameObject.SetActive(show);
    }

    // キー表記をアクセント色で強調する（例：F 持つ → Fがオレンジ）
    private static string Key(string key)
    {
        return $"<color=#FFA31F>{key}</color>";
    }

    private void UpdateSlotBetInput()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance)) return;

        SlotMachine slot = hit.collider.GetComponentInParent<SlotMachine>();
        if (slot == null) return;

        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        bool next = scroll > 0.01f || Keyboard.current.rightArrowKey.wasPressedThisFrame;
        bool previous = scroll < -0.01f || Keyboard.current.leftArrowKey.wasPressedThisFrame;

        if (next) slot.ChangeBet(1);
        else if (previous) slot.ChangeBet(-1);
    }

    private void UpdateBlackjackBetInput()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance)) return;

        BlackjackTable blackjack = hit.collider.GetComponentInParent<BlackjackTable>();
        if (blackjack == null || !blackjack.IsIdle) return;

        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        bool next = scroll > 0.01f || Keyboard.current.rightArrowKey.wasPressedThisFrame;
        bool previous = scroll < -0.01f || Keyboard.current.leftArrowKey.wasPressedThisFrame;

        if (next) blackjack.ChangeBet(1);
        else if (previous) blackjack.ChangeBet(-1);
    }

    void TryUse()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            SmugglingInteractable smuggling = hit.collider.GetComponentInParent<SmugglingInteractable>();
            if (smuggling != null && smuggling.CanInteract(smugglingPlayer))
            {
                smuggling.Interact(smugglingPlayer);
                return;
            }

            // ドア
            DoorInteract door = hit.collider.GetComponentInParent<DoorInteract>();

            if (door != null)
            {
                door.ToggleDoorServerRpc();
                return;
            }

            // 納品ボタン
            DeliveryButton deliveryButton = hit.collider.GetComponentInParent<DeliveryButton>();

            if (deliveryButton != null)
            {
                PlayerEarning playerEarning = GetComponent<PlayerEarning>();

                deliveryButton.PressButton(playerEarning);

                return;
            }

            // スロット
            SlotMachine slot = hit.collider.GetComponentInParent<SlotMachine>();

            if (slot != null)
            {
                PlayerEarning playerEarning = GetComponent<PlayerEarning>();

                slot.Interact(playerEarning);

                return;
            }

            // ブラックジャック
            BlackjackTable blackjack = hit.collider.GetComponentInParent<BlackjackTable>();

            if (blackjack != null)
            {
                PlayerEarning playerEarning = GetComponent<PlayerEarning>();
                blackjack.Interact(playerEarning);
                return;
            }
        }
    }

    private void TryStandBlackjack()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance)) return;

        BlackjackTable blackjack = hit.collider.GetComponentInParent<BlackjackTable>();
        if (blackjack == null || !blackjack.CanLocalPlayerStand(OwnerClientId)) return;

        blackjack.Stand();
    }

    void TryPickup()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            NetworkFurniture furniture =
                hit.collider.GetComponentInParent<NetworkFurniture>();

            if (furniture != null && !furniture.IsHeld)
            {
                heldFurniture = furniture;
                furnitureYawOffset = Mathf.DeltaAngle(
                    cameraTransform.eulerAngles.y,
                    furniture.transform.eulerAngles.y);
                furniturePickupPendingUntil = Time.unscaledTime + 0.75f;
                furnitureNextSyncTime = 0f;
                furniture.PickupServerRpc(NetworkObjectId);
                return;
            }

            CarryableObject carryable = hit.collider.GetComponentInParent<CarryableObject>();

            if (carryable != null)
            {
                heldObject = carryable;
                heldObject.PickupServerRpc(OwnerClientId, NetworkObjectId);
            }
        }
    }

    private void UpdateHeldFurniturePosition()
    {
        if (heldFurniture == null) return;

        if (!heldFurniture.IsHeldBy(OwnerClientId))
        {
            if (Time.unscaledTime >= furniturePickupPendingUntil)
                heldFurniture = null;
            return;
        }

        if (Time.unscaledTime < furnitureNextSyncTime) return;
        furnitureNextSyncTime = Time.unscaledTime + 0.05f;

        Vector3 flatForward = Vector3.ProjectOnPlane(
            cameraTransform.forward,
            Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.01f)
            flatForward = transform.forward;

        float extraDistance = Mathf.Clamp(
            heldFurniture.BoundingRadius,
            0.2f,
            1.35f);
        Vector3 targetPosition =
            cameraTransform.position +
            flatForward * (holdDistance + extraDistance) -
            Vector3.up * holdDownOffset;
        float targetYaw =
            cameraTransform.eulerAngles.y + furnitureYawOffset;

        heldFurniture.UpdateHeldTransformServerRpc(
            targetPosition,
            targetYaw);
    }

    private void DropFurniture()
    {
        if (heldFurniture == null) return;

        Vector3 flatForward = Vector3.ProjectOnPlane(
            cameraTransform.forward,
            Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.01f)
            flatForward = transform.forward;

        float extraDistance = Mathf.Clamp(
            heldFurniture.BoundingRadius,
            0.2f,
            1.35f);
        Vector3 dropPosition =
            cameraTransform.position +
            flatForward * (holdDistance + extraDistance);
        float targetYaw =
            cameraTransform.eulerAngles.y + furnitureYawOffset;

        heldFurniture.DropServerRpc(dropPosition, targetYaw);
        heldFurniture = null;
    }

    void UpdateHeldObjectPosition()
    {
        Vector3 targetPosition = GetSafeHoldPosition();
        Quaternion targetRotation = Quaternion.LookRotation(cameraTransform.forward);

        heldObject.UpdateHeldPositionServerRpc(targetPosition, targetRotation);
    }

    Vector3 GetSafeHoldPosition()
    {
        Vector3 origin = cameraTransform.position;
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        Vector3 down = -cameraTransform.up;

        Vector3 targetPosition = origin
            + forward * holdDistance
            + right * holdRightOffset
            + down * holdDownOffset;

        if (Physics.SphereCast(
            origin,
            holdRadius,
            forward,
            out RaycastHit hit,
            holdDistance,
            holdBlockMask,
            QueryTriggerInteraction.Ignore))
        {
            targetPosition = origin
                + forward * Mathf.Max(0.3f, hit.distance - wallOffset)
                + right * holdRightOffset
                + down * holdDownOffset;
        }

        float minY = transform.position.y + minHoldHeightFromGround;

        if (targetPosition.y < minY)
        {
            targetPosition.y = minY;
        }

        return targetPosition;
    }

    void DropObject()
    {
        Vector3 dropPosition = GetSafeHoldPosition();
        Vector3 throwDirection = cameraTransform.forward;

        heldObject.DropServerRpc(dropPosition, throwDirection);
        heldObject = null;
    }

    // 重いもの持った時足遅くなる
    public int GetHeldWeightLevel()
    {
        if (heldObject == null) return 0;

        return heldObject.weightLevel;
    }
}
