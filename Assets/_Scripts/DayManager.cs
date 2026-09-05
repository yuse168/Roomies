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
    [SerializeField, Range(0f, 2f)] private float morningAmbientIntensity = 0.9f;
    [SerializeField, Range(0f, 2f)] private float nightAmbientIntensity = 0.5f;
    [SerializeField, Range(0f, 2f)] private float morningReflectionIntensity = 0.7f;
    [SerializeField, Range(0f, 2f)] private float nightReflectionIntensity = 0.45f;
    [SerializeField] private Light environmentSun;
    [SerializeField, Range(0f, 3f)] private float morningSunIntensity = 1.2f;
    [SerializeField, Range(0f, 1f)] private float nightSunIntensity = 0.12f;

    [Header("Day設定")]
    [SerializeField] private int maxDay = 3;

    [Header("家賃設定")]
    [SerializeField] private int rentAmount = 500;

    [Header("ターン時間")]
    [SerializeField, Min(10f)] private float turnDuration = 180f;

    [Header("開発用")]
    [Tooltip("Editor/Development BuildのHostでのみ、Nキーによるターン送りを許可します。")]
    [SerializeField] private bool enableDebugDaySkip;

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
    private readonly NetworkVariable<bool> isTransitioning = new(false);
    private int furnitureDeliveryCount;

    // 各プレイヤーの「その日の開始時点」の稼ぎ（今日の収支計算用）
    private readonly System.Collections.Generic.Dictionary<ulong, int> dayStartEarning
        = new System.Collections.Generic.Dictionary<ulong, int>();

    // 他スクリプト（家具編集など）から参照するためのアクセサ
    public static DayManager Instance { get; private set; }
    /// <summary>現在が夜ターンかどうか。</summary>
    public bool IsNight => currentTime != null && currentTime.Value == 1;
    public bool IsGameOver => isGameOver.Value;
    public bool CanBuyFurniture => IsSpawned && IsNight && !isGameOver.Value && !isTransitioning.Value;

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
    private readonly NetworkVariable<int> rentSurcharge = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>次回の家賃徴収額（基本額＋上乗せ）。</summary>
    public int CurrentRentTotal => rentAmount + rentSurcharge.Value;
    public int DaysUntilRent
    {
        get
        {
            int interval = Mathf.Max(1, maxDay);
            int day = Mathf.Max(1, DebugDay);
            return interval - ((day - 1) % interval);
        }
    }

    /// <summary>夜イベントなどから次回家賃に上乗せする（サーバーのみ）。</summary>
    public void ServerAddRentSurcharge(int amount)
    {
        if (!IsServer || amount <= 0) return;
        rentSurcharge.Value += amount;
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
        180f,
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
        currentDay.OnValueChanged += OnDayChanged;
        currentTime.OnValueChanged += OnTimeChanged;
        isGameOver.OnValueChanged += OnGameOverChanged;
        if (IsServer)
        {
            remainingTime.Value = turnDuration;
            SnapshotDayStartEarnings();
        }
        UpdateDayUI();
        UpdateTimerUI();
        ApplySkyMaterial(currentTime.Value);
        ApplySmugglingTime(currentTime.Value);
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        currentDay.OnValueChanged -= OnDayChanged;
        currentTime.OnValueChanged -= OnTimeChanged;
        isGameOver.OnValueChanged -= OnGameOverChanged;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsSpawned || isGameOver.Value) return;

        UpdateTimerUI();

        if (!IsServer) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (enableDebugDaySkip &&
            Keyboard.current != null &&
            Keyboard.current.nKey.wasPressedThisFrame)
        {
            AdvanceTurnServer();
        }
#endif

        // 朝への切り替え演出中はタイマーを止める
        if (isTransitioning.Value) return;

        remainingTime.Value -= Time.deltaTime;

        if (remainingTime.Value <= 0f)
        {
            AdvanceTurnServer();
        }
    }

    private void AdvanceTurnServer()
    {
        if (!IsServer) return;
        if (isGameOver.Value) return;
        if (isTransitioning.Value) return;

        remainingTime.Value = turnDuration;

        // 朝 → 夜（演出なし）
        if (currentTime.Value == 0)
        {
            currentTime.Value = 1;
            return;
        }

        // 夜 → 新しい日の朝。次の日番号を決めてから演出付きで切り替える。
        // 日数はリセットせず、DAY 4、DAY 5…と増え続ける。
        // 家賃だけを指定日数ごと（初期値は3日）に徴収する。
        int nextDay = currentDay.Value + 1;
        bool collectRent = currentDay.Value % Mathf.Max(1, maxDay) == 0;

        StartCoroutine(NightEndRoutine(nextDay, collectRent));
    }

    /// <summary>
    /// 夜終わりのシーケンス（サーバー進行）。
    /// 1) 今日の収支ランキングを全画面でドン！ドン！表示
    /// 2) 続けて「○日目」の朝演出 → 暗転中に日付更新＆全員リスポーン → 朝再開
    /// </summary>
    private IEnumerator NightEndRoutine(int nextDay, bool collectRent)
    {
        isTransitioning.Value = true;

        // ---- 1) 今日の収支リザルト ----
        string resultData = BuildResultData(out int playerCount);
        ShowDayResultClientRpc(resultData);
        yield return new WaitForSeconds(DayResultUI.EstimatedDuration(playerCount));
        HideDayResultClientRpc();

        // ---- 2) 3日目だけ、全員へ家賃支払いフェーズを表示 ----
        if (collectRent)
        {
            int total = rentAmount + rentSurcharge.Value;
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
                rentSurcharge.Value = 0;
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
                isTransitioning.Value = false;
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

        isTransitioning.Value = false;
    }

    /// <summary>家具をネットワーク同期で生成する（サーバー権限・代金もここで引く）。</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BuyFurnitureServerRpc(
        int catalogIndex,
        RpcParams rpcParams = default)
    {
        ulong requester = rpcParams.Receive.SenderClientId;
        bool success = TryPurchaseFurniture(requester, catalogIndex, out string message);
        FurniturePurchaseResultClientRpc(requester, success, catalogIndex, message);
    }

    // 金額・時間帯・配達先はServerで確定し、生成に成功した注文だけ決済する。
    private bool TryPurchaseFurniture(ulong requester, int catalogIndex, out string message)
    {
        message = "家具ショップは夜の自由行動中だけ利用できます";
        if (!IsServer || !CanBuyFurniture) return false;

        message = "プレイヤーの準備ができていません";
        if (NetworkManager == null ||
            !NetworkManager.ConnectedClients.TryGetValue(requester, out var client) ||
            client.PlayerObject == null || !client.PlayerObject.IsSpawned)
            return false;

        message = "同期用家具Prefabの設定が不正です";
        if (networkFurniturePrefab == null ||
            networkFurniturePrefab.GetComponent<NetworkObject>() == null ||
            networkFurniturePrefab.GetComponent<NetworkFurniture>() == null ||
            !NetworkManager.NetworkConfig.Prefabs.Contains(networkFurniturePrefab))
            return false;

        var item = FurnitureCatalog.Get(catalogIndex);
        message = "商品データが不正です";
        if (item == null || item.cost <= 0 ||
            !float.IsFinite(item.placeholderSize.x) || item.placeholderSize.x <= 0f ||
            !float.IsFinite(item.placeholderSize.y) || item.placeholderSize.y <= 0f ||
            !float.IsFinite(item.placeholderSize.z) || item.placeholderSize.z <= 0f)
            return false;

        // カタログのPrefabは見た目用。同期ルートの子にNetworkObjectを追加しない。
        if (item.prefab != null && item.prefab.GetComponentInChildren<NetworkObject>(true) != null)
            return false;

        var marker = FindAnyObjectByType<FurnitureDeliveryPoint>();
        message = "家具の配達地点が設定されていません";
        if (marker == null) return false;

        // 注文者ごとのローカルカウンターではなく、全注文で一つの配置順を使う。
        float angle = furnitureDeliveryCount * 1.1f;
        float radius = Mathf.Min(0.4f + 0.25f * furnitureDeliveryCount, 1.6f);
        Vector3 ground = marker.transform.position +
                         new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        Vector3 center = ground + Vector3.up * (item.placeholderSize.y * 0.5f);
        message = "家具の配達地点が不正です";
        if (!float.IsFinite(center.x) || !float.IsFinite(center.y) || !float.IsFinite(center.z))
            return false;

        var money = SharedMoneyManager.Instance;
        message = $"残高不足（必要 ¥{item.cost}）";
        if (money == null || !money.CanPay(item.cost)) return false;

        GameObject go = null;
        NetworkObject no = null;
        try
        {
            go = Instantiate(networkFurniturePrefab, center, Quaternion.identity);
            no = go.GetComponent<NetworkObject>();
            no.Spawn(true);
            go.GetComponent<NetworkFurniture>().ServerSetIndex(catalogIndex);
        }
        catch (System.Exception exception)
        {
            RemoveFailedFurniture(go, no);
            Debug.LogWarning($"[Furniture] 家具生成に失敗: {exception.Message}");
            message = "家具を配達できませんでした。代金は引かれていません";
            return false;
        }

        if (!money.TrySpend(
                item.cost,
                SharedMoneyReason.FurniturePurchase,
                $"client={requester}, item={item.id}"))
        {
            RemoveFailedFurniture(go, no);
            return false;
        }

        furnitureDeliveryCount++;
        message = $"{item.displayName}を配達しました";
        return true;
    }

    private static void RemoveFailedFurniture(GameObject go, NetworkObject no)
    {
        if (no != null && no.IsSpawned) no.Despawn(true);
        else if (go != null) Destroy(go);
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
        foreach (var nf in FindObjectsByType<NetworkFurniture>())
        {
            nf.ServerActivateEffect();
        }
    }

    // 各プレイヤーの現在の稼ぎを「その日の開始値」として記録
    private void SnapshotDayStartEarnings()
    {
        dayStartEarning.Clear();
        var players = FindObjectsByType<PlayerEarning>();
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
        var players = FindObjectsByType<PlayerEarning>();

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
        bool isMorning = time == 0;
        Material skyMaterial = time == 0 ? morningSkyMaterial : nightSkyMaterial;

        if (environmentSun != null)
        {
            environmentSun.intensity = isMorning ? morningSunIntensity : nightSunIntensity;
            environmentSun.color = isMorning ? new Color(1f, .94f, .82f) : new Color(.6f, .72f, 1f);
        }

        if (skyMaterial != null)
        {
            RenderSettings.skybox = skyMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity =
                isMorning ? morningAmbientIntensity : nightAmbientIntensity;
            RenderSettings.reflectionIntensity =
                isMorning ? morningReflectionIntensity : nightReflectionIntensity;
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
