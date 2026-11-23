#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// ブラックジャック台の上の実物（カード・チップ・点数の札）を、プレハブに置く（Roomies/Casino/2 Build Gimmick Prefabs）。
///
/// ルートの直下に置く物（どれも Visual の外。BlackjackTableView が動かす）：
///   Pivot_Card_P1〜8 / Pivot_Card_D1〜8 … カードの軸（中の Visual が表と裏の板）。ふだんは隠してある
///   Anchor_Shoe（カードが出てくる靴の口）・Anchor_Discard（片付ける箱）・Anchor_PlayerHand / Anchor_DealerHand（1 枚目を置く所）
///   Pivot_Chips_Bet / Double / Payout … チップの山（中の Visual にチップ）。Anchor_Bet・Anchor_Payout・Anchor_Tray
///   Anchor_Score_Player / Anchor_Score_Dealer … 点数の札（斜めに立てた小さな板と文字）
/// 位置は、作ったあとプレハブの中で直してよい（配置ツールを実行し直すと、ここの値に戻る）。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    const int BjCardSlots = 8;

    static void BuildBlackjackCards(GameObject root, BlackjackTable table)
    {
        // 前に置いた物を消す（何度実行しても同じ結果にする）
        foreach (Transform c in root.transform.Cast<Transform>().ToArray())
            if (c.name.StartsWith("Pivot_Card_") || c.name.StartsWith("Pivot_Chips_") || c.name.StartsWith("Anchor_Score_")
                || c.name is "Anchor_Shoe" or "Anchor_Discard" or "Anchor_PlayerHand" or "Anchor_DealerHand" or "Anchor_Bet" or "Anchor_Payout" or "Anchor_Tray")
                Object.DestroyImmediate(c.gameObject);

        float top = Art.BjTop + .0105f;       // フェルトの絵のすぐ上
        Material cards = Art.CardsMaterial();
        Mesh face = Art.CardFace(), back = Art.CardBack();

        // ---- 位置
        // 遊ぶ人は +Z の側から -Z を見る。手の位置は半回転しておく（札の上の辺が奥を向き、遊ぶ人から正しく読める。+X が遊ぶ人の右）
        Transform shoe = Child(root.transform, "Anchor_Shoe", new Vector3(.85f, Art.BjTop + .07f, -.47f));
        Transform discard = Child(root.transform, "Anchor_Discard", new Vector3(-.87f, Art.BjTop + .09f, -.68f));
        Transform playerHand = Child(root.transform, "Anchor_PlayerHand", new Vector3(.25f, top, .34f));
        Transform dealerHand = Child(root.transform, "Anchor_DealerHand", new Vector3(.25f, top, -.2f));
        playerHand.localRotation = dealerHand.localRotation = Quaternion.Euler(0, 180, 0);
        Transform tray = Child(root.transform, "Anchor_Tray", new Vector3(0, Art.BjTop + .02f, -.52f));
        Transform bet = Child(root.transform, "Anchor_Bet", new Vector3(0, top, .72f));
        Transform payout = Child(root.transform, "Anchor_Payout", new Vector3(-.14f, top, .66f));

        // ---- カード
        var view = root.GetComponent<BlackjackTableView>();
        if (view == null) view = root.AddComponent<BlackjackTableView>();
        var so = new SerializedObject(view);
        foreach (var (prefix, hand, prop) in new[] { ("P", playerHand, "playerCards"), ("D", dealerHand, "dealerCards") })
        {
            var array = so.FindProperty(prop);
            array.arraySize = BjCardSlots;
            for (int i = 0; i < BjCardSlots; i++)
            {
                Transform pivot = Child(root.transform, $"Pivot_Card_{prefix}{i + 1}", hand.localPosition + hand.localRotation * (new Vector3(.112f, .0012f, -.014f) * i));
                pivot.localRotation = hand.localRotation;
                Transform visual = Child(pivot, "Visual");
                var faceRenderer = CardPart(visual, "Face", face, cards);
                CardPart(visual, "Back", back, cards);
                pivot.gameObject.SetActive(false);
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("pivot").objectReferenceValue = pivot;
                element.FindPropertyRelative("face").objectReferenceValue = faceRenderer;
            }
        }

        // ---- チップの山（賭け金・ダブルダウン・払い戻し）
        string[] colors = { "chip_red", "chip_blue", "chip_green", "chip_black", "chip_red", "chip_blue", "chip_green", "chip_black" };
        GameObject[] Stack(string name, Vector3 position, int count, out Transform pivot)
        {
            pivot = Child(root.transform, name, position);
            Transform visual = Child(pivot, "Visual");
            var chips = new GameObject[count];
            for (int i = 0; i < count; i++)
                chips[i] = Art.Place(visual, "Chip_" + (i + 1), Art.BjChip(colors[i % colors.Length]), new Vector3(0, i * Art.ChipThickness, 0), i * 23f, false, false);
            pivot.gameObject.SetActive(false);
            return chips;
        }
        var betChips = Stack("Pivot_Chips_Bet", bet.localPosition, 3, out Transform pivotBet);
        var doubleChips = Stack("Pivot_Chips_Double", bet.localPosition + new Vector3(.11f, 0, 0), 3, out Transform pivotDouble);
        var payoutChips = Stack("Pivot_Chips_Payout", payout.localPosition, 8, out Transform pivotPayout);

        // ---- 点数の札（カードの右どなり＝遊ぶ人から見て左。斜めに立てて、遊ぶ人から読める向きに）
        TMP_Text Score(string name, Vector3 position, out GameObject plate)
        {
            Transform anchor = Child(root.transform, name, position);
            anchor.localRotation = Quaternion.Euler(-52, 0, 0);
            Art.Place(Child(anchor, "Visual"), "Plate", Art.ScorePlate(), default, 0, false, false);
            var text = Label(anchor, "Text_Score", "21", new Vector3(0, 0, .002f), .62f, new Color(1f, .93f, .75f));
            ((RectTransform)text.transform).sizeDelta = new Vector2(.15f, .07f);
            text.fontStyle = FontStyles.Bold;
            plate = anchor.gameObject;
            plate.SetActive(false);
            return text;
        }
        TMP_Text playerScore = Score("Anchor_Score_Player", new Vector3(.47f, Art.BjTop + .05f, .38f), out GameObject playerPlate);
        TMP_Text dealerScore = Score("Anchor_Score_Dealer", new Vector3(.47f, Art.BjTop + .05f, -.16f), out GameObject dealerPlate);

        // ---- 参照の割り当て
        so.FindProperty("anchorShoe").objectReferenceValue = shoe;
        so.FindProperty("anchorDiscard").objectReferenceValue = discard;
        so.FindProperty("anchorPlayerHand").objectReferenceValue = playerHand;
        so.FindProperty("anchorDealerHand").objectReferenceValue = dealerHand;
        so.FindProperty("cardStep").vector3Value = new Vector3(.112f, .0012f, -.014f);
        so.FindProperty("atlasColumns").intValue = Art.CardColumns;
        so.FindProperty("atlasRows").intValue = Art.CardRows;
        so.FindProperty("cellU").floatValue = Art.CardCellU;
        so.FindProperty("pivotBet").objectReferenceValue = pivotBet;
        SetObjects(so.FindProperty("betChips"), betChips);
        so.FindProperty("pivotDouble").objectReferenceValue = pivotDouble;
        SetObjects(so.FindProperty("doubleChips"), doubleChips);
        so.FindProperty("pivotPayout").objectReferenceValue = pivotPayout;
        SetObjects(so.FindProperty("payoutChips"), payoutChips);
        so.FindProperty("anchorTray").objectReferenceValue = tray;
        so.FindProperty("anchorBet").objectReferenceValue = bet;
        so.FindProperty("anchorPayout").objectReferenceValue = payout;
        so.FindProperty("playerScoreText").objectReferenceValue = playerScore;
        so.FindProperty("dealerScoreText").objectReferenceValue = dealerScore;
        so.FindProperty("playerScorePlate").objectReferenceValue = playerPlate;
        so.FindProperty("dealerScorePlate").objectReferenceValue = dealerPlate;
        so.ApplyModifiedPropertiesWithoutUndo();

        var ts = new SerializedObject(table);
        ts.FindProperty("view").objectReferenceValue = view;
        ts.ApplyModifiedPropertiesWithoutUndo();
    }

    static MeshRenderer CardPart(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return r;
    }
}
#endif
