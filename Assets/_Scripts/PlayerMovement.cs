using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("移動設定")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.5f;
    [Tooltip("地上で最高速度に達するまでの速さ")]
    public float groundAcceleration = 24f;
    [Tooltip("キーを離した時に止まる速さ")]
    public float groundDeceleration = 32f;
    [Tooltip("空中で方向を変えられる強さ")]
    public float airAcceleration = 7f;

    [Header("持ち物による速度低下")]
    public float carrySlowAmount = 0.15f;

    [Header("視点設定")]
    public float mouseSensitivity = 0.22f;

    [Header("ジャンプ設定")]
    public float jumpHeight = 1.5f;
    public float gravity = -18f;
    [Tooltip("崖から少し離れた直後でもジャンプできる猶予")]
    public float coyoteTime = 0.12f;
    [Tooltip("着地直前に押したジャンプを予約する時間")]
    public float jumpBufferTime = 0.12f;

    [Header("しゃがみ設定")]
    public float crouchHeight = 1f;
    public float standHeight = 2f;
    public float crouchTransitionSpeed = 12f;

    [Header("コミカルなカメラ演出")]
    public bool enableCameraMotion = true;
    [Tooltip("歩行中の上下の揺れ幅")]
    public float walkBobAmount = 0.035f;
    [Tooltip("ダッシュ中の上下の揺れ幅")]
    public float sprintBobAmount = 0.055f;
    public float walkBobFrequency = 9f;
    public float sprintBobFrequency = 12.5f;
    [Tooltip("左右移動時にカメラが傾く角度")]
    public float strafeTilt = 1.6f;
    [Tooltip("着地時にカメラが沈む最大量")]
    public float landingDipAmount = 0.09f;
    public float cameraMotionSmoothness = 14f;

    [Header("ダッシュ演出")]
    public float sprintFieldOfViewBonus = 5f;
    public float fieldOfViewSmoothness = 7f;

    [Header("カメラ設定")]
    public Transform cameraTransform;

    private CharacterController controller;
    private PlayerInteract playerInteract;
    private SmugglingPlayer smugglingPlayer;
    private Camera playerCamera;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float xRotation;
    private bool isCrouching;
    private bool isSprinting;

    [Header("アニメーション設定")]
    [SerializeField] private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

    private Vector3 cameraBaseLocalPosition;
    private float baseFieldOfView = 60f;
    private float bobTimer;
    private float landingDip;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private bool wasGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerInteract = GetComponent<PlayerInteract>();
        smugglingPlayer = GetComponent<SmugglingPlayer>();

        if (!IsOwner)
        {
            if (cameraTransform != null)
            {
                cameraTransform.gameObject.SetActive(false);
            }

            enabled = false;
            return;
        }

        GameSettings.ApplyToPlayer(this);

        if (cameraTransform != null)
        {
            cameraBaseLocalPosition = cameraTransform.localPosition;
            playerCamera = cameraTransform.GetComponent<Camera>();
            if (playerCamera != null)
            {
                baseFieldOfView = playerCamera.fieldOfView;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void Update()
    {
        if (!IsOwner || controller == null) return;

        if (EscMenuUI.IsOpen || CharacterCloset.IsOpen)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            horizontalVelocity = Vector3.zero;
            isSprinting = false;
            return;
        }

        if (smugglingPlayer != null && smugglingPlayer.IsControlLocked)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            horizontalVelocity = Vector3.zero;
            return;
        }

        ReadInput();
        Move();
        Look();
        UpdateStance();
        UpdateAnimation();
    }

    void LateUpdate()
    {
        if (!IsOwner || cameraTransform == null) return;

        UpdateCameraMotion();
    }

    void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null || mouse == null) return;

        moveInput = Vector2.zero;

        if (GameSettings.IsPressed(GameAction.MoveForward)) moveInput.y += 1f;
        if (GameSettings.IsPressed(GameAction.MoveBackward)) moveInput.y -= 1f;
        if (GameSettings.IsPressed(GameAction.MoveLeft)) moveInput.x -= 1f;
        if (GameSettings.IsPressed(GameAction.MoveRight)) moveInput.x += 1f;
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        lookInput = mouse.delta.ReadValue();

        if (GameSettings.WasPressedThisFrame(GameAction.Jump))
        {
            lastJumpPressedTime = Time.time;
        }

        // しゃがみはトグルではなく、キーを押している間だけ維持する。
        isCrouching = GameSettings.IsPressed(GameAction.Crouch);
    }

    public void ResetMotion()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = -2f;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
    }

    public void ApplyMiningImpact(Vector3 impulse)
    {
        if (!IsOwner) return;
        horizontalVelocity += Vector3.ProjectOnPlane(impulse, Vector3.up);
        verticalVelocity = Mathf.Max(verticalVelocity, impulse.y);
        landingDip = Mathf.Max(landingDip, .025f);
    }

    void Move()
    {
        bool grounded = controller.isGrounded;
        if (grounded)
        {
            lastGroundedTime = Time.time;

            if (!wasGrounded && verticalVelocity < -4f)
            {
                float landingStrength = Mathf.InverseLerp(4f, 18f, -verticalVelocity);
                landingDip = Mathf.Max(landingDip, landingDipAmount * landingStrength * 0.8f);
            }

            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
        }

        isSprinting = Keyboard.current != null
            && GameSettings.IsPressed(GameAction.Sprint)
            && !isCrouching
            && moveInput.y > 0.1f;

        float currentSpeed = isCrouching
            ? crouchSpeed
            : isSprinting ? sprintSpeed : walkSpeed;

        if (playerInteract != null)
        {
            int weightLevel = playerInteract.GetHeldWeightLevel();
            if (weightLevel > 0)
            {
                float slowRate = 1f - ((weightLevel - 1) * carrySlowAmount);
                currentSpeed *= Mathf.Clamp(slowRate, 0.35f, 1f);
            }
        }

        if (MiningSite.Instance != null && MiningSite.Instance.IsGasAt(transform.position)) currentSpeed *= .6f;

        FurnitureEffectManager furnitureEffects = FurnitureEffectManager.InstanceOrNull;
        if (furnitureEffects != null)
        {
            currentSpeed *= furnitureEffects.MoveSpeedMultiplier;
        }

        Vector3 desiredDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 desiredVelocity = desiredDirection * currentSpeed;
        float acceleration = grounded
            ? (moveInput.sqrMagnitude > 0.001f ? groundAcceleration : groundDeceleration)
            : airAcceleration;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            desiredVelocity,
            acceleration * Time.deltaTime);

        bool hasBufferedJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canUseCoyoteJump = Time.time - lastGroundedTime <= coyoteTime;
        if (hasBufferedJump && canUseCoyoteJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            grounded = false;
        }

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        wasGrounded = grounded;
    }

    void Look()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        xRotation = Mathf.Clamp(xRotation - mouseY, -88f, 88f);
        transform.Rotate(Vector3.up * mouseX);

        Vector3 bodyEuler = transform.eulerAngles;
        if (!Mathf.Approximately(bodyEuler.x, 0f) || !Mathf.Approximately(bodyEuler.z, 0f))
        {
            transform.rotation = Quaternion.Euler(0f, bodyEuler.y, 0f);
        }
    }

    void UpdateStance()
    {
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float previousHeight = controller.height;
        float nextHeight = Mathf.MoveTowards(
            previousHeight,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime);

        // カプセルの下端を固定し、しゃがむたびに足が床から浮かないようにする。
        float bottom = controller.center.y - previousHeight * 0.5f;
        controller.height = nextHeight;
        Vector3 center = controller.center;
        center.y = bottom + nextHeight * 0.5f;
        controller.center = center;
    }

    void UpdateCameraMotion()
    {
        float planarSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        float speedRatio = sprintSpeed > 0f ? Mathf.Clamp01(planarSpeed / sprintSpeed) : 0f;
        bool movingOnGround = controller.isGrounded && planarSpeed > 0.15f;

        float bobX = 0f;
        float bobY = 0f;
        float bobPitch = 0f;
        if (enableCameraMotion && movingOnGround)
        {
            float frequency = isSprinting ? sprintBobFrequency : walkBobFrequency;
            const float motionScale = 0.72f;
            float amount = (isSprinting ? sprintBobAmount : walkBobAmount) * motionScale;
            bobTimer += Time.deltaTime * frequency;
            bobY = Mathf.Sin(bobTimer) * amount;
            bobX = Mathf.Cos(bobTimer * 0.5f) * amount * 0.45f;
            bobPitch = -Mathf.Sin(bobTimer) * amount * 18f;
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, 0f, 8f * Time.deltaTime);
        }

        landingDip = Mathf.MoveTowards(landingDip, 0f, Time.deltaTime * 0.55f);
        float crouchOffset = (controller.height - standHeight) * 0.5f;
        Vector3 targetPosition = cameraBaseLocalPosition
            + new Vector3(bobX, bobY + crouchOffset - landingDip, 0f);
        float positionLerp = 1f - Mathf.Exp(-cameraMotionSmoothness * Time.deltaTime);
        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            targetPosition,
            positionLerp);

        float localStrafeSpeed = transform.InverseTransformDirection(horizontalVelocity).x;
        float tilt = enableCameraMotion
            ? -Mathf.Clamp(localStrafeSpeed / Mathf.Max(walkSpeed, 0.01f), -1f, 1f) * strafeTilt * 0.72f
            : 0f;
        cameraTransform.localRotation = Quaternion.Euler(xRotation + bobPitch, 0f, tilt);

        if (playerCamera != null)
        {
            float targetFov = baseFieldOfView + (isSprinting ? sprintFieldOfViewBonus * speedRatio : 0f);
            float fovLerp = 1f - Mathf.Exp(-fieldOfViewSmoothness * Time.deltaTime);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, fovLerp);
        }
    }
    void UpdateAnimation()
    {
        if (animator == null || controller == null)
        {
            return;
        }

        float planarSpeed = new Vector3(
            horizontalVelocity.x,
            0f,
            horizontalVelocity.z
        ).magnitude;

        float normalizedSpeed = sprintSpeed > 0f
            ? Mathf.Clamp01(planarSpeed / sprintSpeed)
            : 0f;

        animator.SetFloat(
            SpeedHash,
            normalizedSpeed,
            0.1f,
            Time.deltaTime
        );

        //animator.SetBool(IsGroundedHash, controller.isGrounded);
        //animator.SetBool(IsSprintingHash, isSprinting);
        //animator.SetBool(IsCrouchingHash, isCrouching);
    }
}
