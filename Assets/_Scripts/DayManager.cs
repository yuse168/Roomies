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

    // 診断用
    public int  DebugDay     => currentDay  != null ? currentDay.Value  : -1;
    public int  DebugTime    => currentTime != null ? currentTime.Value : -1;
    public bool DebugSpawned => NetworkObject != null && NetworkObject.IsSpawned;

    /// <summary>夜→朝に切り替わった瞬間に発火（全クライアント）。家具の配達などに使う。</summary>
    public static event System.Action OnMorningArrived;

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

    private void Start()
    {
        Instance = this;

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

        // 家具編集コントローラがシーンに無ければ生成する（仮カタログで動作）。
        // 見た目を差し替えたい場合は GameRoom に FurnitureEditController を手動で置き、
        // catalog に Prefab を割り当てること（手動配置があればそちらが使われる）。
        if (FindAnyObjectByType<FurnitureEditController>() == null)
        {
            var fgo = new GameObject("FurnitureEditController");
            fgo.AddComponent<FurnitureEditController>();
        }

        UpdateDayUI();
        UpdateTimerUI();
        ApplySkyMaterial(currentTime.Value);
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
        if (currentDay.Value < maxDay)
        {
            nextDay = currentDay.Value + 1;
        }
        else
        {
            bool rentPaid = ChargeRent();
            if (!rentPaid)
            {
                GameOver();
                return;
            }
            nextDay = 1;
        }

        StartCoroutine(NightEndRoutine(nextDay));
    }

    /// <summary>
    /// 夜終わりのシーケンス（サーバー進行）。
    /// 1) 今日の収支ランキングを全画面でドン！ドン！表示
    /// 2) 続けて「○日目」の朝演出 → 暗転中に日付更新＆全員リスポーン → 朝再開
    /// </summary>
    private IEnumerator NightEndRoutine(int nextDay)
    {
        isTransitioning = true;

        // ---- 1) 今日の収支リザルト ----
        string resultData = BuildResultData(out int playerCount);
        ShowDayResultClientRpc(resultData);
        yield return new WaitForSeconds(DayResultUI.EstimatedDuration(playerCount));
        HideDayResultClientRpc();

        // ---- 2) 翌朝の演出 ----
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
    public void BuyFurnitureServerRpc(int catalogIndex, Vector3 ground, float yaw)
    {
        if (networkFurniturePrefab == null) return;

        var item = FurnitureCatalog.Get(catalogIndex);
        if (item == null) return;

        var money = SharedMoneyManager.Instance;
        if (money == null) return;
        if (money.CurrentMoney < item.cost) return; // 残高不足

        money.SpendSharedMoney(item.cost);

        // 仮ブロックの中心 = 地面 + 高さ/2（底が地面に乗るように）
        Vector3 center = new Vector3(ground.x, ground.y + item.placeholderSize.y * 0.5f, ground.z);

        var go = Instantiate(networkFurniturePrefab, center, Quaternion.Euler(0f, yaw, 0f));
        var no = go.GetComponent<NetworkObject>();
        no.Spawn(true);

        var nf = go.GetComponent<NetworkFurniture>();
        if (nf != null) nf.ServerSetIndex(catalogIndex);
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

    private bool ChargeRent()
    {
        if (SharedMoneyManager.Instance == null)
        {
            Debug.Log("SharedMoneyManagerがありません");
            return false;
        }

        if (SharedMoneyManager.Instance.CanPay(rentAmount))
        {
            SharedMoneyManager.Instance.PayRent(rentAmount);

            Debug.Log("家賃支払い成功");

            return true;
        }

        Debug.Log("共同口座のお金が足りない！");

        return false;
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

        // 夜(1)→朝(0) になったら配達などのトリガーを発火
        if (newTime == 0)
        {
            OnMorningArrived?.Invoke();
        }
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