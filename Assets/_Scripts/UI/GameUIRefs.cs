using System;
using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム中のUIシーン（GameRoomUI）に置く、UIの受け渡し口。
/// UIはマップ（GameRoom）とは別のシーンにあるので、インスペクターで直接つなげない。
/// マップ側（DayManager など）は <see cref="WhenReady"/> でここからUIを受け取る。
///
/// 参照はインスペクターで割り当てる（配置ツール `Roomies/UI/Split GameRoom UI` が入れる）。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameUIRefs : MonoBehaviour
{
    public static GameUIRefs Instance { get; private set; }

    /// <summary>UIシーンが読み込まれたとき（Awake）に呼ばれる。</summary>
    private static event Action<GameUIRefs> Ready;

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI sharedMoneyText;

    [Header("朝・収支・家賃・夜")]
    [SerializeField] private DayTransitionUI transitionUI;
    [SerializeField] private DayResultUI resultUI;
    [SerializeField] private RentPaymentUI rentPaymentUI;
    [SerializeField] private NightEventUI nightEventUI;

    public TextMeshProUGUI DayText => dayText;
    public TextMeshProUGUI TimerText => timerText;
    public TextMeshProUGUI SharedMoneyText => sharedMoneyText;
    public DayTransitionUI TransitionUI => transitionUI;
    public DayResultUI ResultUI => resultUI;
    public RentPaymentUI RentPaymentUI => rentPaymentUI;
    public NightEventUI NightEventUI => nightEventUI;

    /// <summary>
    /// UIが使えるようになったら callback を呼ぶ。すでに読み込み済みならすぐ呼ぶ。
    /// UIシーンが読み直されたときもまた呼ばれる。やめるときは <see cref="Unsubscribe"/>。
    /// </summary>
    public static void WhenReady(Action<GameUIRefs> callback)
    {
        if (callback == null) return;
        Ready -= callback;
        Ready += callback;
        if (Instance != null) callback(Instance);
    }

    public static void Unsubscribe(Action<GameUIRefs> callback)
    {
        Ready -= callback;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        Ready = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[UI] ゲーム中のUIシーンが2つ読み込まれています。", this);
            return;
        }

        Instance = this;

        if (dayText == null || timerText == null || transitionUI == null ||
            resultUI == null || rentPaymentUI == null || nightEventUI == null)
        {
            Debug.LogError(
                "[UI] ゲーム中のUIの参照が空です。" +
                "メニュー『Roomies/UI/Split GameRoom UI』で置き直してください。", this);
        }

        Ready?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
