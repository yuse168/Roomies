using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("移動設定")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.5f;

    [Header("持ち物による速度低下")]
    public float carrySlowAmount = 0.15f;

    [Header("視点設定")]
    public float mouseSensitivity = 0.03f;

    [Header("ジャンプ設定")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("しゃがみ設定")]
    public float crouchHeight = 1f;
    public float standHeight = 2f;

    [Header("カメラ設定")]
    public Transform cameraTransform;

    private CharacterController controller;
    private PlayerInteract playerInteract;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private float xRotation;
    private bool isCrouching;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerInteract = GetComponent<PlayerInteract>();

        if (!IsOwner)
        {
            if (cameraTransform != null)
            {
                cameraTransform.gameObject.SetActive(false);
            }

            enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!IsOwner) return;

        ReadInput();
        Move();
        Look();
    }

    void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null || mouse == null) return;

        moveInput = Vector2.zero;

        if (keyboard.wKey.isPressed) moveInput.y += 1;
        if (keyboard.sKey.isPressed) moveInput.y -= 1;
        if (keyboard.aKey.isPressed) moveInput.x -= 1;
        if (keyboard.dKey.isPressed) moveInput.x += 1;

        lookInput = mouse.delta.ReadValue();

        if (keyboard.ctrlKey.wasPressedThisFrame)
        {
            ToggleCrouch();
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void Move()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        if (move.magnitude > 1f)
        {
            move.Normalize();
        }

        bool grounded = controller.isGrounded;

        if (grounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && grounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        float currentSpeed = walkSpeed;

        if (Keyboard.current.leftShiftKey.isPressed)
        {
            currentSpeed = sprintSpeed;
        }

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }

        if (playerInteract != null)
        {
            int weightLevel = playerInteract.GetHeldWeightLevel();

            if (weightLevel > 0)
            {
                float slowRate = 1f - ((weightLevel - 1) * carrySlowAmount);
                slowRate = Mathf.Clamp(slowRate, 0.35f, 1f);

                currentSpeed *= slowRate;
            }
        }

        Vector3 finalMove = move * currentSpeed + velocity;

        controller.Move(finalMove * Time.deltaTime);
    }

    void Look()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // body は常に直立を保つ（傾いた場所でスポーンしても転ばず立て直す）。
        // 上下の視点はカメラ側で処理しているため、body はヨーのみ残す。
        Vector3 bodyEuler = transform.eulerAngles;
        if (bodyEuler.x != 0f || bodyEuler.z != 0f)
        {
            transform.rotation = Quaternion.Euler(0f, bodyEuler.y, 0f);
        }
    }

    void ToggleCrouch()
    {
        isCrouching = !isCrouching;

        if (isCrouching)
        {
            controller.height = crouchHeight;
        }
        else
        {
            controller.height = standHeight;
        }
    }
}