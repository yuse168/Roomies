using TMPro;
using UnityEngine;

/// <summary>
/// 部屋の PC（2026/11/25）。見て E を押すと、その人の画面だけで PC のデスクトップ（<see cref="RoomComputerUI"/>）が開く。
/// 開いている間は、その人の移動・操作だけが止まる（ほかの人には関係ない。2 人で同時に使ってもよい）。
///
/// プレハブ：Assets/_Prefabs/Room/RoomComputer.prefab（Roomies/PC/1 Build Prefab）。机とモニターの見た目は部屋の背景（Roomies_RoomRestyle の PC desk）。
///  ・ルート：NetworkObject・このスクリプト・見て E を押す当たり判定（モニターとキーボードのまわり）
///  ・Anchor_Screen：モニターの画面の真ん中。中の Clock・Day は、待ち受けの上に出す時計（このスクリプトが書き換える）
///  ・Visual/Screen：モニターに映す待ち受けの絵（Codex の monitor_home。机とモニターの本体は部屋の背景）
/// </summary>
public class RoomComputer : HomeAppliance
{
    [Tooltip("モニターの画面の真ん中（Anchor_Screen）")]
    [SerializeField] private Transform anchorScreen;
    [SerializeField] private string label = "パソコンを使う";
    [Header("待ち受けの時計（その人の画面で、ゲームの残り時間を出す）")]
    [SerializeField] private TMP_Text screenClock;
    [SerializeField] private TMP_Text screenDay;
    private float clockTimer;

    public Transform AnchorScreen => anchorScreen;

    private void Update()
    {
        clockTimer -= Time.unscaledDeltaTime;
        if (clockTimer > 0f) return;
        clockTimer = 0.5f;
        var dm = DayManager.Instance;
        if (dm == null) return;
        int sec = Mathf.Max(0, Mathf.CeilToInt(dm.RemainingSeconds));
        if (screenClock != null) screenClock.text = $"{sec / 60:00}:{sec % 60:00}";
        if (screenDay != null) screenDay.text = $"DAY {dm.CurrentDay} {(dm.IsNight ? "夜" : "朝")}";
    }

    public override bool CanInteract()
    {
        if (RoomComputerUI.IsOpen) return false;
        var dm = DayManager.Instance;
        return dm == null || !dm.IsGameOver;
    }

    public override string Label => label;

    public override void Interact()
    {
        if (!CanInteract()) return;
        var ui = GameUIRefs.Instance != null ? GameUIRefs.Instance.ComputerUI : null;
        if (ui == null)
        {
            Debug.LogWarning("[PC] デスクトップの画面（RoomComputerUI）が GameRoomUI にありません。Roomies/PC/3 Build Desktop UI を実行してください。");
            return;
        }
        ui.Open(this);
    }
}
