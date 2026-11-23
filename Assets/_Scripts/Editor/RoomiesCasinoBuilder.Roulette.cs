#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// ルーレットの台のプレハブを作る（Roomies/Casino/2 Build Gimmick Prefabs）。
///
///  RouletteTable.prefab（2 階。賭け金 100／500／1000）と、その Variant の RouletteTable_3F.prefab（500／2000／5000、0 で 2 倍の賭けを返金）。
///   ・ルート：NetworkObject・RouletteTable・台の当たり判定。原点は足元、遊ぶ人の立つ側が +Z。
///   ・Visual：台（1 つのメッシュ。賭ける場所の絵も貼ってある）
///   ・Pivot_Wheel：回る盤（中の Visual が見た目。Anchor_Ball が玉）
///   ・Spots/Spot_0〜36・Spot_Red など：賭ける場所（当たり判定・RouletteSpot。Anchor_Chips がチップ）
///   ・Anchor_Board：結果・秒読み・これまでの数字
///
/// 賭ける場所の位置は、台に貼った絵（ArtSource/Casino/roulette_betting_albedo.png）のマスの位置から決める（下の px の表）。
/// 遊ぶ人から見て：左に盤、その右に 0、数字（手前の段が 1・4・7…）、右端に 2:1（列）。手前に「1–12／13–24／25–36」、いちばん手前に「1–18・EVEN・RED・BLACK・ODD・19–36」。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    internal const string RoulettePath = CasinoFolder + "/RouletteTable.prefab";
    internal const string Roulette3FPath = CasinoFolder + "/RouletteTable_3F.prefab";

    static readonly int[] Roulette2FBets = { 100, 500, 1000 };
    static readonly int[] Roulette3FBets = { 500, 2000, 5000 };
    const int Roulette2FMax = 5000, Roulette3FMax = 25000;

    // 絵の中のマスの位置（元の絵の px。左上が原点）
    const float PxZeroX0 = 55, PxGridX0 = 120, PxCol = 65, PxGridX1 = 900, PxColumnX1 = 965;
    const float PxGridY0 = 135, PxRow = 150, PxGridY1 = 585, PxDozenY1 = 685, PxEvenY1 = 805;

    static void BuildRoulettePrefabs()
    {
        var root = new GameObject("RouletteTable");
        try
        {
            root.AddComponent<NetworkObject>();
            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(0, Art.RlTop / 2f, 0); body.size = new Vector3(Art.RlLength, Art.RlTop, Art.RlDepth);
            var table = root.AddComponent<RouletteTable>();

            Art.Place(Child(root.transform, "Visual"), "Table", Art.RouletteTable());

            // 盤と玉
            var wheel = Child(root.transform, "Pivot_Wheel", Art.RlWheelCenter);
            Art.Place(Child(wheel, "Visual"), "Wheel", Art.RouletteWheel());
            var ball = Child(wheel, "Anchor_Ball", new Vector3(0, .025f, .27f));
            Art.Place(Child(ball, "Visual"), "Ball", Art.RouletteBall(), default, 0, false, false);

            // 賭ける場所（置いた順が番号）：0、1〜36、赤・黒・奇数・偶数・前半・後半、3 つ分け、列
            var spotsRoot = Child(root.transform, "Spots", new Vector3(0, Art.RlTop, 0));
            var spots = new List<RouletteSpot>();
            spots.Add(Spot(spotsRoot, RouletteBetKind.Number, 0, PxZeroX0, PxGridY0, PxGridX0, PxGridY1));
            for (int n = 1; n <= 36; n++)
            {
                int c = (n - 1) / 3, r = (n - 1) % 3;   // r = 0 がいちばん手前の段（絵では下の段）
                float x0 = PxGridX0 + c * PxCol, y1 = PxGridY1 - r * PxRow;
                spots.Add(Spot(spotsRoot, RouletteBetKind.Number, n, x0, y1 - PxRow, x0 + PxCol, y1));
            }
            // いちばん手前の段：絵の並びは 1–18・EVEN・RED・BLACK・ODD・19–36
            var evenOrder = new[] { RouletteBetKind.Low, RouletteBetKind.Even, RouletteBetKind.Red, RouletteBetKind.Black, RouletteBetKind.Odd, RouletteBetKind.High };
            float evenW = (PxGridX1 - PxGridX0) / 6f;
            foreach (var kind in new[] { RouletteBetKind.Red, RouletteBetKind.Black, RouletteBetKind.Odd, RouletteBetKind.Even, RouletteBetKind.Low, RouletteBetKind.High })
            {
                int k = System.Array.IndexOf(evenOrder, kind);
                spots.Add(Spot(spotsRoot, kind, 0, PxGridX0 + k * evenW, PxDozenY1, PxGridX0 + (k + 1) * evenW, PxEvenY1));
            }
            float dozenW = (PxGridX1 - PxGridX0) / 3f;
            for (int d = 0; d < 3; d++)
                spots.Add(Spot(spotsRoot, (RouletteBetKind)((int)RouletteBetKind.Dozen1 + d), 0, PxGridX0 + d * dozenW, PxGridY1, PxGridX0 + (d + 1) * dozenW, PxDozenY1));
            // 列（2:1）：手前の段から 1・4・7…、2・5・8…、3・6・9…
            for (int col = 0; col < 3; col++)
                spots.Add(Spot(spotsRoot, (RouletteBetKind)((int)RouletteBetKind.Column1 + col), 0, PxGridX1, PxGridY1 - (col + 1) * PxRow, PxColumnX1, PxGridY1 - col * PxRow));

            // 立て札：結果・秒読み・これまでの数字（盤の奥の板の前）
            var board = Child(root.transform, "Anchor_Board", new Vector3(Art.RlWheelCenter.x, 1.71f, -.62f));
            var result = Label(board, "Text_Result", "？", new Vector3(0, .13f, 0), 3.0f, new Color(1f, .88f, .62f));
            ((RectTransform)result.transform).sizeDelta = new Vector2(1.0f, .46f);
            var status = Label(board, "Text_Status", "賭ける場所を見て E", new Vector3(0, -.13f, 0), 1.0f, new Color(.96f, .94f, .88f));
            ((RectTransform)status.transform).sizeDelta = new Vector2(1.0f, .2f);
            var history = Label(board, "Text_History", "", new Vector3(0, -.28f, 0), .85f, new Color(.97f, .4f, .73f));
            ((RectTransform)history.transform).sizeDelta = new Vector2(1.0f, .2f);

            var so = new SerializedObject(table);
            so.FindProperty("bodyCollider").objectReferenceValue = body;
            so.FindProperty("serverInteractDistance").floatValue = 3.5f;
            SetIntArray(so.FindProperty("betAmounts"), Roulette2FBets);
            so.FindProperty("maxBetPerPlayer").intValue = Roulette2FMax;
            so.FindProperty("zeroRefundsEvenMoney").boolValue = false;
            so.FindProperty("chipUnit").intValue = Roulette2FBets[0];
            var sp = so.FindProperty("spots");
            sp.arraySize = spots.Count;
            for (int i = 0; i < spots.Count; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = spots[i];
            so.FindProperty("wheelPivot").objectReferenceValue = wheel;
            so.FindProperty("ball").objectReferenceValue = ball;
            so.FindProperty("ballOuterRadius").floatValue = Art.RlWheelRadius * .86f;
            so.FindProperty("ballPocketRadius").floatValue = Art.RlWheelRadius * .645f;
            so.FindProperty("ballOuterHeight").floatValue = .03f;
            so.FindProperty("ballPocketHeight").floatValue = .022f;
            so.FindProperty("resultText").objectReferenceValue = result;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("historyText").objectReferenceValue = history;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RoulettePath);
        }
        finally { Object.DestroyImmediate(root); }

        MakeVariant(RoulettePath, Roulette3FPath, r =>
        {
            var so = new SerializedObject(r.GetComponent<RouletteTable>());
            SetIntArray(so.FindProperty("betAmounts"), Roulette3FBets);
            so.FindProperty("maxBetPerPlayer").intValue = Roulette3FMax;
            so.FindProperty("zeroRefundsEvenMoney").boolValue = true;
            so.FindProperty("chipUnit").intValue = Roulette3FBets[0];
            so.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    /// <summary>賭ける場所 1 つ：絵のマス（px の左上と右下）に合わせた当たり判定と札、チップ（はじめは隠しておく）。</summary>
    static RouletteSpot Spot(Transform parent, RouletteBetKind kind, int number, float px0, float py0, float px1, float py1)
    {
        Vector2 a = Art.RouletteLayoutPoint(px0, py0), b = Art.RouletteLayoutPoint(px1, py1);
        Vector2 center = (a + b) / 2f, size = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        string name = kind == RouletteBetKind.Number ? "Spot_" + number : "Spot_" + kind;
        var t = Child(parent, name, new Vector3(center.x, 0, center.y));
        var c = t.gameObject.AddComponent<BoxCollider>();
        c.center = new Vector3(0, .015f, 0);
        c.size = new Vector3(size.x - .006f, .03f, size.y - .006f);
        var spot = t.gameObject.AddComponent<RouletteSpot>();
        spot.kind = kind; spot.number = number;

        // チップは数字にかぶらないよう、マスの手前寄りに置く
        var chips = Child(t, "Anchor_Chips", new Vector3(0, .004f, size.y * .22f));
        Art.Place(Child(chips, "Visual"), "Chip", Art.Chip("chip_blue"), default, 0, false, false);
        chips.gameObject.SetActive(false);
        spot.chips = chips;
        return spot;
    }
}
#endif
