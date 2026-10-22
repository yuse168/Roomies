using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 誰が寝ているかをまとめて見て、夜を終わらせる係。シーンに1つ置く。
///
/// ・**全員が寝たら朝にする**（夜の残り時間を待たずに進む）
/// ・朝まで寝ていた人は、その朝のあいだ体が軽い（ぐっすり）
/// ・何人寝たかは、寝るたびに告知で分かるようにする
///
/// サーバーだけが数える。寝ている人は <see cref="Bed"/> から知らせてもらう。
/// 値はインスペクターで変えられる（AGENTS.mdの規約）。
/// </summary>
public class SleepManager : NetworkBehaviour
{
    public static SleepManager Instance { get; private set; }

    [Header("ぐっすり（朝まで寝た人）")]
    [Tooltip("翌朝の移動速度にかける倍率。")]
    [SerializeField, Range(1f, 1.5f)] private float restedSpeed = 1.15f;

    [Tooltip("ぐっすりが続く秒数。朝のターンの長さに合わせる。")]
    [SerializeField, Min(10f)] private float restedSeconds = 180f;

    [Header("全員で寝たとき")]
    [Tooltip("全員寝てから朝になるまでの間（秒）。落ち着いて「おやすみ」が見える長さ。")]
    [SerializeField, Min(0f)] private float allAsleepDelay = 2f;

    [Tooltip("1人で遊んでいるときも、寝たら朝にする。")]
    [SerializeField] private bool allowSoloSkip = true;

    // いま寝ている人（サーバーのみ）
    private static readonly HashSet<ulong> sleeping = new HashSet<ulong>();
    private float morningAtTime = -1f;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        sleeping.Clear();
        DayManager.OnNightArrived += OnNightArrived;
    }

    public override void OnNetworkDespawn()
    {
        DayManager.OnNightArrived -= OnNightArrived;
        if (Instance == this) Instance = null;
        sleeping.Clear();
    }

    private void OnNightArrived()
    {
        sleeping.Clear();
        morningAtTime = -1f;
    }

    /// <summary>何人が寝ているか（表示用）。</summary>
    public static int SleepingCount => sleeping.Count;

    /// <summary>
    /// ベッドから知らせてもらう（サーバーのみ）。
    /// <see cref="Bed"/> 以外から呼ばない。
    /// </summary>
    public static void ServerNotifySleep(ulong clientId, bool isSleeping)
    {
        if (Instance == null || !Instance.IsServer) return;

        if (isSleeping) sleeping.Add(clientId);
        else sleeping.Remove(clientId);

        Instance.ServerOnSleepChanged(clientId, isSleeping);
    }

    private void ServerOnSleepChanged(ulong clientId, bool isSleeping)
    {
        int awake = Mathf.Max(0, ConnectedPlayers() - sleeping.Count);

        if (isSleeping)
        {
            Announce("おやすみ",
                awake > 0 ? $"あと {awake}人 で朝になる" : "全員ねむった……",
                NightEventManager.StyleInfo);
        }
        else
        {
            // 起きたら朝へのカウントをやめる
            morningAtTime = -1f;
        }

        if (!AllAsleep()) return;

        morningAtTime = Time.time + allAsleepDelay;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || morningAtTime < 0f) return;
        if (Time.time < morningAtTime) return;

        morningAtTime = -1f;
        if (!AllAsleep()) return;

        DayManager day = DayManager.Instance;
        if (day == null || !day.IsSpawned || day.IsGameOver || !day.IsNight) return;

        // 朝まで寝た人へ「ぐっすり」を配る（朝になってから効かせる）
        foreach (ulong client in sleeping) GiveRestedRpc(RpcTarget.Single(client, RpcTargetUse.Temp));

        DailyRequestManager.ServerNotifyEveryoneSlept();
        day.ServerSkipToMorning();
    }

    private bool AllAsleep()
    {
        int players = ConnectedPlayers();
        if (players <= 0) return false;
        if (players == 1 && !allowSoloSkip) return false;
        return sleeping.Count >= players;
    }

    private static int ConnectedPlayers()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return 0;

        int count = 0;
        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.IsSpawned) count++;
        }
        return count;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void GiveRestedRpc(RpcParams rpcParams = default)
    {
        NetworkManager nm = NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        var movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (movement == null) return;

        movement.ApplyTemporarySpeed(restedSpeed, restedSeconds);
    }

    private static void Announce(string title, string body, byte style)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounce(title, body, style);
    }
}
