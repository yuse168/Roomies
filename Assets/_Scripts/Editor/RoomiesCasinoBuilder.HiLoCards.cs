#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// ハイ＆ローの台の上の実物（カード・チップ）を、プレハブに置く（Roomies/Casino/2 Build Gimmick Prefabs）。
/// ブラックジャック（RoomiesCasinoBuilder.BlackjackCards.cs）と同じ作り。トランプの絵・マテリアル・チップの形も同じ物を使う。
///
/// ルートの直下に置く物（どれも Visual の外。HiLoTableView が動かす）：
///   Pivot_Card_1〜6 … カードの軸（中の Visual が表と裏の板）。ふだんは隠してある。1 枚目から順に、となりへ並ぶ
///   Anchor_Deck（山の上＝カードが出てくる所）・Anchor_Discard（片付ける所）・Anchor_Row（1 枚目を置く所）
///   Pivot_Chips_Bet / Payout … チップの山（中の Visual にチップ）。Anchor_Bet・Anchor_Payout・Anchor_Tray（親のチップ入れ）
/// 位置は、作ったあとプレハブの中で直してよい（配置ツールを実行し直すと、ここの値に戻る）。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    /// <summary>置いておくカードの枚数：1 枚目 ＋ 続けられる回数（5）。これを超えたら、最後の 1 枚の上に重ねる。</summary>
    const int HiLoCardSlots = 6;
    /// <summary>ブラックジャックと同じカードを、少し大きく見せる（台が広く、カードが主役なので）。</summary>
    const float HiLoCardScale = Art.HlCardScale;
    static readonly Vector3 HiLoCardStep = new Vector3(.175f, .0012f, 0f);

    static HiLoTableView BuildHiLoCards(GameObject root)
    {
        float top = Art.HlTop + .002f;       // フェルトのすぐ上
        Material cards = Art.CardsMaterial();
        Mesh face = Art.CardFace(), back = Art.CardBack();

        // ---- 位置
        // 遊ぶ人は +Z の側から -Z を見る。並べる向きは半回転しておく（札の上の辺が奥を向き、遊ぶ人から正しく読める。
        // 1 枚目は遊ぶ人から見て左＝山のとなり。そこから右へ並ぶ）
        Transform deck = Child(root.transform, "Anchor_Deck", Art.HlDeck);
        Transform discard = Child(root.transform, "Anchor_Discard", Art.HlDeck + new Vector3(0, .01f, 0));
        Transform row = Child(root.transform, "Anchor_Row", new Vector3(.35f, top, Art.HlRowZ));
        row.localRotation = Quaternion.Euler(0, 180, 0);
        Transform tray = Child(root.transform, "Anchor_Tray", Art.HlTray);
        Transform bet = Child(root.transform, "Anchor_Bet", new Vector3(0, top, .36f));
        Transform payout = Child(root.transform, "Anchor_Payout", new Vector3(-.16f, top, .36f));

        // ---- 山のいちばん上：裏面の札（見た目だけ。台の形の「札の束」の上にのせる）
        Transform deckTop = Child(root.transform.Find("Visual"), "DeckTop", new Vector3(Art.HlDeck.x, Art.HlTop + Art.HlDeckHeight + .0006f, Art.HlDeck.z));
        deckTop.localRotation = Quaternion.Euler(0, 180, 0);
        deckTop.localScale = Vector3.one * HiLoCardScale;
        CardPart(deckTop, "Back", Art.CardBackTop(), cards);

        // ---- カード
        var view = root.AddComponent<HiLoTableView>();
        var so = new SerializedObject(view);
        var array = so.FindProperty("cards");
        array.arraySize = HiLoCardSlots;
        for (int i = 0; i < HiLoCardSlots; i++)
        {
            Transform pivot = Child(root.transform, $"Pivot_Card_{i + 1}", row.localPosition + row.localRotation * (HiLoCardStep * i));
            pivot.localRotation = row.localRotation;
            Transform visual = Child(pivot, "Visual");
            visual.localScale = Vector3.one * HiLoCardScale;
            var faceRenderer = CardPart(visual, "Face", face, cards);
            CardPart(visual, "Back", back, cards);
            pivot.gameObject.SetActive(false);
            var element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("pivot").objectReferenceValue = pivot;
            element.FindPropertyRelative("face").objectReferenceValue = faceRenderer;
        }

        // ---- チップの山（賭け金・払い戻し）
        string[] colors = { "chip_red", "chip_blue", "chip_green", "chip_black", "chip_red", "chip_blue", "chip_green", "chip_black" };
        GameObject[] Stack(string name, Vector3 position, int count, out Transform pivot)
        {
            pivot = Child(root.transform, name, position);
            Transform visual = Child(pivot, "Visual");
            var chips = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                chips[i] = Art.Place(visual, "Chip_" + (i + 1), Art.BjChip(colors[i % colors.Length]), new Vector3(0, i * Art.ChipThickness, 0), i * 23f, false, false);
                CardPart(chips[i].transform, "Top", Art.ChipTop(colors[i % colors.Length]), cards);     // 上の面の模様（トランプと同じマテリアル）
            }
            pivot.gameObject.SetActive(false);
            return chips;
        }
        var betChips = Stack("Pivot_Chips_Bet", bet.localPosition, 3, out Transform pivotBet);
        var payoutChips = Stack("Pivot_Chips_Payout", payout.localPosition, 8, out Transform pivotPayout);

        // ---- 参照の割り当て
        so.FindProperty("anchorDeck").objectReferenceValue = deck;
        so.FindProperty("anchorDiscard").objectReferenceValue = discard;
        so.FindProperty("anchorRow").objectReferenceValue = row;
        so.FindProperty("cardStep").vector3Value = HiLoCardStep;
        so.FindProperty("atlasColumns").intValue = Art.CardColumns;
        so.FindProperty("cellU").floatValue = Art.CardCellU;
        so.FindProperty("cellV").floatValue = Art.CardCellV;
        so.FindProperty("pivotBet").objectReferenceValue = pivotBet;
        SetObjects(so.FindProperty("betChips"), betChips);
        so.FindProperty("pivotPayout").objectReferenceValue = pivotPayout;
        SetObjects(so.FindProperty("payoutChips"), payoutChips);
        so.FindProperty("anchorTray").objectReferenceValue = tray;
        so.FindProperty("anchorBet").objectReferenceValue = bet;
        so.FindProperty("anchorPayout").objectReferenceValue = payout;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }
}
#endif
