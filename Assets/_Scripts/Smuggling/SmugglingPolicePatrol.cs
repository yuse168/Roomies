using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 夜だけPoint 1/2を往復し、懐中電灯で紙袋所持者を発見すると追跡する警察。
/// 移動・発見・逮捕はサーバーだけが判定し、NetworkTransformで同期する。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class SmugglingPolicePatrol : NetworkBehaviour
{
    private enum AiState : byte
    {
        Patrol,
        Scan,
        LookBack,
        FakeReverse,
        Surprised,
        Chase,
    }

    private static readonly List<SmugglingPolicePatrol> Officers = new List<SmugglingPolicePatrol>();

    [Header("巡回地点（シーン配置後に自由に移動可能）")]
    [SerializeField] private Transform point1;
    [SerializeField] private Transform point2;
    [SerializeField] private float patrolSpeed = 2.2f;
    [SerializeField] private float pointReachDistance = 0.15f;

    [Header("未発見時の警戒行動")]
    [Tooltip("通常巡回中に次の警戒行動を抽選するまでの秒数")]
    [SerializeField] private Vector2 patrolActionInterval = new Vector2(3f, 6f);
    [Tooltip("巡回ごとに変化させる歩行速度。既存の追跡速度には影響しない")]
    [SerializeField] private Vector2 patrolSpeedRange = new Vector2(1.8f, 2.5f);
    [SerializeField] private Vector2 scanDurationRange = new Vector2(0.9f, 1.5f);
    [Range(10f, 100f)]
    [SerializeField] private float scanAngle = 65f;
    [SerializeField] private Vector2 lookBackDurationRange = new Vector2(1.2f, 1.9f);
    [Range(90f, 180f)]
    [SerializeField] private float lookBackAngle = 165f;
    [SerializeField] private Vector2 fakeReverseDurationRange = new Vector2(0.7f, 1.35f);

    [Header("懐中電灯・発見")]
    [SerializeField] private Light flashlight;
    [SerializeField] private float viewDistance = 10f;
    [Range(10f, 160f)]
    [SerializeField] private float viewAngle = 58f;
    [SerializeField] private LayerMask sightBlockingMask = ~0;

    [Header("追跡")]
    [SerializeField] private float surpriseSeconds = 0.75f;
    [SerializeField] private float chaseSpeed = 5.8f;
    [SerializeField] private float captureDistance = 1.15f;
    [SerializeField] private float escapeDistance = 18f;
    [SerializeField] private float maxChaseSeconds = 18f;

    [Header("表示・当たり判定")]
    [SerializeField] private GameObject visibleRoot;
    [SerializeField] private Collider bodyCollider;

    private readonly NetworkVariable<bool> rosterActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private AiState aiState;
    private Vector3 point1Position;
    private Vector3 point2Position;
    private bool movingToPoint2 = true;
    private SmugglingPlayer chaseTarget;
    private float stateTimer;
    private float chaseTimer;
    private bool wasNight;
    private float nextPatrolActionTimer;
    private float currentPatrolSpeed;
    private float patrolActionDuration;
    private float patrolTurnSign;
    private Quaternion patrolActionStartRotation;
    private bool fakeReverseOriginalDirection;

    public override void OnNetworkSpawn()
    {
        Officers.Add(this);
        rosterActive.OnValueChanged += OnRosterActiveChanged;
        DayManager.OnNightArrived += OnNightArrived;
        DayManager.OnMorningArrived += OnMorningArrived;

        point1Position = point1 != null ? point1.position : transform.position;
        point2Position = point2 != null ? point2.position : transform.position;
        ApplyRosterActive(rosterActive.Value);

        if (IsServer && DayManager.Instance != null && DayManager.Instance.IsNight)
        {
            wasNight = true;
            if (IsRosterLeader()) SelectNightRoster();
        }
    }

    public override void OnNetworkDespawn()
    {
        Officers.Remove(this);
        rosterActive.OnValueChanged -= OnRosterActiveChanged;
        DayManager.OnNightArrived -= OnNightArrived;
        DayManager.OnMorningArrived -= OnMorningArrived;
    }

    private void Update()
    {
        if (!IsServer) return;

        bool isNight = DayManager.Instance != null && DayManager.Instance.IsNight;
        if (isNight != wasNight)
        {
            wasNight = isNight;
            if (isNight && IsRosterLeader()) SelectNightRoster();
            else if (!isNight)
            {
                rosterActive.Value = false;
                ResetToPatrol();
            }
        }

        if (!isNight || !rosterActive.Value) return;

        // Rigidbodyを持たない警察とCharacterControllerの組み合わせでは
        // OnCollisionEnterが発生しないため、実際のCollider範囲をServer側で確認する。
        // 巡回中に紙袋所持者へ直接触れた場合も「タッチされた」として逮捕する。
        if (TryArrestTouchingPlayer()) return;

        switch (aiState)
        {
            case AiState.Patrol:
                Patrol();
                break;
            case AiState.Scan:
                UpdateScan();
                break;
            case AiState.LookBack:
                UpdateLookBack();
                break;
            case AiState.FakeReverse:
                UpdateFakeReverse();
                break;
            case AiState.Surprised:
                UpdateSurprise();
                break;
            case AiState.Chase:
                Chase();
                break;
        }

        // 左右確認・後方確認・引き返し中も懐中電灯の向きで発見判定する。
        if (IsUnawareState(aiState)) TrySpotPlayer();
    }

    private void Patrol()
    {
        if (point1 != null && point2 != null)
        {
            Vector3 target = movingToPoint2 ? point2Position : point1Position;
            MoveTowards(target, GetPatrolSpeed());
            if (Vector3.Distance(transform.position, target) <= pointReachDistance)
            {
                movingToPoint2 = !movingToPoint2;
                BeginScan();
                return;
            }
        }

        nextPatrolActionTimer -= Time.deltaTime;
        if (nextPatrolActionTimer <= 0f) SelectPatrolAction();
    }

    private void SelectPatrolAction()
    {
        float roll = Random.value;
        if (roll < 0.65f)
        {
            RandomizePatrolSpeed();
            ScheduleNextPatrolAction();
        }
        else if (roll < 0.85f)
        {
            BeginScan();
        }
        else if (roll < 0.95f)
        {
            BeginLookBack();
        }
        else
        {
            BeginFakeReverse();
        }
    }

    private void BeginScan()
    {
        aiState = AiState.Scan;
        patrolActionDuration = RandomInRange(scanDurationRange, 1.2f);
        stateTimer = patrolActionDuration;
        patrolActionStartRotation = transform.rotation;
        patrolTurnSign = Random.value < 0.5f ? -1f : 1f;
    }

    private void UpdateScan()
    {
        stateTimer -= Time.deltaTime;
        float progress = 1f - Mathf.Clamp01(stateTimer / patrolActionDuration);
        float yaw = Mathf.Sin(progress * Mathf.PI * 2f) * scanAngle * patrolTurnSign;
        transform.rotation = patrolActionStartRotation * Quaternion.Euler(0f, yaw, 0f);

        if (stateTimer <= 0f) FinishPatrolAction();
    }

    private void BeginLookBack()
    {
        aiState = AiState.LookBack;
        patrolActionDuration = RandomInRange(lookBackDurationRange, 1.5f);
        stateTimer = patrolActionDuration;
        patrolActionStartRotation = transform.rotation;
        patrolTurnSign = Random.value < 0.5f ? -1f : 1f;
    }

    private void UpdateLookBack()
    {
        stateTimer -= Time.deltaTime;
        float progress = 1f - Mathf.Clamp01(stateTimer / patrolActionDuration);
        float turnAmount;

        if (progress < 0.3f)
            turnAmount = Mathf.SmoothStep(0f, 1f, progress / 0.3f);
        else if (progress < 0.65f)
            turnAmount = 1f;
        else
            turnAmount = Mathf.SmoothStep(1f, 0f, (progress - 0.65f) / 0.35f);

        transform.rotation = patrolActionStartRotation
            * Quaternion.Euler(0f, lookBackAngle * patrolTurnSign * turnAmount, 0f);

        if (stateTimer <= 0f) FinishPatrolAction();
    }

    private void BeginFakeReverse()
    {
        aiState = AiState.FakeReverse;
        patrolActionDuration = RandomInRange(fakeReverseDurationRange, 1f);
        stateTimer = patrolActionDuration;
        fakeReverseOriginalDirection = movingToPoint2;
        movingToPoint2 = !movingToPoint2;
    }

    private void UpdateFakeReverse()
    {
        if (point1 != null && point2 != null)
        {
            Vector3 target = movingToPoint2 ? point2Position : point1Position;
            MoveTowards(target, GetPatrolSpeed());
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        movingToPoint2 = fakeReverseOriginalDirection;
        FinishPatrolAction();
    }

    private void FinishPatrolAction()
    {
        transform.rotation = aiState == AiState.FakeReverse
            ? transform.rotation
            : patrolActionStartRotation;
        aiState = AiState.Patrol;
        stateTimer = 0f;
        RandomizePatrolSpeed();
        ScheduleNextPatrolAction();
    }

    private bool IsUnawareState(AiState stateToCheck)
    {
        return stateToCheck == AiState.Patrol
            || stateToCheck == AiState.Scan
            || stateToCheck == AiState.LookBack
            || stateToCheck == AiState.FakeReverse;
    }

    private float GetPatrolSpeed()
    {
        return currentPatrolSpeed > 0f ? currentPatrolSpeed : patrolSpeed;
    }

    private void RandomizePatrolSpeed()
    {
        currentPatrolSpeed = RandomInRange(patrolSpeedRange, patrolSpeed);
    }

    private void ScheduleNextPatrolAction()
    {
        nextPatrolActionTimer = RandomInRange(patrolActionInterval, 4.5f);
    }

    private static float RandomInRange(Vector2 range, float fallback)
    {
        float rawMin = Mathf.Min(range.x, range.y);
        float rawMax = Mathf.Max(range.x, range.y);
        if (rawMax <= 0f) return fallback;

        float min = Mathf.Max(0.05f, rawMin);
        float max = Mathf.Max(min, rawMax);
        return Random.Range(min, max);
    }

    private void TrySpotPlayer()
    {
        SmugglingPlayer best = null;
        float bestDistance = float.MaxValue;

        foreach (SmugglingPlayer player in FindObjectsByType<SmugglingPlayer>())
        {
            if (!player.IsSpawned || !player.HasBag) continue;

            Vector3 origin = flashlight != null ? flashlight.transform.position : transform.position + Vector3.up * 1.4f;
            Vector3 target = player.transform.position + Vector3.up;
            Vector3 offset = target - origin;
            float distance = offset.magnitude;
            if (distance > viewDistance || distance >= bestDistance) continue;

            Vector3 forward = flashlight != null ? flashlight.transform.forward : transform.forward;
            if (Vector3.Angle(forward, offset) > viewAngle * 0.5f) continue;

            if (Physics.Raycast(origin, offset.normalized, out RaycastHit hit, distance, sightBlockingMask, QueryTriggerInteraction.Ignore))
            {
                SmugglingPlayer hitPlayer = hit.collider.GetComponentInParent<SmugglingPlayer>();
                if (hitPlayer != player) continue;
            }

            best = player;
            bestDistance = distance;
        }

        if (best == null) return;

        chaseTarget = best;
        aiState = AiState.Surprised;
        stateTimer = surpriseSeconds;
        chaseTimer = 0f;

        if (DayManager.Instance != null)
            DayManager.Instance.ServerSendAnnounceTo(
                best.OwnerClientId,
                "！見つかった！",
                "警察が追ってくる。ダッシュで逃げ切れ！",
                NightEventManager.StyleDanger);
    }

    private void UpdateSurprise()
    {
        if (!IsValidChaseTarget())
        {
            ResetToPatrol();
            return;
        }

        Face(chaseTarget.transform.position);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) aiState = AiState.Chase;
    }

    private void Chase()
    {
        if (!IsValidChaseTarget())
        {
            ResetToPatrol();
            return;
        }

        float distance = Vector3.Distance(transform.position, chaseTarget.transform.position);
        chaseTimer += Time.deltaTime;

        if (distance <= captureDistance)
        {
            chaseTarget.ServerArrest();
            ResetToPatrol();
            return;
        }

        if (distance >= escapeDistance || chaseTimer >= maxChaseSeconds)
        {
            if (DayManager.Instance != null)
                DayManager.Instance.ServerSendAnnounceTo(
                    chaseTarget.OwnerClientId,
                    "逃げ切った！",
                    "警察をまいた。売人のところへ急ごう。",
                    NightEventManager.StylePeace);
            ResetToPatrol();
            return;
        }

        MoveTowards(chaseTarget.transform.position, chaseSpeed);
    }

    private bool TryArrestTouchingPlayer()
    {
        if (bodyCollider == null || !bodyCollider.enabled) return false;

        Bounds captureBounds = bodyCollider.bounds;
        captureBounds.Expand(0.2f);

        foreach (SmugglingPlayer player in FindObjectsByType<SmugglingPlayer>())
        {
            if (!player.IsSpawned || !player.HasBag) continue;

            CharacterController controller = player.GetComponent<CharacterController>();
            Collider playerCollider = controller != null
                ? controller
                : player.GetComponentInChildren<Collider>();

            bool isTouching = playerCollider != null
                ? captureBounds.Intersects(playerCollider.bounds)
                : captureBounds.Contains(player.transform.position);

            if (!isTouching) continue;

            Debug.Log($"[Smuggling] 警察が{player.name}へ接触。Serverで逮捕します。");
            player.ServerArrest();
            ResetToPatrol();
            return true;
        }

        return false;
    }

    private bool IsValidChaseTarget()
    {
        return chaseTarget != null && chaseTarget.IsSpawned && chaseTarget.HasBag;
    }

    private void ResetToPatrol()
    {
        chaseTarget = null;
        aiState = AiState.Patrol;
        stateTimer = 0f;
        chaseTimer = 0f;
        RandomizePatrolSpeed();
        ScheduleNextPatrolAction();
    }

    private void MoveTowards(Vector3 target, float speed)
    {
        Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);
        transform.position = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);
        Face(flatTarget);
    }

    private void Face(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction),
            540f * Time.deltaTime);
    }

    private void OnNightArrived()
    {
        if (!IsServer) return;
        wasNight = true;
        if (IsRosterLeader()) SelectNightRoster();
    }

    private void OnMorningArrived()
    {
        if (!IsServer) return;
        wasNight = false;
        rosterActive.Value = false;
        ResetToPatrol();
    }

    private bool IsRosterLeader()
    {
        SmugglingPolicePatrol leader = null;
        foreach (SmugglingPolicePatrol officer in Officers)
        {
            if (officer == null || !officer.IsSpawned) continue;
            if (leader == null || officer.NetworkObjectId < leader.NetworkObjectId) leader = officer;
        }
        return leader == this;
    }

    private void SelectNightRoster()
    {
        var available = new List<SmugglingPolicePatrol>();
        foreach (SmugglingPolicePatrol officer in Officers)
        {
            if (officer != null && officer.IsSpawned) available.Add(officer);
        }
        available.Sort((a, b) => a.NetworkObjectId.CompareTo(b.NetworkObjectId));

        int activeCount = Mathf.Min(Random.Range(1, 3), available.Count);
        for (int i = 0; i < available.Count; i++)
        {
            available[i].rosterActive.Value = i < activeCount;
            available[i].ResetToPatrol();
        }
    }

    private void OnRosterActiveChanged(bool oldValue, bool newValue)
    {
        ApplyRosterActive(newValue);
    }

    private void ApplyRosterActive(bool active)
    {
        if (visibleRoot != null) visibleRoot.SetActive(active);
        if (bodyCollider != null) bodyCollider.enabled = active;
        if (flashlight != null) flashlight.enabled = active;
    }

    private void OnDrawGizmosSelected()
    {
        Transform lightTransform = flashlight != null ? flashlight.transform : transform;
        Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.35f);
        Gizmos.DrawWireSphere(lightTransform.position, viewDistance);
        if (point1 != null && point2 != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(point1.position, point2.position);
        }
    }
}
