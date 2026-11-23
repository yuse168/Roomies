#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 階ごとのレート：上の階の台は、1 階の台のプレハブの Variant（見た目と仕組みは同じで、数値だけ違う）。
/// 数値はこの表で決め、Roomies/Casino/2 Build Gimmick Prefabs が Variant に書く。置いたあとはインスペクターで直せる。
///
///  階   賭け金            スロット（戻る割合）        ブラックジャック（最善の打ち方での戻る割合）
///  1    10 / 50 / 100     96.2%（Slot.prefab）       97.6%（BlackjackTable.prefab）
///  2    100 / 500 / 1000  97.8%（Slot_2F）           98.9%（ダブルダウンあり）
///  3    500 / 2000 / 5000 置かない                    101.2%（ダブルダウンあり・ブラックジャックは賭け金の 3 倍が戻る）
///
/// ブラックジャックの戻る割合は Docs/casino_20261123/ev.py の計算（カードは無限・親は 17 以上で止まる、のいまの決まり）。
/// スロットは SlotMachine.ExpectedReturn() と同じ式で、テスト RoomiesCasinoTests が見張る。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    const string SlotBasePath = "Assets/_Prefabs/Slot.prefab";
    const string BlackjackBasePath = "Assets/_Prefabs/BlackjackTable.prefab";
    internal const string Slot2FPath = CasinoFolder + "/Slot_2F.prefab";
    internal const string Blackjack2FPath = CasinoFolder + "/BlackjackTable_2F.prefab";
    internal const string Blackjack3FPath = CasinoFolder + "/BlackjackTable_3F.prefab";

    // 2 階のスロット：配当は 100R 賭けあたり。重みは「宝石」を 5 → 6 にして少し当たりやすく
    static readonly int[] Slot2FBets = { 100, 500, 1000 };
    static readonly (int reward, int weight)[] Slot2FSymbols = { (100, 50), (150, 30), (300, 15), (500, 6), (1000, 2) };

    static readonly int[] Blackjack2FBets = { 100, 500, 1000 };
    static readonly int[] Blackjack3FBets = { 500, 2000, 5000 };
    const float Blackjack3FNaturalReturn = 3f;   // ブラックジャックで賭け金の 3 倍が戻る（1 階・2 階は 2.5 倍）

    static void BuildRateVariants()
    {
        // スロット（2 階）
        MakeVariant(SlotBasePath, Slot2FPath, root =>
        {
            var so = new SerializedObject(root.GetComponentInChildren<SlotMachine>(true));
            SetIntArray(so.FindProperty("betAmounts"), Slot2FBets);
            var symbols = so.FindProperty("slotSymbols");
            for (int i = 0; i < symbols.arraySize && i < Slot2FSymbols.Length; i++)
            {
                symbols.GetArrayElementAtIndex(i).FindPropertyRelative("reward").intValue = Slot2FSymbols[i].reward;
                symbols.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue = Slot2FSymbols[i].weight;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        // ブラックジャック（2 階・3 階）
        MakeVariant(BlackjackBasePath, Blackjack2FPath, root =>
        {
            var so = new SerializedObject(root.GetComponent<BlackjackTable>());
            SetIntArray(so.FindProperty("betAmounts"), Blackjack2FBets);
            so.FindProperty("allowDoubleDown").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        MakeVariant(BlackjackBasePath, Blackjack3FPath, root =>
        {
            var so = new SerializedObject(root.GetComponent<BlackjackTable>());
            SetIntArray(so.FindProperty("betAmounts"), Blackjack3FBets);
            so.FindProperty("allowDoubleDown").boolValue = true;
            so.FindProperty("blackjackReturnMultiplier").floatValue = Blackjack3FNaturalReturn;
            so.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    static void SetIntArray(SerializedProperty p, int[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).intValue = values[i];
    }

    /// <summary>元のプレハブの Variant を作る（もうあれば、同じ Variant の数値だけ書き直す。GUID は変わらない）。</summary>
    static void MakeVariant(string basePath, string variantPath, System.Action<GameObject> edit)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        if (existing != null)
        {
            var contents = PrefabUtility.LoadPrefabContents(variantPath);
            try { edit(contents); PrefabUtility.SaveAsPrefabAsset(contents, variantPath); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return;
        }
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        try
        {
            edit(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    // ================================================================ シーンに置く
    // スロット：右の壁ぎわに、入口の側から奥へ。画面は部屋の中（-X）を向く。1 階は 3 台、2 階は入口側の 2 台の真上
    static readonly float[] SlotZ = { -10.9f, -12.95f, -15.0f };
    const float SlotBackX = 16.8f - .25f - .06f;   // 右の壁の内側の面から 6cm 空ける
    const float SlotYaw = 270f;
    // ブラックジャック：奥の左寄り（遊ぶ人は通りの側に立つ）。ルーレット：遊ぶ人は奥（エレベーターの側）に立ち、通りの窓の方を向く
    static readonly Vector3 Blackjack1FXZ = new Vector3(5.76f, 0f, -16.34f);
    static readonly Vector3 Blackjack2FXZ = new Vector3(4.2f, 0f, -16.6f);
    static readonly Vector3 Blackjack3FXZ = new Vector3(4.2f, 0f, -16.8f);
    static readonly Vector3 Roulette2FXZ = new Vector3(7.6f, 0f, -12.0f);
    static readonly Vector3 Roulette3FXZ = new Vector3(6.4f, 0f, -12.2f);
    // ハイ＆ロー（2 階）とチンチロ（3 階）：右寄り。遊ぶ人は奥の側に立つ
    static readonly Vector3 HiLo2FXZ = new Vector3(12.2f, 0f, -10.3f);
    static readonly Vector3 Chinchiro3FXZ = new Vector3(12.8f, 0f, -13.4f);

    /// <summary>台を置く（1 階の前からある台は場所だけ直す）。階ごとの表示の切り替えに入れる見た目を返す。</summary>
    static Dictionary<int, List<GameObject>> PlaceTables(Scene scene)
    {
        var looks = new Dictionary<int, List<GameObject>> { [2] = new List<GameObject>(), [3] = new List<GameObject>() };
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Slot2FPath) == null) BuildRateVariants();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RoulettePath) == null) BuildRoulettePrefabs();

        // ---- 1 階のスロット 3 台（前からシーンにある物を使い、名前と場所をそろえる）
        var baseSlot = AssetDatabase.LoadAssetAtPath<GameObject>(SlotBasePath);
        var old = scene.GetRootGameObjects().Where(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == baseSlot).OrderBy(g => g.name).ToList();
        float slotX = SlotBackX - RoomiesCasinoArt.SlotDepth / 2f;
        for (int i = 0; i < SlotZ.Length; i++)
        {
            string name = "Slot_1F_" + (char)('A' + i);
            if (i < old.Count) old[i].name = name;
            PlacePrefab(scene, SlotBasePath, name, new Vector3(slotX, RoomiesTownRebuild.KFloorY(1), SlotZ[i]), SlotYaw);
        }
        for (int i = SlotZ.Length; i < old.Count; i++) Object.DestroyImmediate(old[i]);

        // ---- 2 階のスロット 2 台
        for (int i = 0; i < 2; i++)
        {
            var go = PlacePrefab(scene, Slot2FPath, "Slot_2F_" + (char)('A' + i), new Vector3(slotX, RoomiesTownRebuild.KFloorY(2), SlotZ[i]), SlotYaw);
            AddLook(looks[2], go.transform.Find("Visual"));
            AddLook(looks[2], go.transform.Find("Anchor_Screen"));
        }

        // ---- ブラックジャック（1 階は前からある台）
        var baseTable = AssetDatabase.LoadAssetAtPath<GameObject>(BlackjackBasePath);
        var table1 = scene.GetRootGameObjects().FirstOrDefault(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == baseTable);
        string table1Name = table1 != null ? table1.name : "BlackjackTable";
        foreach (var (path, name, xz, floor) in new[] { (BlackjackBasePath, table1Name, Blackjack1FXZ, 1), (Blackjack2FPath, "BlackjackTable_2F", Blackjack2FXZ, 2), (Blackjack3FPath, "BlackjackTable_3F", Blackjack3FXZ, 3) })
        {
            var p = xz; p.y = RoomiesTownRebuild.KFloorY(floor);
            var go = PlacePrefab(scene, path, name, p, 0f);
            if (floor > 1) AddLook(looks[floor], go.transform.Find("Visual"));
        }

        // ---- ルーレット（台の +Z を奥へ向ける）
        foreach (var (path, name, xz, floor) in new[] { (RoulettePath, "RouletteTable_2F", Roulette2FXZ, 2), (Roulette3FPath, "RouletteTable_3F", Roulette3FXZ, 3) })
        {
            var p = xz; p.y = RoomiesTownRebuild.KFloorY(floor);
            var go = PlacePrefab(scene, path, name, p, 180f);
            foreach (string part in new[] { "Visual", "Pivot_Wheel", "Spots", "Anchor_Board" }) AddLook(looks[floor], go.transform.Find(part));
        }
        // ---- ハイ＆ロー（2 階）とチンチロ（3 階）
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HiLoPath) == null) BuildGamePrefabs();
        {
            var p = HiLo2FXZ; p.y = RoomiesTownRebuild.KFloorY(2);
            var go = PlacePrefab(scene, HiLoPath, "HiLoTable_2F", p, 180f);
            foreach (string part in new[] { "Visual", "Anchor_Card", "Anchor_Board" }) AddLook(looks[2], go.transform.Find(part));
        }
        {
            var p = Chinchiro3FXZ; p.y = RoomiesTownRebuild.KFloorY(3);
            var go = PlacePrefab(scene, ChinchiroPath, "ChinchiroTable_3F", p, 180f);
            foreach (string part in new[] { "Visual", "Anchor_Board", "Pivot_Die_1", "Pivot_Die_2", "Pivot_Die_3" }) AddLook(looks[3], go.transform.Find(part));
        }
        return looks;
    }

    static void AddLook(List<GameObject> list, Transform t) { if (t != null) list.Add(t.gameObject); }
}
#endif
