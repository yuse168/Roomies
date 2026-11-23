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
    // スロット：1 階の台と同じ並び（右の壁ぎわ）の、入口側から 2 台ぶん。ブラックジャック：奥の左寄り
    static readonly Vector3 Blackjack2FXZ = new Vector3(4.2f, 0f, -16.6f);
    static readonly Vector3 Blackjack3FXZ = new Vector3(4.2f, 0f, -16.8f);

    /// <summary>2 階・3 階の台を置く。階ごとの表示の切り替えに入れる見た目（Visual の中の飾りと画面）を返す。</summary>
    static Dictionary<int, List<GameObject>> PlaceUpperTables(Scene scene)
    {
        var looks = new Dictionary<int, List<GameObject>> { [2] = new List<GameObject>(), [3] = new List<GameObject>() };
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Slot2FPath) == null) BuildRateVariants();

        float up2 = RoomiesTownRebuild.KFloorY(2) - RoomiesTownRebuild.KFloorY(1);
        float up3 = RoomiesTownRebuild.KFloorY(3) - RoomiesTownRebuild.KFloorY(1);

        // 1 階のスロット（Slot.prefab の実体）を、画面の z の大きい順（入口側から）に
        var baseSlot = AssetDatabase.LoadAssetAtPath<GameObject>(SlotBasePath);
        var slots1F = scene.GetRootGameObjects()
            .Where(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == baseSlot)
            .OrderByDescending(g => g.GetComponentInChildren<Canvas>(true).transform.position.z).ToArray();
        for (int i = 0; i < 2 && i < slots1F.Length; i++)
        {
            Transform src = slots1F[i].transform;
            var go = PlacePrefab(scene, Slot2FPath, "Slot_2F_" + (char)('A' + i), src.position + Vector3.up * up2, 0f);
            go.transform.rotation = src.rotation;
            var machine = go.GetComponentInChildren<SlotMachine>(true);
            AddLook(looks[2], machine.transform.Find("Visual/Cabinet"));
            AddLook(looks[2], machine.GetComponentInChildren<Canvas>(true).transform);
        }

        // ブラックジャック：1 階の台と同じだけ床に沈める（台の原点は床より 3.7cm 下）
        var baseTable = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<BlackjackTable>() != null && g.transform.position.y < 3f);
        float sink = baseTable != null ? baseTable.transform.position.y - RoomiesTownRebuild.KFloorY(1) : 0f;
        foreach (var (path, name, xz, floor) in new[] { (Blackjack2FPath, "BlackjackTable_2F", Blackjack2FXZ, 2), (Blackjack3FPath, "BlackjackTable_3F", Blackjack3FXZ, 3) })
        {
            var p = xz; p.y = RoomiesTownRebuild.KFloorY(floor) + sink;
            var go = PlacePrefab(scene, path, name, p, 0f);
            AddLook(looks[floor], go.transform.Find("Visual/Art"));
        }
        return looks;
    }

    static void AddLook(List<GameObject> list, Transform t) { if (t != null) list.Add(t.gameObject); }
}
#endif
