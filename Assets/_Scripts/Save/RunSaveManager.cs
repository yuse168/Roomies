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
///  ・途中：ターンが変わる・家具が増減/移動する・お金が動く・冷蔵庫が変わるたびに上書き保存する
///  ・終わり：ホストがメニューへ戻る／ゲームを閉じる直前にもう一度保存する（<see cref="TrySaveNow"/>）
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

    [Header("ゲームオーバー")]
    [Tooltip("家賃を払えずに負けたら、セーブを消す（その部屋の続きは無くなる）。")]
    [SerializeField] private bool deleteSaveOnGameOver = true;

    [Header("続きから")]
    [Tooltip("参加者の所持金を名前で突き合わせて戻す。")]
    [SerializeField] private bool restorePlayerMoney = true;

    private float nextCheckTime;
    private float nextForceSaveTime;
    private string lastSignature;

    private bool applied;
    private bool deletedForThisRun;
    private readonly List<SavedPlayerMoney> pendingPlayerMoney = new List<SavedPlayerMoney>();

    /// <summary>GameRoomに置いてある実体（無ければ null）。</summary>
    public static RunSaveManager Active { get; private set; }

    private void OnEnable()
    {
        Active = this;
        PlayerNameDisplay.ServerPlayerNamed += OnServerPlayerNamed;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        PlayerNameDisplay.ServerPlayerNamed -= OnServerPlayerNamed;
    }

    /// <summary>ウィンドウを閉じた（Alt+F4など）ときも、最後の状態を残す。</summary>
    private void OnApplicationQuit()
    {
        ServerSaveNow();
    }

    /// <summary>
    /// いまの進行を**すぐに**保存する。ホストがメニューへ戻る／終了する直前に呼ぶ。
    /// 保存できたら true。ホストでない・負けている・保存を切っている・書けなかったときは false
    /// （呼ぶ側は「保存されています」と言ってよいかの判断に使う）。
    /// </summary>
    public static bool TrySaveNow() => Active != null && Active.ServerSaveNow();

    /// <summary><see cref="TrySaveNow"/> の中身。</summary>
    public bool ServerSaveNow()
    {
        if (!IsServer || !autoSave || !applied) return false;

        DayManager day = DayManager.Instance;
        if (day == null || !day.IsSpawned || day.IsGameOver) return false;

        RoomiesSaveData data = ServerCapture();
        if (data == null || !SaveSystem.Write(data)) return false;

        lastSignature = Signature(data);
        nextForceSaveTime = Time.time + forceSaveInterval;
        return true;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (!applied)
        {
            TryApplyPendingLoad();
            return;
        }

        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        DayManager day = DayManager.Instance;
        if (day == null || !day.IsSpawned) return;

        // 負けたらセーブを消す（「負ける直前」から何度も始められないように）。
        // やり直すと IsGameOver が戻るので、そのときはまた保存を始める
        if (day.IsGameOver)
        {
            if (deleteSaveOnGameOver && !deletedForThisRun)
            {
                deletedForThisRun = true;
                lastSignature = null;
                if (SaveSystem.Exists() && SaveSystem.Delete())
                    Debug.Log("[Save] ゲームオーバーなのでセーブを消しました");
            }
            return;
        }

        deletedForThisRun = false;

        if (!autoSave) return;

        // 朝↔夜の切り替え演出の最中は書かない（家賃の支払いなどが半分だけ済んだ状態を残さないため）
        if (day.IsTransitioning) return;

        RoomiesSaveData data = ServerCapture();
        if (data == null) return;

        // 中身が変わっていないときは書かない（ディスクを触る回数を減らす）
        string signature = Signature(data);
        bool forced = forceSaveInterval > 0f && Time.time >= nextForceSaveTime;
        if (signature == lastSignature && !forced) return;

        if (SaveSystem.Write(data))
        {
            lastSignature = signature;
            nextForceSaveTime = Time.time + forceSaveInterval;
        }
    }

    /// <summary>
    /// 「前回の保存から変わったか」を見るための文字列。残り秒は毎フレーム変わるので入れない
    /// （残り秒だけの更新は forceSaveInterval ごとの保存に任せる）。
    /// </summary>
    private static string Signature(RoomiesSaveData data)
    {
        var sb = new System.Text.StringBuilder(128);
        sb.Append(data.day).Append('/').Append(data.time).Append('/')
          .Append(data.sharedMoney).Append('/').Append(data.rentSurcharge).Append('/')
          .Append(data.fridgeStocked ? 1 : 0).Append(data.fridgeBroken ? 1 : 0);

        foreach (SavedFurniture f in data.furniture)
        {
            // 置き直しに気づければよいので 10cm 単位に丸める（物理の細かい揺れでは書かない）
            sb.Append('|').Append(f.catalogIndex).Append(f.effectActive ? '+' : '-')
              .Append(Mathf.RoundToInt(f.position.x * 10f)).Append(',')
              .Append(Mathf.RoundToInt(f.position.y * 10f)).Append(',')
              .Append(Mathf.RoundToInt(f.position.z * 10f)).Append(',')
              .Append(Mathf.RoundToInt(f.yaw / 5f));
        }

        foreach (SavedPlayerMoney p in data.players)
            sb.Append('|').Append(p.name).Append('=').Append(p.earning);

        return sb.ToString();
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
        ServerApply(data);
    }

    /// <summary>
    /// セーブの中身を部屋へ反映する（サーバーのみ）。DayManagerと口座がそろっていなければ false。
    /// 途中で1つ失敗しても残りは続ける（家具が1つ置けなくても、日数とお金は戻す）。
    /// </summary>
    public bool ServerApply(RoomiesSaveData data)
    {
        if (data == null || !IsServer) return false;

        DayManager day = DayManager.Instance;
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (day == null || !day.IsSpawned || money == null || !money.IsSpawned) return false;

        applied = true;

        day.ServerApplySavedState(data.day, data.time, data.remainingSeconds, data.rentSurcharge);
        money.ServerRestoreBalance(data.sharedMoney);

        NightFridge fridge = NightFridge.Instance;
        if (fridge != null && fridge.IsSpawned)
            fridge.ServerRestoreState(data.fridgeStocked, data.fridgeBroken);

        int spawned = 0;
        int total = data.furniture != null ? data.furniture.Count : 0;
        if (data.furniture != null)
        {
            foreach (SavedFurniture saved in data.furniture)
            {
                if (saved == null) continue;

                NetworkFurniture furniture =
                    day.ServerSpawnFurniture(saved.catalogIndex, saved.position, saved.yaw);
                if (furniture == null) continue;

                // 効いていた家具は効いたまま戻す（戻さないと翌朝まで効果が消える）
                if (saved.effectActive) furniture.ServerActivateEffect();
                spawned++;
            }
        }

        pendingPlayerMoney.Clear();
        if (restorePlayerMoney && data.players != null) pendingPlayerMoney.AddRange(data.players);

        // すでに入っている人（ホスト自身など）にも戻す
        RestoreAlreadySpawnedPlayers();

        Debug.Log($"[Save] 続きから開始: Day{data.day} {(data.time == 1 ? "夜" : "朝")} " +
                  $"口座¥{data.sharedMoney} 家具{spawned}/{total}個");
        return true;
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

    /// <summary>いまの部屋の状態をセーブの形にまとめる（サーバーのみ）。そろっていなければ null。</summary>
    public RoomiesSaveData ServerCapture()
    {
        if (!IsServer) return null;

        DayManager day = DayManager.Instance;
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (day == null || !day.IsSpawned || money == null || !money.IsSpawned) return null;

        var data = new RoomiesSaveData
        {
            day = day.CurrentDay,
            time = day.IsNight ? 1 : 0,
            remainingSeconds = day.RemainingSeconds,
            sharedMoney = money.CurrentMoney,
            rentSurcharge = day.RentSurcharge,
            hostName = LocalPlayerName(),
        };

        NightFridge fridge = NightFridge.Instance;
        if (fridge != null && fridge.IsSpawned)
        {
            data.fridgeStocked = fridge.IsStocked;
            data.fridgeBroken = fridge.IsBroken;
        }

        foreach (NetworkFurniture furniture in FindObjectsByType<NetworkFurniture>(FindObjectsSortMode.None))
        {
            if (furniture == null || !furniture.IsSpawned) continue;
            if (furniture.CatalogIndex < 0) continue;

            data.furniture.Add(new SavedFurniture
            {
                catalogIndex = furniture.CatalogIndex,
                position = furniture.transform.position,
                yaw = furniture.transform.eulerAngles.y,
                effectActive = furniture.IsEffectActive,
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
