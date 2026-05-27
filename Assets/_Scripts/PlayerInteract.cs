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
    public float holdHeight = -0.2f;
    public float holdRadius = 0.35f;
    public float wallOffset = 0.25f;
    public float minHoldHeightFromGround = 0.6f;

    [Header("壁・床判定")]
    public LayerMask holdBlockMask;

    private CarryableObject heldObject;

    void Start()
    {
        if (!IsOwner) return;

        GameObject uiObject = GameObject.Find("InteractText");

        if (uiObject != null)
        {
            interactText = uiObject.GetComponent<TextMeshProUGUI>();
            interactText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("InteractText が見つかりません");
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        UpdateInteractUI();

        if (heldObject != null)
        {
            UpdateHeldObjectPosition();
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            TryUse();
        }

        if (keyboard.fKey.wasPressedThisFrame)
        {
            if (heldObject != null)
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

        // 持ち中
        if (heldObject != null)
        {
            interactText.gameObject.SetActive(true);
            interactText.text = "F 離す";
            return;
        }

        if (cameraTransform == null)
        {
            interactText.gameObject.SetActive(false);
            return;
        }

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // 持てる物
            CarryableObject carryable = hit.collider.GetComponentInParent<CarryableObject>();

            if (carryable != null)
            {
                interactText.gameObject.SetActive(true);
                interactText.text = "F 持つ";
                return;
            }

            // ドア
            DoorInteract door = hit.collider.GetComponentInParent<DoorInteract>();

            if (door != null)
            {
                interactText.gameObject.SetActive(true);
                interactText.text = "E 使用";
                return;
            }

            // 納品ボタン
            DeliveryButton deliveryButton = hit.collider.GetComponentInParent<DeliveryButton>();

            if (deliveryButton != null)
            {
                interactText.gameObject.SetActive(true);
                interactText.text = "E 納品";
                return;
            }

            // スロット
            SlotMachine slot = hit.collider.GetComponentInParent<SlotMachine>();

            if (slot != null)
            {
                interactText.gameObject.SetActive(true);
                interactText.text = "E スロット";
                return;
            }
        }

        interactText.gameObject.SetActive(false);
    }

    void TryUse()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
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
                slot.Interact();
                return;
            }
        }
    }

    void TryPickup()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            CarryableObject carryable = hit.collider.GetComponentInParent<CarryableObject>();

            if (carryable != null)
            {
                heldObject = carryable;
                heldObject.PickupServerRpc(OwnerClientId, NetworkObjectId);
            }
        }
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
        Vector3 direction = cameraTransform.forward;

        Vector3 targetPosition =
            origin +
            direction * holdDistance +
            Vector3.up * holdHeight;

        if (Physics.SphereCast(
            origin,
            holdRadius,
            direction,
            out RaycastHit hit,
            holdDistance,
            holdBlockMask,
            QueryTriggerInteraction.Ignore))
        {
            targetPosition =
                origin +
                direction * Mathf.Max(0.3f, hit.distance - wallOffset) +
                Vector3.up * holdHeight;
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