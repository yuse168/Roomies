using UnityEngine;

/// <summary>
/// 部屋の PC（2026/11/25）。見て E を押すと、その人の画面だけで PC のデスクトップ（<see cref="RoomComputerUI"/>）が開く。
/// 開いている間は、その人の移動・操作だけが止まる（ほかの人には関係ない。2 人で同時に使ってもよい）。
///
/// プレハブ：Assets/_Prefabs/Room/RoomComputer.prefab（Roomies/PC/1 Build Prefab）。机とモニターの見た目は部屋の背景（Roomies_RoomRestyle の PC desk）。
///  ・ルート：NetworkObject・このスクリプト・見て E を押す当たり判定（モニターとキーボードのまわり）
///  ・Anchor_Screen：モニターの画面の真ん中（開く動きの基準）
///  ・Visual：空（見た目は背景の机。差し替えるときはここへ）
/// </summary>
public class RoomComputer : HomeAppliance
{
    [Tooltip("モニターの画面の真ん中（Anchor_Screen）")]
    [SerializeField] private Transform anchorScreen;
    [SerializeField] private string label = "パソコンを使う";

    public Transform AnchorScreen => anchorScreen;

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
