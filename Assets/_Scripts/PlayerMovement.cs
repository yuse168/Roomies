using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

// PlayerMovement
// プレイヤーの一人称移動・ジャンプ・マウス視点操作を行うコード。
// Netcode対応版なので、自分が操作しているPlayerだけ入力を受け付ける。
public class PlayerMovement : NetworkBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 0.03f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("カメラ設定")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private float xRotation;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // 自分のPlayer以外はカメラと入力を無効にする
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
        // 自分のPlayerだけ動かす
        if (!IsOwner) return;

        ReadInput();
        Move();
        Look();
    }

    // キーボードとマウスの入力を読む
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

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // WASD移動とジャンプと重力処理
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

        Vector3 finalMove = move * moveSpeed + velocity;

        controller.Move(finalMove * Time.deltaTime);
    }

    // マウスで視点を動かす
    void Look()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}