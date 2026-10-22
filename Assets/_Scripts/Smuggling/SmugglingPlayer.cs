using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>プレイヤーごとの運び屋進行、紙袋表示、逮捕・牢屋状態を同期する。</summary>
public class SmugglingPlayer : NetworkBehaviour
{
    private enum JobState : byte
    {
        None,
        CarryingBag,
        ArrestedWaitingMorning,
        JailLabor,
    }

    [Header("紙袋の見た目（未設定なら仮の紙袋を生成）")]
    [SerializeField] private GameObject bagVisualPrefab;
    [SerializeField] private Vector3 bagLocalPosition = new Vector3(0.58f, 0.62f, 0.45f);
    [SerializeField] private Vector3 bagLocalEuler = new Vector3(8f, 0f, -8f);
    [SerializeField] private float bagScale = 0.32f;

    [Header("サーバー検証")]
    [SerializeField] private float contactDistance = 4f;
    [SerializeField] private float laborDistance = 4f;

    private readonly NetworkVariable<JobState> state = new NetworkVariable<JobState>(
        JobState.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> contentIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Owner,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> laborProgress = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameObject bagVisual;
    private bool localControlLocked;
    private readonly HashSet<ulong> activeChaseSources = new HashSet<ulong>();

    public bool HasBag => state.Value == JobState.CarryingBag;
    public bool CanTakeBag => state.Value == JobState.None;
    public bool CanDeliverBag => state.Value == JobState.CarryingBag;
    public bool CanDoJailLabor => state.Value == JobState.JailLabor;
    public bool IsJailed => state.Value == JobState.ArrestedWaitingMorning || state.Value == JobState.JailLabor;
    public bool IsControlLocked => localControlLocked;
    public int JailLaborProgress => laborProgress.Value;

    public override void OnNetworkSpawn()
    {
        state.OnValueChanged += OnStateChanged;
        DayManager.OnMorningArrived += OnMorningArrived;
        RefreshBagVisual();
    }

    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= OnStateChanged;
        DayManager.OnMorningArrived -= OnMorningArrived;
        activeChaseSources.Clear();
        if (IsOwner) SetLocalChaseOverlay(false);
        if (bagVisual != null) Destroy(bagVisual);
    }

    /// <summary>
    /// 警察AIが追跡を始めた・やめた時だけServerから呼ぶ。
    /// 警察ごとに記録し、複数人に追われている途中で表示が消えないようにする。
    /// </summary>
    public void ServerSetChasedBy(ulong policeNetworkObjectId, bool chasing)
    {
        if (!IsServer) return;

        bool wasChased = activeChaseSources.Count > 0;
        if (chasing)
            activeChaseSources.Add(policeNetworkObjectId);
        else
            activeChaseSources.Remove(policeNetworkObjectId);

        bool isChased = activeChaseSources.Count > 0;
        if (wasChased != isChased) SetChaseStateOwnerRpc(isChased);
    }

    public void RequestContactInteraction(SmugglingContact.ContactRole role)
    {
        if (!IsOwner) return;
        ContactServerRpc((byte)role);
    }

    public void RequestJailLabor()
    {
        if (!IsOwner) return;
        JailLaborServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ContactServerRpc(byte roleValue, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (DayManager.Instance == null || !DayManager.Instance.IsNight) return;

        var role = (SmugglingContact.ContactRole)roleValue;
        if (!IsNearContact(role)) return;

        if (role == SmugglingContact.ContactRole.Giver)
        {
            if (state.Value != JobState.None) return;
            contentIndex.Value = SmugglingConfig.RandomContentIndex();
            state.Value = JobState.CarryingBag;
            AnnounceOwner("闇バイト", "紙袋を受け取った。家の裏の売人まで運ぼう。", NightEventManager.StyleDanger);
            return;
        }

        if (state.Value != JobState.CarryingBag) return;

        string content = SmugglingConfig.GetContentName(contentIndex.Value);
        state.Value = JobState.None;
        contentIndex.Value = -1;

        PlayerEarning earning = GetComponent<PlayerEarning>();
        if (earning != null &&
            SharedMoneyManager.Instance != null &&
            SharedMoneyManager.Instance.TryAdd(
                SmugglingConfig.SuccessReward,
                SharedMoneyReason.SmugglingReward,
                $"client={OwnerClientId}"))
        {
            earning.AddEarning(SmugglingConfig.SuccessReward);
        }

        AnnounceOwner(
            "運び屋 成功 +" + SmugglingConfig.SuccessReward + "R",
            "紙袋の中身は「" + content + "」だった。",
            NightEventManager.StyleFun);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void JailLaborServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (state.Value != JobState.JailLabor || !IsNearLaborStation()) return;

        laborProgress.Value++;
        if (laborProgress.Value < SmugglingConfig.JailLaborCount)
        {
            AnnounceOwner(
                "牢屋の労働",
                $"作業 {laborProgress.Value}/{SmugglingConfig.JailLaborCount}",
                NightEventManager.StyleInfo);
            return;
        }

        state.Value = JobState.None;
        laborProgress.Value = 0;
        AnnounceOwner("釈放", "労働を終えて牢屋から解放された。", NightEventManager.StylePeace);

        PlayerSpawnSync spawn = GetComponent<PlayerSpawnSync>();
        if (spawn != null) spawn.ServerRespawn();
    }

    /// <summary>警察のサーバーAIから呼ぶ。</summary>
    public void ServerArrest()
    {
        if (!IsServer || state.Value != JobState.CarryingBag) return;

        ClearServerChaseState();
        state.Value = JobState.ArrestedWaitingMorning;
        contentIndex.Value = -1;
        laborProgress.Value = 0;

        int charged = SharedMoneyManager.Instance != null
            ? SharedMoneyManager.Instance.SpendUpTo(
                SmugglingConfig.ArrestFine,
                SharedMoneyReason.ArrestFine,
                $"client={OwnerClientId}")
            : 0;

        PlayerEarning earning = GetComponent<PlayerEarning>();
        if (earning != null && charged > 0) earning.SpendEarning(charged);

        ArrestOwnerRpc();
        StartCoroutine(ServerMoveToJailAfterArrest());
    }

    /// <summary>朝の通常リスポーンを行わず、牢屋へ戻す必要があるか。</summary>
    public bool ServerShouldStayInJail()
    {
        return IsServer && IsJailed;
    }

    public void ServerSendToJail()
    {
        if (!IsServer) return;
        SmugglingJailPoint point = SmugglingJailPoint.Find();
        if (point == null)
        {
            Debug.LogWarning("[Smuggling] SmugglingJailPoint がシーンにありません。");
            return;
        }

        TeleportOwnerRpc(point.transform.position, point.transform.eulerAngles.y);
    }

    private IEnumerator ServerMoveToJailAfterArrest()
    {
        yield return new WaitForSecondsRealtime(1.7f);
        ServerSendToJail();
    }

    private void OnMorningArrived()
    {
        if (!IsServer) return;

        if (state.Value == JobState.ArrestedWaitingMorning)
        {
            state.Value = JobState.JailLabor;
            AnnounceOwner(
                "牢屋の朝",
                $"作業台で労働しよう（E × {SmugglingConfig.JailLaborCount}回）。",
                NightEventManager.StyleDanger);
        }
        else if (state.Value == JobState.CarryingBag)
        {
            // 夜の終了時に未達成の依頼は失効する。
            state.Value = JobState.None;
            contentIndex.Value = -1;
        }
    }

    private bool IsNearContact(SmugglingContact.ContactRole role)
    {
        foreach (SmugglingContact contact in FindObjectsByType<SmugglingContact>())
        {
            if (contact.Role == role && Vector3.Distance(transform.position, contact.transform.position) <= contactDistance)
                return true;
        }
        return false;
    }

    private bool IsNearLaborStation()
    {
        foreach (SmugglingJailLabor station in FindObjectsByType<SmugglingJailLabor>())
        {
            if (Vector3.Distance(transform.position, station.transform.position) <= laborDistance) return true;
        }
        return false;
    }

    private void AnnounceOwner(string title, string body, byte style)
    {
        if (DayManager.Instance != null)
            DayManager.Instance.ServerSendAnnounceTo(OwnerClientId, title, body, style);
    }

    private void ClearServerChaseState()
    {
        if (!IsServer || activeChaseSources.Count == 0) return;
        activeChaseSources.Clear();
        SetChaseStateOwnerRpc(false);
    }

    [Rpc(SendTo.Owner)]
    private void SetChaseStateOwnerRpc(bool chasing)
    {
        SetLocalChaseOverlay(chasing);
    }

    private static void SetLocalChaseOverlay(bool chasing)
    {
        ArrestCinematic cinematic = FindCinematic();
        if (cinematic != null) cinematic.SetChasing(chasing);
    }

    /// <summary>
    /// 逮捕演出はシーンに置いてある実体を使う（実行時に作らない）。
    /// 置き忘れていたら警告だけ出して、ゲームは止めない。
    /// </summary>
    private static ArrestCinematic FindCinematic()
    {
        ArrestCinematic cinematic = ArrestCinematic.Instance != null
            ? ArrestCinematic.Instance
            : FindAnyObjectByType<ArrestCinematic>();

        if (cinematic == null)
            Debug.LogWarning("[Smuggling] ArrestCinematic がシーンにありません。" +
                             "メニュー Roomies/Smuggling/Place Arrest Cinematic In Open Scene で置いてください。");

        return cinematic;
    }

    private void OnStateChanged(JobState oldState, JobState newState)
    {
        RefreshBagVisual();
    }

    private void RefreshBagVisual()
    {
        if (!IsSpawned) return;

        if (!HasBag)
        {
            if (bagVisual != null) Destroy(bagVisual);
            bagVisual = null;
            return;
        }

        if (bagVisual != null) return;

        if (bagVisualPrefab != null)
        {
            bagVisual = Instantiate(bagVisualPrefab, transform);
        }
        else
        {
            bagVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bagVisual.name = "PaperBag_Placeholder";
            bagVisual.transform.SetParent(transform, false);
            Renderer renderer = bagVisual.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.56f, 0.36f, 0.18f);
            Collider col = bagVisual.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        bagVisual.transform.localPosition = bagLocalPosition;
        bagVisual.transform.localRotation = Quaternion.Euler(bagLocalEuler);
        bagVisual.transform.localScale = Vector3.one * Mathf.Max(0.01f, bagScale);
    }

    [Rpc(SendTo.Owner)]
    private void ArrestOwnerRpc()
    {
        ArrestCinematic cinematic = FindCinematic();
        if (cinematic != null) cinematic.PlayArrest();
        StartCoroutine(LocalControlLockRoutine());
    }

    private IEnumerator LocalControlLockRoutine()
    {
        localControlLocked = true;
        // 演出（サイレン→白→暗転→牢屋で明転）が終わるまで操作を止める
        yield return new WaitForSecondsRealtime(2.9f);
        localControlLocked = false;
    }

    [Rpc(SendTo.Owner)]
    private void TeleportOwnerRpc(Vector3 position, float yaw)
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        if (controller != null) controller.enabled = true;
    }
}
