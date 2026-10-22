using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 部屋の進行をホストの端末へ保存し、「続きから」を反映する係。
/// GameRoomシーンに**実体として1つ置く**（メニュー `Roomies/Save/Build Save Manager`）。
///
/// ホスト（サーバー）でだけ動く。参加している人数は関係なく、
/// 1人で始めても、あとから友達が入ってきても同じように進む。
///
/// やること：
///  ・開始時：ロビーで選んだセーブ（<see cref="SaveSystem.PendingLoad"/>）を部屋へ反映する
///  ・途中：ターンが変わる・家具が増減する・お金が動くたびに上書き保存する
///  ・参加者が入ってきたら、セーブにある同じ名前の人の所持金を戻す
///
/// 規約（AGENTS.md）：実行時に見た目を作らない。間隔や挙動はインスペクターで変えられる。
/// </summary>
public class RunSaveManager : MonoBehaviour
{
    [Header("オートセーブ")]
    [Tooltip("状態が変わっていないか調べる間隔（秒）。変わっていたときだけ書き込む。")]
    [SerializeField, Min(0.5f)] private float checkInterval = 2f;

    [Tooltip("変化が無くてもこの秒数ごとに保存し直す。0で無効。")]
    [SerializeField, Min(0f)] private float forceSaveInterval = 60f;

    [Tooltip("切ると保存しなくなる（デバッグ用）。")]
    [SerializeField] private bool autoSave = true;

    [Header("続きから")]
    [Tooltip("参加者の所持金を名前で突き合わせて戻す。")]
    [SerializeField] private bool restorePlayerMoney = true;

    private float nextCheckTime;
    private float nextForceSaveTime;
    private string lastSignature;

    private bool applied;
    private readonly List<SavedPlayerMoney> pendingPlayerMoney = new List<SavedPlayerMoney>();

    private void OnEnable()
    {
        PlayerNameDisplay.ServerPlayerNamed += OnServerPlayerNamed;
    }

    private void OnDisable()
    {
        PlayerNameDisplay.ServerPlayerNamed -= OnServerPlayerNamed;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (!applied)
        {
            TryApplyPendingLoad();
            return;
        }

        if (!autoSave || Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        DayManager day = DayManager.Instance;
        if (day == null || !day.IsSpawned || day.IsGameOver) return;

        RoomiesSaveData data = Capture(day);
        if (data == null) return;

        // 中身が変わっていないときは書かない（ディスクを触る回数を減らす）
        string signature = $"{data.day}/{data.time}/{data.sharedMoney}/{data.rentSurcharge}/{data.furniture.Count}";
        bool forced = forceSaveInterval > 0f && Time.time >= nextForceSaveTime;
        if (signature == lastSignature && !forced) return;

        if (SaveSystem.Write(data))
        {
            lastSignature = signature;
            nextForceSaveTime = Time.time + forceSaveInterval;
        }
    }

    private static bool IsServer
    {
        get
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening && nm.IsServer;
        }
    }

    // ---- 続きから ----

    /// <summary>
    /// ロビーで選んだセーブを部屋へ反映する。
    /// DayManagerと口座がそろうまで毎フレーム待つ（シーンの準備が終わるのを待つだけ）。
    /// </summary>
    private void TryApplyPendingLoad()
    {
        DayManager day = DayManager.Instance;
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (day == null || !day.IsSpawned || money == null || !money.IsSpawned) return;

        RoomiesSaveData data = SaveSystem.PendingLoad;
        applied = true;
        nextCheckTime = Time.time + checkInterval;
        nextForceSaveTime = Time.time + forceSaveInterval;

        if (data == null)
        {
            // 新しく始めた場合。いまの状態をそのまま1枠目として保存する
            Debug.Log("[Save] 新規で開始しました（この部屋の進行をこれから保存します）");
            return;
        }

        SaveSystem.ClearPendingLoad();

        day.ServerApplySavedState(data.day, data.time, data.remainingSeconds, data.rentSurcharge);
        money.ServerRestoreBalance(data.sharedMoney);

        int spawned = 0;
        foreach (SavedFurniture saved in data.furniture)
        {
            if (day.ServerSpawnFurniture(saved.catalogIndex, saved.position, saved.yaw) != null) spawned++;
        }

        pendingPlayerMoney.Clear();
        if (restorePlayerMoney && data.players != null) pendingPlayerMoney.AddRange(data.players);

        // すでに入っている人（ホスト自身など）にも戻す
        RestoreAlreadySpawnedPlayers();

        Debug.Log($"[Save] 続きから開始: Day{data.day} {(data.time == 1 ? "夜" : "朝")} " +
                  $"口座¥{data.sharedMoney} 家具{spawned}/{data.furniture.Count}個");
    }

    private void RestoreAlreadySpawnedPlayers()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;

        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            var display = client.PlayerObject.GetComponent<PlayerNameDisplay>();
            if (display != null) OnServerPlayerNamed(display);
        }
    }

    /// <summary>
    /// 名前が届いた人に、セーブにあった同じ名前の所持金を戻す。
    /// 1件は1回だけ使う（同じ名前が2人いても二重にはならない）。
    /// </summary>
    private void OnServerPlayerNamed(PlayerNameDisplay display)
    {
        if (!IsServer || !applied || display == null) return;
        if (pendingPlayerMoney.Count == 0) return;

        string name = display.PlayerName;
        if (string.IsNullOrEmpty(name)) return;

        for (int i = 0; i < pendingPlayerMoney.Count; i++)
        {
            if (pendingPlayerMoney[i].name != name) continue;

            int amount = Mathf.Max(0, pendingPlayerMoney[i].earning);
            pendingPlayerMoney.RemoveAt(i);

            if (amount <= 0) return;

            var earning = display.GetComponent<PlayerEarning>();
            if (earning == null) return;

            earning.AddEarning(amount);
            Debug.Log($"[Save] {name} の所持金 ¥{amount} を戻しました");
            return;
        }
    }

    // ---- いまの状態を集める ----

    private RoomiesSaveData Capture(DayManager day)
    {
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (money == null) return null;

        var data = new RoomiesSaveData
        {
            day = day.CurrentDay,
            time = day.IsNight ? 1 : 0,
            remainingSeconds = day.RemainingSeconds,
            sharedMoney = money.CurrentMoney,
            rentSurcharge = day.RentSurcharge,
            hostName = LocalPlayerName(),
        };

        foreach (NetworkFurniture furniture in FindObjectsByType<NetworkFurniture>(FindObjectsSortMode.None))
        {
            if (furniture == null || !furniture.IsSpawned) continue;
            if (furniture.CatalogIndex < 0) continue;

            data.furniture.Add(new SavedFurniture
            {
                catalogIndex = furniture.CatalogIndex,
                position = furniture.transform.position,
                yaw = furniture.transform.eulerAngles.y,
            });
        }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null)
        {
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;

                var display = client.PlayerObject.GetComponent<PlayerNameDisplay>();
                var earning = client.PlayerObject.GetComponent<PlayerEarning>();
                if (display == null || earning == null) continue;

                string name = display.PlayerName;
                if (string.IsNullOrEmpty(name)) continue;

                data.players.Add(new SavedPlayerMoney { name = name, earning = earning.GetEarning() });
            }
        }

        return data;
    }

    private static string LocalPlayerName()
    {
        NetworkManager nm = NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        var display = player != null ? player.GetComponent<PlayerNameDisplay>() : null;
        return display != null ? display.PlayerName : "";
    }
}
