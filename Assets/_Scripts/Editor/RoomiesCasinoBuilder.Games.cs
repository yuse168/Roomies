#if UNITY_EDITOR
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// ハイ＆ロー（2 階）とチンチロ（3 階）の台のプレハブを作る（Roomies/Casino/2 Build Gimmick Prefabs）。
///
///  HiLoTable.prefab      … ルート：NetworkObject・HiLoTable・HiLoTableView・台の当たり判定。Visual：台。
///                           Pivot_Card_〇・Pivot_Chips_〇：台の上のカードとチップ（RoomiesCasinoBuilder.HiLoCards.cs）。
///                           Anchor_Odds：立て札の倍率の文字。Anchor_Board：手持ちの額と案内の文字
///  ChinchiroTable.prefab … ルート：NetworkObject・ChinchiroTable・台の当たり判定。Visual：台とどんぶり。
///                           Pivot_Die_1〜3：サイコロの軸（中の Visual が見た目）。Anchor_Board：出目と賭け金の文字
/// どちらも、足元が原点で、遊ぶ人の立つ側が +Z。数値（賭け金・倍率）はプレハブのインスペクターで直せる。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    internal const string HiLoPath = CasinoFolder + "/HiLoTable.prefab";
    internal const string ChinchiroPath = CasinoFolder + "/ChinchiroTable.prefab";

    static readonly int[] HiLoBets = { 100, 500, 1000 };
    static readonly int[] ChinchiroBets = { 500, 2000, 5000 };

    static void BuildGamePrefabs()
    {
        BuildHiLoPrefab();
        BuildChinchiroPrefab();
    }

    static void BuildHiLoPrefab()
    {
        var root = new GameObject("HiLoTable");
        try
        {
            root.AddComponent<NetworkObject>();
            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(0, Art.HlTop / 2f, 0); body.size = new Vector3(Art.HlLength, Art.HlTop, Art.HlDepth);
            var table = root.AddComponent<HiLoTable>();

            Art.Place(Child(root.transform, "Visual"), "Table", Art.HiLoTable());

            // 立て札：上と下の倍率（カードそのものは、台の上の実物）
            var oddsAnchor = Child(root.transform, "Anchor_Odds", Art.HlCardCenter);
            var odds = Label(oddsAnchor, "Text_Odds", "上か\n下か", new Vector3(0, 0, .002f), .6f, new Color(1f, .88f, .62f));
            ((RectTransform)odds.transform).sizeDelta = new Vector2(Art.HlCardW - .02f, Art.HlCardH - .04f);
            odds.fontStyle = FontStyles.Bold;
            odds.lineSpacing = 18f;

            // 台の上のカードとチップ
            var view = BuildHiLoCards(root);

            var board = Child(root.transform, "Anchor_Board");
            var pot = Label(board, "Text_Pot", "賭け金 100R", new Vector3(0, Art.HlTop + .06f, -.092f), .62f, new Color(1f, .88f, .62f));
            ((RectTransform)pot.transform).sizeDelta = new Vector2(.86f, .1f);
            var hint = Label(board, "Text_Hint", "HIGH & LOW", new Vector3(0, 1.83f, -.348f), .9f, new Color(.97f, .4f, .73f));
            ((RectTransform)hint.transform).sizeDelta = new Vector2(.82f, .2f);

            var so = new SerializedObject(table);
            so.FindProperty("bodyCollider").objectReferenceValue = body;
            so.FindProperty("serverInteractDistance").floatValue = 3.5f;
            SetIntArray(so.FindProperty("betAmounts"), HiLoBets);
            so.FindProperty("oddsText").objectReferenceValue = odds;
            so.FindProperty("view").objectReferenceValue = view;
            so.FindProperty("potText").objectReferenceValue = pot;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HiLoPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void BuildChinchiroPrefab()
    {
        var root = new GameObject("ChinchiroTable");
        try
        {
            root.AddComponent<NetworkObject>();
            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(0, Art.CcTop / 2f, 0); body.size = new Vector3(Art.CcRadius * 2, Art.CcTop, Art.CcRadius * 2);
            var table = root.AddComponent<ChinchiroTable>();

            Art.Place(Child(root.transform, "Visual"), "Table", Art.ChinchiroTable());

            // サイコロ 3 つ（どんぶりの底）。はじめは 4・5・6 を上にしておく
            float y = Art.CcTop + Art.CcBowlFloor + Art.CcDie / 2f;
            var rest = new[] { new Vector3(-.055f, y, .02f), new Vector3(.05f, y, .04f), new Vector3(.005f, y, -.055f) };
            var up = new[] { Quaternion.Euler(0, 20, -90), Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 35), Quaternion.Euler(180, 70, 0) };
            var pivots = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                pivots[i] = Child(root.transform, "Pivot_Die_" + (i + 1), rest[i]);
                pivots[i].localRotation = up[i];
                Art.Place(Child(pivots[i], "Visual"), "Die", Art.Die(), default, 0, false, false);
            }

            var board = Child(root.transform, "Anchor_Board", new Vector3(0, 1.7f, -.508f));
            var result = Label(board, "Text_Result", "チンチロ", new Vector3(0, .1f, 0), 1.25f, new Color(1f, .88f, .62f));
            ((RectTransform)result.transform).sizeDelta = new Vector2(.96f, .26f);
            var status = Label(board, "Text_Status", "賭け金 500R", new Vector3(0, -.13f, 0), .8f, new Color(.96f, .94f, .88f));
            ((RectTransform)status.transform).sizeDelta = new Vector2(.96f, .18f);

            var so = new SerializedObject(table);
            so.FindProperty("bodyCollider").objectReferenceValue = body;
            so.FindProperty("serverInteractDistance").floatValue = 3.5f;
            SetIntArray(so.FindProperty("betAmounts"), ChinchiroBets);
            var dp = so.FindProperty("dicePivots");
            dp.arraySize = 3;
            for (int i = 0; i < 3; i++) dp.GetArrayElementAtIndex(i).objectReferenceValue = pivots[i];
            so.FindProperty("resultText").objectReferenceValue = result;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ChinchiroPath);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
