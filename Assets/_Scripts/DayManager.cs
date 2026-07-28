using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class DayManager : NetworkBehaviour
{
    [Header("スカイドーム")]
    [SerializeField] private Material morningSkyMaterial;
    [SerializeField] private Material nightSkyMaterial;

    [Header("Day設定")]
    [SerializeField] private int maxDay = 3;

    [Header("家賃設定")]
    [SerializeField] private int rentAmount = 500;

    [Header("ターン時間")]
    [SerializeField] private float turnDuration = 300f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private DayTransitionUI transitionUI;
    [SerializeField] private DayResultUI resultUI;
    [SerializeField] private RentPaymentUI rentPaymentUI;

    [Header("家具のマルチ同期")]
    [Tooltip("NetworkObject + NetworkFurniture を付けた家具プレハブ。NetworkManagerのNetworkPrefabにも登録すること。")]
    [SerializeField] private GameObject networkFurniturePrefab;

    /// <summary>家具をネットワーク同期で生成できるか（プレハブ設定済みか）。</summary>
    public bool CanSpawnNetworkFurniture => networkFurniturePrefab != null;

    // 朝への切り替え演出中フラグ（サーバー側でタイマー・再入を止める）
    private bool isTransitioning = false;

    // 各プレイヤーの「その日の開始時点」の稼ぎ（今日の収支計算用）
    private readonly System.Collections.Generic.Dictionary<ulong, int> dayStartEarning
        = new System.Collections.Generic.Dictionary<ulong, int>();

    // 他スクリプト（家具編集など）から参照するためのアクセサ
    public static DayManager Instance { get; private set; }
    /// <summary>現在が夜ターンかどうか。</summary>
    public bool IsNight => currentTime != null && currentTime.Value == 1;

    /// <summary>残り時間（秒）。HUDの進捗バーなどが参照する。</summary>
    public float RemainingSeconds => remainingTime != null ? remainingTime.Value : 0f;

    /// <summary>1ターンの長さ（秒）。</summary>
    public float TurnDurationSeconds => turnDuration;

    // 診断用
    public int  DebugDay     => currentDay  != null ? currentDay.Value  : -1;
    public int  DebugTime    => currentTime != null ? currentTime.Value : -1;
    public bool DebugSpawned => NetworkObject != null && NetworkObject.IsSpawned;

    /// <summary>夜→朝に切り替わった瞬間に発火（全クライアント）。家具の配達などに使う。</summary>
    public static event System.Action OnMorningArrived;

    /// <summary>朝→夜に切り替わった瞬間に発火（全クライアント）。夜イベントの抽選などに使う。</summary>
    public static event System.Action OnNightArrived;

    // 夜イベント等による次回家賃への上乗せ額（サーバーのみ管理）
    private int rentSurcharge = 0;

    /// <summary>次回の家賃徴収額（基本額＋上乗せ）。</summary>
    public int CurrentRentTotal => rentAmount + rentSurcharge;

    /// <summary>夜イベントなどから次回家賃に上乗せする（サーバーのみ）。</summary>
    public void ServerAddRentSurcharge(int amount)
    {
        if (!IsServer || amount <= 0) return;
        rentSurcharge += amount;
    }

    private NetworkVariable<int> currentDay = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 0 = 朝
    // 1 = 夜
    private NetworkVariable<int> currentTime = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isGameOver = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> remainingTime = new NetworkVariable<float>(
        300f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        // DayManagerはNetworkObjectと同じGameObjectに1つだけ置く。
        // UIへ誤って追加されたコンポーネントが昼夜状態を上書きしないよう防止する。
        if (GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"[DayManager] {name} にはNetworkObjectがありません。重複・誤配置として無効化します。");
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[DayManager] DayManagerが重複しています。{name} を無効化します。");
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (!enabled) return;

        currentDay.OnValueChanged += OnDayChanged;
        currentTime.OnValueChanged += OnTimeChanged;
        isGameOver.OnValueChanged += OnGameOverChanged;

        // 全画面の朝演出UIが未設定ならランタイムで生成する（シーン配置不要）
        if (transitionUI == null)
        {
            var go = new GameObject("DayTransitionUI");
            transitionUI = go.AddComponent<DayTransitionUI>();
        }

        // 収支リザルトUIも同様に生成
        if (resultUI == null)
        {
            var go = new GameObject("DayResultUI");
            resultUI = go.AddComponent<DayResultUI>();
        }

        // 3日目の家賃支払い演出も各クライアントでランタイム生成
        if (rentPaymentUI == null)
        {
            var go = new GameObject("RentPaymentUI");
            rentPaymentUI = go.AddComponent<RentPaymentUI>();
        }

        // 家具編集コントローラがシーンに無ければ生成する（仮カタログで動作）。
        // 見た目を差し替えたい場合は GameRoom に FurnitureEditController を手動で置き、
        // catalog に Prefab を割り当てること（手動配置があればそちらが使われる）。
        if (FindAnyObjectByType<FurnitureEditController>() == null)
        {
            var fgo = new GameObject("FurnitureEditController");
            fgo.AddComponent<FurnitureEditController>();
        }

        // 夜イベント管理もシーンに無ければ生成する（シーン配置不要）
        if (FindAnyObjectByType<NightEventManager>() == null)
        {
            var ngo = new GameObject("NightEventManager");
            ngo.AddComponent<NightEventManager>();
        }

        UpdateDayUI();
        UpdateTimerUI();
        ApplySkyMaterial(currentTime.Value);
        ApplySmugglingTime(currentTime.Value);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            remainingTime.Value = turnDuration;
            SnapshotDayStartEarnings();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (Instance == this) Instance = null;

        currentDay.OnValueChanged -= OnDayChanged;
        currentTime.OnValueChanged -= OnTimeChanged;
        isGameOver.OnValueChanged -= OnGameOverChanged;
    }

    private void Update()
    {
        if (isGameOver.Value) return;

        UpdateTimerUI();

        if (Keyboard.current != null &&
            Keyboard.current.nKey.wasPressedThisFrame)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NextDayServerRpc();
            }
        }

        if (!IsServer) return;

        // 朝への切り替え演出中はタイマーを止める
        if (isTransitioning) return;

        remainingTime.Value -= Time.deltaTime;

        if (remainingTime.Value <= 0f)
        {
            NextDayServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void NextDayServerRpc()
    {
        if (isGameOver.Value) return;
        if (isTransitioning) return;

        remainingTime.Value = turnDuration;

        // 朝 → 夜（演出なし）
        if (currentTime.Value == 0)
        {
            currentTime.Value = 1;
            return;
        }

        // 夜 → 新しい日の朝。次の日番号を決めてから演出付きで切り替える。
        int nextDay;
        bool collectRent = false;
        if (currentDay.Value < maxDay)
        {
            nextDay = currentDay.Value + 1;
        }
        else
        {
            nextDay = 1;
            collectRent = true;
        }

        StartCoroutine(NightEndRoutine(nextDay, collectRent));
    }

    /// <summary>
    /// 夜終わりのシーケンス（サーバー進行）。
    /// 1) 今日の収支ランキングを全画面でドン！ドン！表示
    /// 2) 続けて「○日目」の朝演出 → 暗転中に日付更新＆全員リスポーン → 朝再開
    /// </summary>
    private IEnumerator NightEndRoutine(int nextDay, bool collectRent)
    {
        isTransitioning = true;

        // ---- 1) 今日の収支リザルト ----
        string resultData = BuildResultData(out int playerCount);
        ShowDayResultClientRpc(resultData);
        yield return new WaitForSeconds(DayResultUI.EstimatedDuration(playerCount));
        HideDayResultClientRpc();

        // ---- 2) 3日目だけ、全員へ家賃支払いフェーズを表示 ----
        if (collectRent)
        {
            int total = rentAmount + rentSurcharge;
            SharedMoneyManager money = SharedMoneyManager.Instance;
            int balance = money != null
                ? money.CurrentMoney
                : 0;
            bool canPay = money != null &&
                          money.TrySpend(
                              total,
                              SharedMoneyReason.Rent,
                              $"Day {currentDay.Value}");

            if (canPay)
            {
                rentSurcharge = 0;
                Debug.Log($"家賃支払い成功（¥{total}）");
            }
            else
            {
                Debug.Log("共同口座のお金が足りない！");
            }

            PlayRentPaymentClientRpc(total, balance, canPay);

            float duration = canPay
                ? RentPaymentUI.SuccessDuration
                : RentPaymentUI.FailureDuration;
            yield return new WaitForSecondsRealtime(duration);
            HideRentPaymentClientRpc();

            if (!canPay)
            {
                isTransitioning = false;
                GameOver();
                yield break;
            }
        }

        // ---- 3) 翌朝の演出 ----
        PlayDayTransitionClientRpc(nextDay);
        yield return new WaitForSeconds(DayTransitionUI.BlackoutDelay);

        // 暗転中に日付・時間を更新（OnTimeChangedで空も朝に変わる）
        currentDay.Value = nextDay;
        currentTime.Value = 0;
        remainingTime.Value = turnDuration;

        RespawnAllPlayers();
        SnapshotDayStartEarnings();      // 新しい日の基準を記録
        ActivateAllFurnitureEffects();   // 家具の効果を翌朝から有効化（同期）

        yield return new WaitForSeconds(2.6f);

        isTransitioning = false;
    }

    /// <summary>家具をネットワーク同期で生成する（サーバー権限・代金もここで引く）。</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyFurnitureServerRpc(
        int catalogIndex,
        Vector3 ground,
        float yaw,
        RpcParams rpcParams = default)
    {
        ulong requester = rpcParams.Receive.SenderClientId;
        if (networkFurniturePrefab == null)
        {
            FurniturePurchaseResultClientRpc(
                requester, false, catalogIndex,
                "同期用家具Prefabが設定されていません");
            return;
        }

        var item = FurnitureCatalog.Get(catalogIndex);
        if (item == null)
        {
            FurniturePurchaseResultClientRpc(
                requester, false, catalogIndex,
                "商品データが見つかりません");
            return;
        }

        var money = SharedMoneyManager.Instance;
        if (money == null)
        {
            FurniturePurchaseResultClientRpc(
                requester, false, catalogIndex,
                "共有口座が見つかりません");
            return;
        }
        if (!money.TrySpend(
                item.cost,
                SharedMoneyReason.FurniturePurchase,
                $"client={requester}, item={item.id}"))
        {
            FurniturePurchaseResultClientRpc(
                requester, false, catalogIndex,
                $"残高不足（必要 ¥{item.cost}）");
            return;
        }

        // 仮ブロックの中心 = 地面 + 高さ/2（底が地面に乗るように）
        Vector3 center = new Vector3(ground.x, ground.y + item.placeholderSize.y * 0.5f, ground.z);

        var go = Instantiate(networkFurniturePrefab, center, Quaternion.Euler(0f, yaw, 0f));
        var no = go.GetComponent<NetworkObject>();
        no.Spawn(true);

        var nf = go.GetComponent<NetworkFurniture>();
        if (nf != null) nf.ServerSetIndex(catalogIndex);

        FurniturePurchaseResultClientRpc(
            requester, true, catalogIndex,
            $"{item.displayName}を配達しました");
    }

    [ClientRpc]
    private void FurniturePurchaseResultClientRpc(
        ulong targetClientId,
        bool success,
        int catalogIndex,
        string message)
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        FurnitureEditController controller =
            FindAnyObjectByType<FurnitureEditController>();
        if (controller != null)
            controller.OnServerPurchaseResult(
                success,
                catalogIndex,
                message);
    }

    /// <summary>朝に全ての同期家具の効果を有効化する（サーバー）。</summary>
    private void ActivateAllFurnitureEffects()
    {
        if (!IsServer) return;
        foreach (var nf in FindObjectsByType<NetworkFurniture>(FindObjectsSortMode.None))
        {
            nf.ServerActivateEffect();
        }
    }

    // 各プレイヤーの現在の稼ぎを「その日の開始値」として記録
    private void SnapshotDayStartEarnings()
    {
        dayStartEarning.Clear();
        var players = FindObjectsByType<PlayerEarning>(FindObjectsSortMode.None);
        foreach (var pe in players)
        {
            var no = pe.GetComponent<NetworkObject>();
            if (no != null) dayStartEarning[no.NetworkObjectId] = pe.GetEarning();
        }
    }

    // 今日の収支ランキングを "名前|金額" 改行区切り（降順）で作る
    private string BuildResultData(out int playerCount)
    {
        var list = new System.Collections.Generic.List<(string name, int amount)>();
        var players = FindObjectsByType<PlayerEarning>(FindObjectsSortMode.None);

        foreach (var pe in players)
        {
            int baseline = 0;
            var no = pe.GetComponent<NetworkObject>();
            if (no != null) dayStartEarning.TryGetValue(no.NetworkObjectId, out baseline);

            int today = pe.GetEarning() - baseline;

            string name = "Player";
            var nd = pe.GetComponent<PlayerNameDisplay>();
            if (nd != null) name = nd.GetPlayerName();

            list.Add((name, today));
        }

        list.Sort((a, b) => b.amount.CompareTo(a.amount)); // 金額の降順

        var sb = new System.Text.StringBuilder();
        foreach (var e in list)
            sb.AppendLine($"{e.name}|{e.amount}");

        playerCount = list.Count;
        return sb.ToString().TrimEnd();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowDayResultClientRpc(string data)
    {
        if (resultUI != null) resultUI.Play(data);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HideDayResultClientRpc()
    {
        if (resultUI != null) resultUI.Hide();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayRentPaymentClientRpc(int rent, int balance, bool canPay)
    {
        if (rentPaymentUI != null)
            rentPaymentUI.Play(rent, balance, canPay);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HideRentPaymentClientRpc()
    {
        if (rentPaymentUI != null)
            rentPaymentUI.Hide();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayDayTransitionClientRpc(int day)
    {
        if (transitionUI != null)
        {
            transitionUI.Play(day);
        }
    }

    /// <summary>接続中の全プレイヤーをスポーン地点へ戻す（サーバーのみ）。</summary>
    private void RespawnAllPlayers()
    {
        if (!IsServer || NetworkManager == null) return;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var spawnSync = playerObj.GetComponent<PlayerSpawnSync>();
            if (spawnSync != null)
            {
                spawnSync.ServerRespawn();
            }
        }
    }

    // ================================================================
    // 夜イベント連携（NightEventManagerはNetworkBehaviourではないため、
    // シーン配置済みのこのオブジェクトがRPCを中継する）
    // ================================================================

    /// <summary>夜イベントの演出を全クライアントへ送る（サーバーのみ）。</summary>
    public void ServerSendNightEvent(byte eventType, int value)
    {
        if (!IsServer) return;
        NightEventClientRpc(eventType, value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NightEventClientRpc(byte eventType, int value)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.PlayEventVisual(eventType, value);
    }

    /// <summary>全員にバナー告知を送る（サーバーのみ）。</summary>
    public void ServerSendAnnounce(string title, string body, byte style)
    {
        if (!IsServer) return;
        AnnounceClientRpc(title, body, style);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceClientRpc(string title, string body, byte style)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ShowBanner(title, body, style);
    }

    /// <summary>特定プレイヤーだけにバナー告知を送る（サーバーのみ）。</summary>
    public void ServerSendAnnounceTo(ulong clientId, string title, string body, byte style)
    {
        if (!IsServer) return;
        AnnounceToClientRpc(title, body, style,
            RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AnnounceToClientRpc(string title, string body, byte style, RpcParams rpcParams = default)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ShowBanner(title, body, style);
    }

    /// <summary>全員の画面を揺らす（サーバーのみ）。地震イベント用。</summary>
    public void ServerSendCameraShake(float duration, float magnitude)
    {
        if (!IsServer) return;
        ShakeCameraClientRpc(duration, magnitude);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShakeCameraClientRpc(float duration, float magnitude)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ShakeCamera(duration, magnitude);
    }

    private void GameOver()
    {
        if (!IsServer) return;

        isGameOver.Value = true;

        Debug.Log("GAME OVER");
    }

    private void OnDayChanged(int oldDay, int newDay)
    {
        UpdateDayUI();
    }

    private void OnTimeChanged(int oldTime, int newTime)
    {
        UpdateDayUI();
        ApplySkyMaterial(newTime);
        ApplySmugglingTime(newTime);

        // 夜(1)→朝(0) になったら配達などのトリガーを発火
        if (newTime == 0)
        {
            OnMorningArrived?.Invoke();
        }
        // 朝(0)→夜(1) になったら夜イベントのトリガーを発火
        else if (newTime == 1)
        {
            OnNightArrived?.Invoke();
        }
    }

    /// <summary>運び屋NPCへ時間帯を直接反映する。イベント購読順に依存させない。</summary>
    private void ApplySmugglingTime(int time)
    {
        bool isNight = time == 1;
        int count = SmugglingContact.ApplyNightStateToAll(isNight);
        Debug.Log($"[Smuggling] DayManagerが時間帯を適用: isNight={isNight}, contacts={count}");
    }

    private void OnGameOverChanged(bool oldValue, bool newValue)
    {
        UpdateDayUI();
        UpdateTimerUI();
    }

    private void UpdateDayUI()
    {
        if (dayText == null) return;

        if (isGameOver.Value)
        {
            dayText.text = "GAME OVER";
            return;
        }

        string timeText = currentTime.Value == 0 ? "朝" : "夜";

        dayText.text = "DAY " + currentDay.Value + " " + timeText;
    }

    private void ApplySkyMaterial(int time)
    {
        Material skyMaterial = time == 0 ? morningSkyMaterial : nightSkyMaterial;

        if (skyMaterial != null)
        {
            RenderSettings.skybox = skyMaterial;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        if (isGameOver.Value)
        {
            timerText.text = "";
            return;
        }

        int totalSeconds = Mathf.CeilToInt(remainingTime.Value);

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text =
            minutes.ToString("00") + ":" +
            seconds.ToString("00");
    }
}
