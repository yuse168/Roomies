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

    [Tooltip("夜イベント（寝不足など）で一時的に移動速度へ掛ける倍率。1で通常。")]
    public float speedScale = 1f;

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
    [Tooltip("立ち上がり時の空間チェックに含めるレイヤー")]
    [SerializeField] private LayerMask stanceCollisionMask = ~0;

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
    [Header("足音")]
    [Tooltip("この距離だけ水平移動するごとに足音を1回鳴らす")]
    public float footstepDistance = 1.55f;
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
    private readonly Collider[] stanceOverlapBuffer = new Collider[16];
    private bool isSprinting;

    [Header("アニメーション設定")]
    [SerializeField] private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int EmoteHash = Animator.StringToHash("Emote");
    private static readonly int EmoteIndexHash = Animator.StringToHash("EmoteIndex");
    private static readonly int EmoteCancelHash = Animator.StringToHash("EmoteCancel");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private bool isEmoteWheelOpen;
    private bool isEmoting;
    private int currentEmoteIndex;

    private Vector3 cameraBaseLocalPosition;
    private float baseFieldOfView = 60f;
    private float bobTimer;
    private float landingDip;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private bool wasGrounded;
    private float footstepTravel;

    void Awake()
    {
        // Remote players also receive the jump/emote RPCs, before owner input starts.
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

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
    }

    void OnDisable()
    {
        CancelEmoteWheel();
    }

    public override void OnNetworkDespawn()
    {
        CancelEmoteWheel();
        base.OnNetworkDespawn();
    }

    void CancelEmoteWheel()
    {
        if (!isEmoteWheelOpen) return;
        isEmoteWheelOpen = false;
        EmoteWheelUI.HideIfCreated();
    }

    void Update()
    {
        if (!IsOwner || controller == null) return;

        if (EscMenuUI.IsOpen || CharacterCloset.IsOpen)
        {
            CancelEmoteWheel();
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            horizontalVelocity = Vector3.zero;
            isSprinting = false;
            return;
        }

        if (smugglingPlayer != null && smugglingPlayer.IsControlLocked)
        {
            CancelEmoteWheel();
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
        // キーを離しても立ち姿勢ぶんの空間がなければ、実際の姿勢はしゃがみに保つ。
        bool crouchHeld = GameSettings.IsPressed(GameAction.Crouch);
        if (crouchHeld)
        {
            isCrouching = true;
        }
        else if (isCrouching || controller.height < standHeight - 0.001f)
        {
            isCrouching = !CanStandUp();
        }
        else
        {
            isCrouching = false;
        }

        if (GameSettings.WasPressedThisFrame(GameAction.Emote))
        {
            isEmoteWheelOpen = true;
            EmoteWheelUI.Instance.Show();
        }

        if (GameSettings.IsPressed(GameAction.Emote))
        {
            if (isEmoteWheelOpen)
            {
                EmoteWheelUI.Instance.AddMouseInput(mouse.delta.ReadValue());
                lookInput = Vector2.zero; // ホイール操作中は視点移動を固定
            }
        }
        else if (isEmoteWheelOpen)
        {
            int selectedIndex = EmoteWheelUI.Instance.CurrentSelectedSlot;
            CancelEmoteWheel();

            if (selectedIndex > 0)
            {
                PlayEmoteRpc(selectedIndex);
            }
        }
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
                // 着地はジャンプ1回につき1度なので、RPCでも頻度は低い
                PlayLandRpc();
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

        // 夜イベント（寝不足など）の一時的な重さ。通常は1で何も変わらない。
        currentSpeed *= Mathf.Clamp(speedScale, 0.2f, 2f);

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

        if (isEmoting && (moveInput.sqrMagnitude > 0.01f || (hasBufferedJump && canUseCoyoteJump)))
        {
            StopEmoteRpc();
        }
        if (hasBufferedJump && canUseCoyoteJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            grounded = false;
            PlayJumpRpc();
        }

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        UpdateFootsteps(grounded);
        wasGrounded = grounded;
    }

    /// <summary>
    /// 接地中の水平移動distanceを積算し、歩幅ぶん進むごとに足音を鳴らす。
    /// 速く動くほど自然に間隔が詰まる。
    /// 高頻度なのでRPCは使わず、自分の足音だけをローカルで鳴らす。
    /// </summary>
    private void UpdateFootsteps(bool grounded)
    {
        if (!grounded)
        {
            footstepTravel = 0f;
            return;
        }

        float speed = Vector3.ProjectOnPlane(horizontalVelocity, Vector3.up).magnitude;
        if (speed < 0.6f)
        {
            footstepTravel = 0f;
            return;
        }

        footstepTravel += speed * Time.deltaTime;

        // しゃがみ移動は歩幅を広く・音量を小さくして忍び足にする
        float stride = isCrouching ? footstepDistance * 1.4f : footstepDistance;
        if (footstepTravel < stride) return;

        footstepTravel = 0f;
        GameAudio.Play("player/footstep", isCrouching ? 0.5f : 1f, 0.09f);
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

    bool CanStandUp()
    {
        if (controller == null)
        {
            return true;
        }

        float targetHeight = Mathf.Max(standHeight, controller.radius * 2f);
        if (controller.height >= targetHeight - 0.001f)
        {
            return true;
        }

        Vector3 lossyScale = transform.lossyScale;
        float radiusScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
        float heightScale = Mathf.Abs(lossyScale.y);
        float worldRadius = controller.radius * radiusScale;
        float worldHeight = Mathf.Max(targetHeight * heightScale, worldRadius * 2f);

        // 接触している床を障害物として誤検出しないよう、skinWidthぶん内側を調べる。
        float clearance = Mathf.Max(
            0.01f,
            controller.skinWidth * Mathf.Max(radiusScale, heightScale));
        float checkRadius = Mathf.Max(0.01f, worldRadius - clearance);
        float checkHeight = Mathf.Max(checkRadius * 2f, worldHeight - clearance * 2f);

        // 現在の足元を固定したまま、立ち姿勢になった場合のカプセルを組み立てる。
        float bottom = controller.center.y - controller.height * 0.5f;
        Vector3 targetCenter = controller.center;
        targetCenter.y = bottom + targetHeight * 0.5f;

        Vector3 worldCenter = transform.TransformPoint(targetCenter);
        float halfSegment = Mathf.Max(0f, checkHeight * 0.5f - checkRadius);
        Vector3 upperPoint = worldCenter + transform.up * halfSegment;
        Vector3 lowerPoint = worldCenter - transform.up * halfSegment;

        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            upperPoint,
            lowerPoint,
            checkRadius,
            stanceOverlapBuffer,
            stanceCollisionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider hit = stanceOverlapBuffer[i];
            if (hit == null || hit == controller)
            {
                continue;
            }

            Transform hitTransform = hit.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
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

        animator.SetBool(IsGroundedHash, controller.isGrounded);
        animator.SetBool(IsCrouchingHash, isCrouching);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayJumpRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger(JumpHash);
        }

        GameAudio.PlayAt("player/jump", transform.position, 1f, 0.05f);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayLandRpc()
    {
        GameAudio.PlayAt("player/land", transform.position, 1f, 0.06f);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayEmoteRpc(int emoteIndex)
    {
        currentEmoteIndex = emoteIndex;
        isEmoting = (emoteIndex > 0);

        if (animator != null)
        {
            animator.SetInteger(EmoteIndexHash, emoteIndex);
            animator.SetTrigger(EmoteHash);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void StopEmoteRpc()
    {
        currentEmoteIndex = 0;
        isEmoting = false;

        if (animator != null)
        {
            animator.SetInteger(EmoteIndexHash, 0);
            animator.SetTrigger(EmoteCancelHash);
        }
    }
}
