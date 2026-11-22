#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using static RoomiesRestyleKit;
using Object = UnityEngine.Object;

/// <summary>
/// ベッドと冷蔵庫の見た目を差し替える（メニュー「Roomies/Art/Restyle/Restyle Night Prefabs」）。
/// 触るのは `Visual` の中だけ。仮の見た目（Placeholder・扉の箱）は消さずに非表示にし、
/// 新しい見た目を `Visual/Restyle` に置く。ルートのスクリプト・当たり判定・軸・アンカーはそのまま。
/// 何度実行しても同じ結果。
/// </summary>
internal static partial class RoomiesRoomRestyleBuilder
{
    [MenuItem("Roomies/Art/Restyle/Restyle Night Prefabs")]
    internal static void RestyleNightPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Begin(); Palette();
        rng = new System.Random(7);
        MarkStatic = false;
        try
        {
            EditPrefab("Assets/_Prefabs/Night/Bed.prefab", BedVisual);
            EditPrefab("Assets/_Prefabs/Night/NightFridge.prefab", FridgeVisual);
        }
        finally { MarkStatic = true; }
        End();
        Debug.Log("[Roomies Restyle] Bed and NightFridge visuals replaced (Visual/Restyle).");
    }

    static void EditPrefab(string path, Action<Transform> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root.transform); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Visual の下に Restyle を作り直し、それ以外の子は非表示にする。</summary>
    static Transform FreshVisual(Transform visual)
    {
        var old = visual.Find("Restyle");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (Transform child in visual) child.gameObject.SetActive(false);
        return Group(visual, "Restyle");
    }

    static void BedVisual(Transform bed) => BedParts(FreshVisual(bed.Find("Visual")));

    static void BedParts(Transform v)
    {
        var sheet = Mat("bed_sheet", "E4DCCB", .06f, 0, null, 1, AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/fabric.png"), new Vector2(2, 3));
        var duvet = Mat("bed_duvet", "5F6FA0", .06f, 0, null, 1, AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/fabric.png"), new Vector2(2, 3));
        var duvetLight = Mat("bed_duvet_light", "8A97BF", .06f, 0, null, 1, AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/fabric.png"), new Vector2(2, 1));
        RBox(v, "Frame", V(0, .2f, 0), V(1.1f, .2f, 2.0f), .02f, woodMid);
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) Box(v, "Leg", V(sx * .49f, .05f, sz * .93f), V(.08f, .1f, .08f), woodDark);
        RBox(v, "Headboard", V(0, .56f, 1.0f), V(1.12f, .66f, .07f), .03f, woodMid);
        RBox(v, "Footboard", V(0, .3f, -1.0f), V(1.12f, .3f, .05f), .02f, woodMid);
        RBox(v, "Mattress", V(0, .38f, 0), V(1.02f, .2f, 1.9f), .05f, sheet);
        RBox(v, "Duvet", V(0, .49f, -.3f), V(1.08f, .11f, 1.28f), .05f, duvet, V(0, 0, 0));
        RBox(v, "Duvet fold", V(0, .525f, .3f), V(1.08f, .07f, .22f), .034f, duvetLight);
        RBox(v, "Duvet side", V(.54f, .36f, -.3f), V(.04f, .26f, 1.24f), .018f, duvet);
        RBox(v, "Duvet side", V(-.54f, .36f, -.3f), V(.04f, .26f, 1.24f), .018f, duvet);
        RBox(v, "Pillow", V(.03f, .535f, .72f), V(.64f, .12f, .36f), .055f, sheet, V(0, 5, 0));
    }

    static void FridgeVisual(Transform fridge)
    {
        FridgeBodyParts(FreshVisual(fridge.Find("Visual")));
        FridgeDoorParts(FreshVisual(fridge.Find("Pivot_Door/Visual")));
    }

    // 冷蔵庫は扉が -Z 側（プレハブの作りのまま）
    static void FridgeBodyParts(Transform v)
    {
        var bodyGreen = Mat("fridge_green", "6C9558", .45f);
        RBox(v, "Body", V(0, .91f, .06f), V(.78f, 1.78f, .62f), .03f, bodyGreen);
        Box(v, "Base", V(0, .04f, .06f), V(.74f, .08f, .58f), black);
        RBox(v, "Box on top", V(-.12f, 1.9f, .1f), V(.36f, .2f, .3f), .01f, cardboard, V(0, 12, 0));
        RBox(v, "Tin on top", V(.22f, 1.86f, .05f), V(.2f, .12f, .2f), .02f, red, V(0, -8, 0));
    }

    /// <summary>扉。親は蝶番（Pivot_Door）の位置。扉の中心は x +0.38。</summary>
    static void FridgeDoorParts(Transform d)
    {
        var bodyGreen = Mat("fridge_green", "6C9558", .45f);
        var bodyGreenDark = Mat("fridge_green_dark", "587C48", .45f);
        RBox(d, "Freezer door", V(.38f, .57f, 0), V(.77f, .6f, .09f), .025f, bodyGreen);
        RBox(d, "Fridge door", V(.38f, -.31f, 0), V(.77f, 1.12f, .09f), .025f, bodyGreen);
        Box(d, "Door seam", V(.38f, .26f, .0f), V(.75f, .02f, .07f), bodyGreenDark);
        RBox(d, "Handle", V(.7f, .5f, -.065f), V(.035f, .34f, .04f), .012f, metalGrey);
        RBox(d, "Handle", V(.7f, .02f, -.065f), V(.035f, .46f, .04f), .012f, metalGrey);
        // 貼り紙とマグネット（扉と一緒に動く）
        const float z = -.047f;
        // メモはアトラスの絵（fridge_notes_atlas）を貼った板。扉の表（-Z）へ向ける。数字は（絵の番号, 位置, 高さ, 傾き）
        (int n, Vector2 at, float h, float roll)[] notes =
        {
            (0, new Vector2(.25f, .6f), .18f, 5), (3, new Vector2(.48f, .48f), .15f, -7), (2, new Vector2(.3f, -.05f), .34f, -3),
            (5, new Vector2(.53f, -.32f), .16f, 8), (1, new Vector2(.2f, -.52f), .2f, -10),
        };
        foreach (var note in notes)
            MeshPart(d, "Note", NoteMesh(note.n), new[] { propsCutout }, V(note.at.x, note.at.y, z - .001f), V(0, 180, note.roll), note.h, false);
        Material[] magnets = { red, blue, yellow, purple, green };
        Vector2[] spots = { new Vector2(.25f, .69f), new Vector2(.47f, .555f), new Vector2(.3f, .11f), new Vector2(.52f, -.25f), new Vector2(.12f, -.5f), new Vector2(.6f, .7f), new Vector2(.62f, -.62f) };
        for (int i = 0; i < spots.Length; i++) Cyl(d, "Magnet", V(spots[i].x, spots[i].y, z - .004f), .035f, .012f, magnets[i % magnets.Length], V(90, 0, 0));
    }
}
#endif
