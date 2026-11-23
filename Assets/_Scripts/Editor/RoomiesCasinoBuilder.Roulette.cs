#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ルーレットの台のプレハブを作る（Roomies/Casino/2 Build Gimmick Prefabs）。
///
///  RouletteTable.prefab（2 階。賭け金 100／500／1000）と、その Variant の RouletteTable_3F.prefab（500／2000／5000、0 で 2 倍の賭けを返金）。
///   ・ルート：NetworkObject・RouletteTable・台の当たり判定。原点は足元、遊ぶ人の立つ側が +Z。
///   ・Visual：台の見た目
///   ・Pivot_Wheel：回る盤（中の Visual が見た目。Anchor_Ball が玉）
///   ・Spots/Spot_0〜36・Spot_Red など：賭ける場所（当たり判定・RouletteSpot。Visual がマスの色、Anchor_Label が文字、Anchor_Chips がチップ）
///   ・Anchor_Board：結果・秒読み・これまでの数字
///
/// 台の上の並び（遊ぶ人から見て）：左に盤、右に数字のマス（3 段 × 12 列。手前の段が 1・4・7…）、
/// その手前に「1〜12／13〜24／25〜36」、いちばん手前に「1〜18・偶数・赤・黒・奇数・19〜36」。0 は数字のマスの左。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    internal const string RoulettePath = CasinoFolder + "/RouletteTable.prefab";
    internal const string Roulette3FPath = CasinoFolder + "/RouletteTable_3F.prefab";

    // 台の天板の高さ、マスの 1 辺、数字のマスの左端（x）と手前の端（z）、盤の中心
    // （RlGridX0 は「遊ぶ人から見た左右」での左端。置くときに x の符号を返す）
    const float RlTop = .92f, RlCell = .15f, RlGridX0 = -.35f, RlGridZ1 = .15f;
    // 遊ぶ人（+Z の側に立って -Z を向く）から見て、左が +X。盤は左、数字は左から右へ増える
    static readonly Vector3 RlWheel = new Vector3(1.08f, RlTop + .01f, -.02f);

    static readonly int[] Roulette2FBets = { 100, 500, 1000 };
    static readonly int[] Roulette3FBets = { 500, 2000, 5000 };
    const int Roulette2FMax = 5000, Roulette3FMax = 25000;

    static void BuildRoulettePrefabs()
    {
        var root = new GameObject("RouletteTable");
        try
        {
            root.AddComponent<NetworkObject>();
            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(0, RlTop / 2f, 0); body.size = new Vector3(3.3f, RlTop, 1.5f);
            var table = root.AddComponent<RouletteTable>();

            LookRouletteTable(Child(Child(root.transform, "Visual"), "Placeholder"));

            // 盤と玉
            var wheel = Child(root.transform, "Pivot_Wheel", RlWheel);
            LookRouletteWheel(Child(wheel, "Visual"));
            var ball = Child(wheel, "Anchor_Ball", new Vector3(0, .03f, .27f));
            LookRouletteBall(Child(ball, "Visual"));

            // 賭ける場所
            var spotsRoot = Child(root.transform, "Spots", new Vector3(0, RlTop, 0));
            var spots = new List<RouletteSpot>();
            // 0（数字のマスの左。3 段ぶん）
            spots.Add(RouletteSpotAt(spotsRoot, RouletteBetKind.Number, 0,
                new Vector3(-(RlGridX0 - RlCell / 2f), 0, RlGridZ1 - RlCell * 1.5f), new Vector2(RlCell, RlCell * 3)));
            // 1〜36（列 c、段 r。手前の段が 3c+1）
            for (int n = 1; n <= 36; n++)
            {
                int c = (n - 1) / 3, r = (n - 1) % 3;
                spots.Add(RouletteSpotAt(spotsRoot, RouletteBetKind.Number, n,
                    new Vector3(-(RlGridX0 + (c + .5f) * RlCell), 0, RlGridZ1 - (r + .5f) * RlCell), new Vector2(RlCell, RlCell)));
            }
            // いちばん手前：2 倍の賭け（6 つ）
            var evens = new[] { RouletteBetKind.Low, RouletteBetKind.Even, RouletteBetKind.Red, RouletteBetKind.Black, RouletteBetKind.Odd, RouletteBetKind.High };
            float evenZ = RlGridZ1 + RlCell * 1.5f;
            // 置く順は Red, Black, Odd, Even, Low, High（番号がこの順になる）
            foreach (var kind in new[] { RouletteBetKind.Red, RouletteBetKind.Black, RouletteBetKind.Odd, RouletteBetKind.Even, RouletteBetKind.Low, RouletteBetKind.High })
            {
                int k = System.Array.IndexOf(evens, kind);
                spots.Add(RouletteSpotAt(spotsRoot, kind, 0,
                    new Vector3(-(RlGridX0 + (k * 2 + 1) * RlCell), 0, evenZ), new Vector2(RlCell * 2, RlCell)));
            }
            // その奥：3 つ分け
            for (int d = 0; d < 3; d++)
                spots.Add(RouletteSpotAt(spotsRoot, (RouletteBetKind)((int)RouletteBetKind.Dozen1 + d), 0,
                    new Vector3(-(RlGridX0 + (d * 4 + 2) * RlCell), 0, RlGridZ1 + RlCell * .5f), new Vector2(RlCell * 4, RlCell)));

            // 立て札：結果・秒読み・これまでの数字（盤の奥）
            var board = Child(root.transform, "Anchor_Board", new Vector3(RlWheel.x, 1.72f, -.62f));
            var result = Label(board, "Text_Result", "？", new Vector3(0, .12f, .03f), 3.2f, new Color(1f, .9f, .6f));
            ((RectTransform)result.transform).sizeDelta = new Vector2(1.0f, .5f);
            var status = Label(board, "Text_Status", "賭ける場所を見て E", new Vector3(0, -.14f, .03f), 1.1f, Color.white);
            ((RectTransform)status.transform).sizeDelta = new Vector2(1.0f, .2f);
            var history = Label(board, "Text_History", "", new Vector3(0, -.3f, .03f), .9f, new Color(.8f, .8f, .85f));
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

    /// <summary>賭ける場所 1 つ：当たり判定と札、マスの見た目、文字、チップ（はじめは隠しておく）。</summary>
    static RouletteSpot RouletteSpotAt(Transform parent, RouletteBetKind kind, int number, Vector3 center, Vector2 size)
    {
        string name = kind == RouletteBetKind.Number ? "Spot_" + number : "Spot_" + kind;
        var t = Child(parent, name, center);
        var c = t.gameObject.AddComponent<BoxCollider>();
        c.center = new Vector3(0, .015f, 0);
        c.size = new Vector3(size.x - .008f, .03f, size.y - .008f);
        var spot = t.gameObject.AddComponent<RouletteSpot>();
        spot.kind = kind; spot.number = number;

        LookRouletteCell(Child(t, "Visual"), kind, number, size);

        // 文字は台の上に寝かせる（遊ぶ人の側から読める向き）
        string text = kind == RouletteBetKind.Number ? number.ToString() : RouletteRules.SpotName(kind, number);
        var label = Label(t, "Anchor_Label", text, new Vector3(0, .006f, 0), kind == RouletteBetKind.Number ? 1.0f : .85f, Color.white);
        label.transform.localRotation = Quaternion.Euler(90, 180, 0);
        ((RectTransform)label.transform).sizeDelta = new Vector2(size.x, size.y);

        var chips = Child(t, "Anchor_Chips", new Vector3(size.x * .22f, .005f, size.y * .12f));
        LookRouletteChip(Child(chips, "Visual"));
        chips.gameObject.SetActive(false);
        spot.chips = chips;
        return spot;
    }

    // ---- 見た目（仮。段階 6 で作り直す） ----

    static GameObject Disc(Transform parent, string name, Vector3 center, float diameter, float halfHeight, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = new Vector3(diameter, halfHeight, diameter);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void LookRouletteTable(Transform g)
    {
        var wood = Mat("casino_wood"); var felt = Mat("casino_felt"); var rail = Mat("casino_leather"); var gold = Mat("casino_gold");
        Box(g, "Table body", new Vector3(-1.6f, .1f, -.7f), new Vector3(1.6f, RlTop - .06f, .7f), wood);
        Box(g, "Table base", new Vector3(-1.5f, 0, -.6f), new Vector3(1.5f, .1f, .6f), Mat("casino_black"));
        Box(g, "Table felt", new Vector3(-1.6f, RlTop - .06f, -.7f), new Vector3(1.6f, RlTop - .002f, .7f), felt);
        // ふち（革）
        Box(g, "Table rail", new Vector3(-1.65f, RlTop - .06f, .7f), new Vector3(1.65f, RlTop + .04f, .75f), rail);
        Box(g, "Table rail", new Vector3(-1.65f, RlTop - .06f, -.75f), new Vector3(1.65f, RlTop + .04f, -.7f), rail);
        Box(g, "Table rail", new Vector3(-1.65f, RlTop - .06f, -.7f), new Vector3(-1.6f, RlTop + .04f, .7f), rail);
        Box(g, "Table rail", new Vector3(1.6f, RlTop - .06f, -.7f), new Vector3(1.65f, RlTop + .04f, .7f), rail);
        // 盤の受け皿（回らない外側）
        Disc(g, "Wheel bowl", new Vector3(RlWheel.x, RlTop + .005f, RlWheel.z), .98f, .012f, wood);
        Disc(g, "Wheel bowl rim", new Vector3(RlWheel.x, RlTop + .012f, RlWheel.z), .92f, .012f, gold);
        // 立て札の柱と板
        Box(g, "Board post", new Vector3(RlWheel.x - .03f, RlTop, -.68f), new Vector3(RlWheel.x + .03f, 1.4f, -.62f), gold);
        Box(g, "Board", new Vector3(RlWheel.x - .55f, 1.34f, -.66f), new Vector3(RlWheel.x + .55f, 2.02f, -.6f), Mat("casino_black"));
        Box(g, "Board trim", new Vector3(RlWheel.x - .58f, 2.02f, -.67f), new Vector3(RlWheel.x + .58f, 2.06f, -.59f), gold);
    }

    static void LookRouletteWheel(Transform g)
    {
        Disc(g, "Wheel disc", new Vector3(0, .008f, 0), .84f, .008f, Mat("casino_wood"));
        Disc(g, "Wheel hub", new Vector3(0, .04f, 0), .3f, .03f, Mat("casino_gold"));
        Disc(g, "Wheel cap", new Vector3(0, .09f, 0), .08f, .03f, Mat("casino_gold"));
        for (int i = 0; i < RouletteRules.PocketCount; i++)
        {
            int n = RouletteRules.WheelOrder[i];
            var mat = n == 0 ? Mat("casino_felt") : RouletteRules.IsRed(n) ? Mat("chip_red") : Mat("casino_black");
            var rot = Quaternion.Euler(0, i * 360f / RouletteRules.PocketCount, 0);
            var pocket = Box(g, "Pocket " + n, new Vector3(-.021f, .016f, -.06f), new Vector3(.021f, .022f, .06f), mat);
            pocket.transform.localPosition = rot * new Vector3(0, .019f, .27f);
            pocket.transform.localRotation = rot;
        }
    }

    static void LookRouletteBall(Transform g)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Ball";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(g, false);
        go.transform.localScale = Vector3.one * .045f;
        go.GetComponent<MeshRenderer>().sharedMaterial = Mat("casino_cream");
    }

    static void LookRouletteCell(Transform g, RouletteBetKind kind, int number, Vector2 size)
    {
        Material mat =
            kind == RouletteBetKind.Number ? (number == 0 ? Mat("casino_felt_line") : RouletteRules.IsRed(number) ? Mat("chip_red") : Mat("casino_black")) :
            kind == RouletteBetKind.Red ? Mat("chip_red") :
            kind == RouletteBetKind.Black ? Mat("casino_black") : Mat("casino_wood");
        if (kind == RouletteBetKind.Number && number == 0) mat = Mat("chip_green");
        Box(g, "Cell", new Vector3(-size.x / 2 + .006f, 0, -size.y / 2 + .006f), new Vector3(size.x / 2 - .006f, .004f, size.y / 2 - .006f), mat);
    }

    static void LookRouletteChip(Transform g)
    {
        // 1 枚ぶん（高さ 1.2cm）。Anchor_Chips の y の伸ばしで山の高さになる
        Disc(g, "Chip", new Vector3(0, .006f, 0), .075f, .006f, Mat("chip_blue"));
    }
}
#endif
