using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 夜イベント＆ランダムトラブルの管理。
/// シーンに配置不要：DayManagerがランタイムで生成する。
/// ネットワーク送信はDayManagerのRPC中継（ServerSend～）を使うので
/// このクラス自体はNetworkBehaviourではない。
///
/// 夜になった瞬間にサーバーが抽選し、全クライアントへ演出を同期する：
///  ・平和な夜   … 何も起きない
///  ・電気代請求 … 共同口座からランダム額を徴収
///  ・食費請求   … 各プレイヤーの個人所持金からランダム額を徴収
///  ・停電       … 朝まで照明が消えて暗くなる（見た目のみ）
///  ・地震       … 部屋の荷物が吹っ飛ぶ＋画面が揺れる
///  ・家賃値上げ … 次回の家賃徴収額に上乗せ
///
/// 他スクリプトから使える告知API（ServerAnnounce / ServerAnnounceTo）も持つ。
/// </summary>
public class NightEventManager : MonoBehaviour
{
    public static NightEventManager Instance { get; private set; }

    private enum NightEventType : byte
    {
        Peace = 0,      // 平和な夜
        UtilityBill,    // 電気代請求
        FoodBill,       // 食費請求
        Blackout,       // 停電
        Earthquake,     // 地震
        RentHike,       // 家賃値上げ
    }

    // バナー色の種類（RPCで送るためbyte化）
    public const byte StyleInfo   = 0;
    public const byte StyleDanger = 1;
    public const byte StyleFun    = 2;
    public const byte StylePeace  = 3;

    [Header("UI")]
    [SerializeField] private NightEventUI eventUI;

    [Header("金額設定")]
    [SerializeField] private int utilityBillMin = 100;
    [SerializeField] private int utilityBillMax = 300;
    [SerializeField] private int foodBillMin = 50;
    [SerializeField] private int foodBillMax = 150;
    [SerializeField] private int rentHikeMin = 100;
    [SerializeField] private int rentHikeMax = 300;

    // 停電の復旧用（ローカル）
    private readonly List<Light> disabledLights = new List<Light>();
    private float prevAmbientIntensity = 1f;
    private Color prevAmbientLight;
    private bool blackoutActive;

    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 告知UIが未設定ならランタイムで生成する（シーン配置不要）
        if (eventUI == null)
        {
            var go = new GameObject("NightEventUI");
            eventUI = go.AddComponent<NightEventUI>();
        }

        DayManager.OnNightArrived += OnNight;
        DayManager.OnMorningArrived += OnMorning;
    }

    private void OnDestroy()
    {
        DayManager.OnNightArrived -= OnNight;
        DayManager.OnMorningArrived -= OnMorning;
        if (Instance == this) Instance = null;
    }

    private static bool IsServer
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }
    }

    // ---- 夜イベント抽選（サーバー） ----

    private void OnNight()
    {
        if (!IsServer) return;

        var dm = DayManager.Instance;
        if (dm == null) return;

        var type = PickRandomEvent();

        switch (type)
        {
            case NightEventType.Peace:
                dm.ServerSendNightEvent((byte)type, 0);
                break;

            case NightEventType.UtilityBill:
            {
                int amount = RandomAmount(utilityBillMin, utilityBillMax);
                var money = SharedMoneyManager.Instance;
                if (money != null)
                {
                    amount = money.SpendUpTo(
                        amount,
                        SharedMoneyReason.UtilityBill,
                        "Night event");
                }
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.FoodBill:
            {
                int amount = RandomAmount(foodBillMin, foodBillMax);
                foreach (var pe in FindObjectsByType<PlayerEarning>())
                {
                    pe.SpendEarning(Mathf.Min(amount, Mathf.Max(0, pe.GetEarning())));
                }
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.Blackout:
                dm.ServerSendNightEvent((byte)type, 0);
                break;

            case NightEventType.Earthquake:
                StartCoroutine(EarthquakeRoutine());
                dm.ServerSendNightEvent((byte)type, 0);
                break;

            case NightEventType.RentHike:
            {
                int amount = RandomAmount(rentHikeMin, rentHikeMax);
                dm.ServerAddRentSurcharge(amount);
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }
        }
    }

    private NightEventType PickRandomEvent()
    {
        // (イベント, 重み) の抽選テーブル
        (NightEventType type, int weight)[] table =
        {
            (NightEventType.Peace,       25),
            (NightEventType.UtilityBill, 20),
            (NightEventType.FoodBill,    20),
            (NightEventType.Blackout,    12),
            (NightEventType.Earthquake,  13),
            (NightEventType.RentHike,    10),
        };

        int total = 0;
        foreach (var e in table) total += e.weight;

        int roll = Random.Range(0, total);
        foreach (var e in table)
        {
            if (roll < e.weight) return e.type;
            roll -= e.weight;
        }
        return NightEventType.Peace;
    }

    private static int RandomAmount(int min, int max)
    {
        // 10円単位のランダム額
        return Random.Range(min / 10, max / 10 + 1) * 10;
    }

    // ---- 全クライアント演出（DayManagerのRPC中継から呼ばれる） ----

    /// <summary>夜イベントの見た目・バナーを再生する（各クライアントローカル）。</summary>
    public void PlayEventVisual(byte typeByte, int value)
    {
        var type = (NightEventType)typeByte;

        switch (type)
        {
            case NightEventType.Peace:
                ShowBanner("しずかな夜", "今夜は何も起きなさそうだ……", StylePeace);
                break;

            case NightEventType.UtilityBill:
                ShowBanner("電気代の請求！", $"共同口座から ¥{value:N0} 引き落とされた！", StyleDanger);
                break;

            case NightEventType.FoodBill:
                ShowBanner("食費の請求！", $"ひとり ¥{value:N0} ずつ支払った……腹は減る。", StyleDanger);
                break;

            case NightEventType.Blackout:
                ShowBanner("停電発生！", "ブレーカーが落ちた！朝まで真っ暗……", StyleDanger);
                ApplyBlackout();
                break;

            case NightEventType.Earthquake:
                ShowBanner("地震だー！！", "部屋のものが吹っ飛んだ！", StyleDanger);
                break;

            case NightEventType.RentHike:
                ShowBanner("家賃値上げのお知らせ", $"大家さん「次の家賃、¥{value:N0} 上乗せね」", StyleDanger);
                break;
        }
    }

    private void OnMorning()
    {
        // 停電の復旧（各クライアントがローカルで戻す）
        RestoreBlackout();
    }

    // ---- 停電（ローカル演出） ----

    private void ApplyBlackout()
    {
        if (blackoutActive) return;
        blackoutActive = true;

        prevAmbientIntensity = RenderSettings.ambientIntensity;
        prevAmbientLight = RenderSettings.ambientLight;

        disabledLights.Clear();
        foreach (var light in FindObjectsByType<Light>())
        {
            if (light != null && light.enabled)
            {
                light.enabled = false;
                disabledLights.Add(light);
            }
        }

        RenderSettings.ambientIntensity = 0.15f;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.10f);
    }

    private void RestoreBlackout()
    {
        if (!blackoutActive) return;
        blackoutActive = false;

        foreach (var light in disabledLights)
        {
            if (light != null) light.enabled = true;
        }
        disabledLights.Clear();

        RenderSettings.ambientIntensity = prevAmbientIntensity;
        RenderSettings.ambientLight = prevAmbientLight;
    }

    // ---- 地震（サーバー物理＋全員の画面揺れ） ----

    private IEnumerator EarthquakeRoutine()
    {
        const int pulses = 3;

        for (int i = 0; i < pulses; i++)
        {
            // 部屋の持てる物を吹っ飛ばす（持たれている物＝kinematicは除外）
            foreach (var obj in FindObjectsByType<CarryableObject>())
            {
                if (obj == null || obj.rb == null || obj.rb.isKinematic) continue;

                Vector3 dir = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.6f, 1f),
                    Random.Range(-1f, 1f));

                obj.rb.AddForce(dir.normalized * Random.Range(2f, 5f), ForceMode.Impulse);
                obj.rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);
            }

            var dm = DayManager.Instance;
            if (dm != null) dm.ServerSendCameraShake(0.8f, 0.25f);

            yield return new WaitForSeconds(1.2f);
        }
    }

    /// <summary>画面揺れ（各クライアントローカル）。DayManagerのRPC中継から呼ばれる。</summary>
    public void ShakeCamera(float duration, float magnitude)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        var cam = Camera.main;
        if (cam == null) yield break;

        Transform t = cam.transform;
        Vector3 basePos = t.localPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - elapsed / duration;
            t.localPosition = basePos + Random.insideUnitSphere * magnitude * damper;
            yield return null;
        }

        t.localPosition = basePos;
        shakeRoutine = null;
    }

    // ---- 他スクリプト向けの告知API（サーバーから呼ぶ） ----

    /// <summary>全員にバナー告知（サーバーのみ有効）。</summary>
    public void ServerAnnounce(string title, string body, byte style = StyleFun)
    {
        if (!IsServer) return;
        var dm = DayManager.Instance;
        if (dm != null) dm.ServerSendAnnounce(title, body, style);
    }

    /// <summary>特定プレイヤーだけにバナー告知（サーバーのみ有効）。</summary>
    public void ServerAnnounceTo(ulong clientId, string title, string body, byte style = StyleInfo)
    {
        if (!IsServer) return;
        var dm = DayManager.Instance;
        if (dm != null) dm.ServerSendAnnounceTo(clientId, title, body, style);
    }

    /// <summary>バナー表示（ローカル）。色・アイコンはNightEventUI側がstyleから決める。</summary>
    public void ShowBanner(string title, string body, byte style)
    {
        if (eventUI == null) return;

        eventUI.Show(title, body, style);
    }
}
