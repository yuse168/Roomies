using UnityEngine;

/// <summary>
/// ルーレットの台の上の、賭ける場所 1 つ（当たり判定つき）。プレハブに置いてある。
/// 通信はしない。どの場所を見ているかを台（RouletteTable）が知るための札。
/// チップの見た目は Anchor_Chips の下に置いてあり、台が出し入れと高さの反映だけを行う。
/// </summary>
public class RouletteSpot : MonoBehaviour
{
    public RouletteBetKind kind = RouletteBetKind.Number;

    [Tooltip("数字に賭ける場所のときの数字（0〜36）")]
    [Range(0, 36)] public int number;

    [Tooltip("置かれたチップの見た目（Anchor_Chips）。賭けがあるときだけ出す")]
    public Transform chips;
}
