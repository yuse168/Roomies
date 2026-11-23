#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// カジノのギミックのプレハブを作る（Roomies/Casino/2 Build Gimmick Prefabs）。置き場所は Assets/_Prefabs/Casino/。
///
///  CasinoElevator.prefab … ルート：NetworkObject・CasinoElevator。原点は 1 階の箱の床の真ん中、扉の向きが +Z。
///     Floor_1F / Floor_2F / Floor_3F（階の高さだけ上に置く）それぞれに：
///       Visual            … 壁・箱の内装（1 つのメッシュ）と、ぶつかるだけの当たり判定（Collision）。差し替えるときはここを入れ替える
///       Anchor_Cabin      … 箱の床の真ん中（人を移すときの基準）
///       Pivot_DoorL / R   … 引き戸の軸（当たり判定つき。見た目は中の Visual）
///       Button_Call       … 外の呼びボタン
///       Button_Go_1F〜3F  … 箱の中の行き先ボタン（Visual/Lit が点いた見た目）
///       Button_Key        … 箱の中の鍵穴
///       Lamp_Hall_1〜3 / Lamp_Cabin_1〜3 … 階数のランプ（箱がある階だけ出す）
///       Light_Cabin       … 箱の明かり
///  CasinoClerk.prefab … ルート：NetworkObject・CasinoClerk・話しかける当たり判定。Pivot_Body/Visual が体。
///
/// 形は RoomiesCasinoArt.Gimmicks.cs（物 1 つを 1 つのメッシュで作る）。ここは部品の置き方と、スクリプトへのつなぎ込み。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    internal const string CasinoFolder = "Assets/_Prefabs/Casino";
    internal const string ElevatorPath = CasinoFolder + "/CasinoElevator.prefab";
    internal const string ClerkPath = CasinoFolder + "/CasinoClerk.prefab";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset";

    // エレベーターを置く所（1 階の箱の床の真ん中）。建物の側で空けてある範囲（KEvX0〜KEvX1、奥の壁〜KEvZ1）の真ん中
    internal static Vector3 ElevatorPosition => new Vector3(
        (RoomiesTownRebuild.KEvX0 + RoomiesTownRebuild.KEvX1) / 2f,
        RoomiesTownRebuild.KFloorY(1),
        RoomiesTownRebuild.KEvZ1 - Art.EvWall - Art.EvHalfZ);

    [MenuItem("Roomies/Casino/2 Build Gimmick Prefabs")]
    internal static void BuildGimmickPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(CasinoFolder)) AssetDatabase.CreateFolder("Assets/_Prefabs", "Casino");
        Art.Begin();
        BuildElevatorPrefab();
        BuildClerkPrefab();
        RebuildSlotPrefab();
        RebuildBlackjackPrefab();
        BuildRateVariants();
        BuildRoulettePrefabs();
        BuildGamePrefabs();
        Art.End();
        Debug.Log("[Casino] gimmick prefabs built");
    }

    // ================================================================ 部品を置く道具
    static Transform Child(Transform parent, string name, Vector3 localPosition = default)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    /// <summary>ぶつかるだけの当たり判定（見た目なし）。Visual の中の Collision にまとめる。</summary>
    static void Wall(Transform collision, string name, Vector3 min, Vector3 max)
    {
        var c = Child(collision, name).gameObject.AddComponent<BoxCollider>();
        c.center = (min + max) / 2f; c.size = max - min;
    }

    static TextMeshPro Label(Transform parent, string name, string text, Vector3 localPosition, float size, Color color)
    {
        var t = new GameObject(name, typeof(RectTransform)).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(0, 180, 0);   // 文字の表が +Z を向く
        var tmp = t.gameObject.AddComponent<TextMeshPro>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        tmp.fontSharedMaterial = tmp.font != null ? tmp.font.material : null;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        ((RectTransform)t).sizeDelta = new Vector2(.6f, .25f);
        t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return tmp;
    }

    // ================================================================ エレベーター
    static void BuildElevatorPrefab()
    {
        float hx = Art.EvHalfX, hz = Art.EvHalfZ, wall = Art.EvWall, door = Art.EvDoorHalf, doorH = Art.EvDoorH, ceil = Art.EvCeil;
        var root = new GameObject("CasinoElevator");
        try
        {
            root.AddComponent<NetworkObject>();
            var elevator = root.AddComponent<CasinoElevator>();

            var floorParts = new List<(Transform anchor, Transform doorL, Transform doorR, List<GameObject> lamps)>();
            var lights = new List<Light>();
            for (int f = 0; f < 3; f++)
            {
                float y = RoomiesTownRebuild.KFloorY(f + 1) - RoomiesTownRebuild.KFloorY(1);
                float height = RoomiesTownRebuild.KCeilY(f + 1) - RoomiesTownRebuild.KFloorY(f + 1);
                var fl = Child(root.transform, $"Floor_{f + 1}F", new Vector3(0, y, 0));

                // 見た目（1 つのメッシュ）と、ぶつかるだけの当たり判定
                var visual = Child(fl, "Visual");
                Art.Place(visual, "Shaft", Art.ElevatorShaft(height, f));
                var col = Child(visual, "Collision");
                float ox = hx + wall, fz1 = hz + wall;
                Wall(col, "Side wall L", new Vector3(-ox, 0, -hz), new Vector3(-hx, height, fz1));
                Wall(col, "Side wall R", new Vector3(hx, 0, -hz), new Vector3(ox, height, fz1));
                Wall(col, "Front wall L", new Vector3(-hx, 0, hz), new Vector3(-door, height, fz1));
                Wall(col, "Front wall R", new Vector3(door, 0, hz), new Vector3(hx, height, fz1));
                Wall(col, "Front wall top", new Vector3(-door, doorH, hz), new Vector3(door, height, fz1));
                Wall(col, "Cabin ceiling", new Vector3(-hx, ceil, -hz), new Vector3(hx, ceil + .1f, hz));

                var anchor = Child(fl, "Anchor_Cabin");
                float doorZ = hz + wall / 2f;
                var doorL = ElevatorDoor(fl, "Pivot_DoorL", new Vector3(-door / 2f, 0, doorZ));
                var doorR = ElevatorDoor(fl, "Pivot_DoorR", new Vector3(door / 2f, 0, doorZ));

                // 外の呼びボタン（扉の右の壁）
                ElevatorButton(fl, "Button_Call", CasinoElevatorButton.Kind.Call, f, elevator,
                    new Vector3(door + .32f, 1.15f, fz1 + .02f), new Vector3(.24f, .3f, .08f), "呼ぶ", false);

                // 箱の中の盤（奥の壁）。上から 3 階・2 階・1 階・鍵穴
                float panelZ = -hz + Art.EvBack + .1f;
                for (int k = 2; k >= 0; k--)
                    ElevatorButton(fl, $"Button_Go_{k + 1}F", CasinoElevatorButton.Kind.Go, k, elevator,
                        new Vector3(0, 1.22f + k * .26f, panelZ), new Vector3(.4f, .2f, .08f), $"{k + 1}F", true);
                ElevatorButton(fl, "Button_Key", CasinoElevatorButton.Kind.KeySlot, f, elevator,
                    new Vector3(0, .92f, panelZ), new Vector3(.4f, .22f, .08f), "KEY", false);

                // 階数のランプ：外の扉の上と、箱の中の盤の上（左から 1・2・3）
                var lamps = new List<GameObject>();
                foreach (var (prefix, pos) in new[] { ("Lamp_Hall", new Vector3(0, doorH + .3f, fz1 + .062f)), ("Lamp_Cabin", new Vector3(0, 2.2f, -hz + Art.EvBack + .072f)) })
                {
                    Art.Place(visual, prefix + " off", Art.LampRowOff(), pos, 0, false, false);
                    for (int k = 0; k < 3; k++)
                    {
                        var lamp = Child(fl, $"{prefix}_{k + 1}", pos + new Vector3((1 - k) * .26f, 0, .006f));
                        Art.Place(Child(lamp, "Visual"), "Lit", Art.GlowPlate(.18f, .12f, "pink"), default, 0, false, false);
                        Label(lamp, "Anchor_Label", (k + 1).ToString(), new Vector3(0, 0, .016f), 1.0f, new Color(.2f, .03f, .1f));
                        lamps.Add(lamp.gameObject);
                    }
                }

                // 箱の明かり
                var light = Child(fl, "Light_Cabin", new Vector3(0, ceil - .3f, 0)).gameObject.AddComponent<Light>();
                light.type = LightType.Point; light.intensity = 2.4f; light.range = 4.2f; light.color = new Color(1f, .82f, .58f); light.shadows = LightShadows.None;
                lights.Add(light);

                floorParts.Add((anchor, doorL, doorR, lamps));
            }

            var so = new SerializedObject(elevator);
            var floors = so.FindProperty("floors");
            floors.arraySize = floorParts.Count;
            for (int f = 0; f < floorParts.Count; f++)
            {
                var p = floors.GetArrayElementAtIndex(f);
                p.FindPropertyRelative("cabinAnchor").objectReferenceValue = floorParts[f].anchor;
                p.FindPropertyRelative("doorLeft").objectReferenceValue = floorParts[f].doorL;
                p.FindPropertyRelative("doorRight").objectReferenceValue = floorParts[f].doorR;
                var lit = p.FindPropertyRelative("lampsLit");
                lit.arraySize = floorParts[f].lamps.Count;
                for (int i = 0; i < floorParts[f].lamps.Count; i++) lit.GetArrayElementAtIndex(i).objectReferenceValue = floorParts[f].lamps[i];
            }
            so.FindProperty("cabinSize").vector3Value = new Vector3(hx * 2, ceil, hz * 2);
            so.FindProperty("doorClosedOffset").floatValue = door / 2f;
            so.FindProperty("doorSlide").floatValue = door - .02f;
            var cl = so.FindProperty("cabinLights");
            cl.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++) cl.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ElevatorPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// <summary>引き戸 1 枚：軸（当たり判定つき）と、中の見た目。閉じたとき、開口の半分をふさぐ。</summary>
    static Transform ElevatorDoor(Transform floor, string name, Vector3 localPosition)
    {
        var pivot = Child(floor, name, localPosition);
        var c = pivot.gameObject.AddComponent<BoxCollider>();
        c.center = new Vector3(0, Art.EvDoorH / 2f, 0);
        c.size = new Vector3(Art.EvDoorHalf, Art.EvDoorH, .1f);
        Art.Place(Child(pivot, "Visual"), "Door", Art.ElevatorDoor());
        return pivot;
    }

    static void ElevatorButton(Transform floor, string name, CasinoElevatorButton.Kind kind, int index, CasinoElevator elevator,
        Vector3 localPosition, Vector3 size, string text, bool withLit)
    {
        var b = Child(floor, name, localPosition);
        var c = b.gameObject.AddComponent<BoxCollider>();
        c.size = size;
        var button = b.gameObject.AddComponent<CasinoElevatorButton>();
        var visual = Child(b, "Visual");
        Art.Place(visual, "Button", Art.ElevatorButton(size, kind == CasinoElevatorButton.Kind.KeySlot), default, 0, false, false);
        GameObject lit = null;
        if (withLit) lit = Art.Place(visual, "Lit", Art.GlowPlate(size.x - .06f, size.y - .06f, "warm"), new Vector3(0, 0, size.z / 2f), 0, false, false);
        if (kind != CasinoElevatorButton.Kind.KeySlot)
            Label(b, "Anchor_Label", text, new Vector3(0, 0, size.z / 2f + .012f), kind == CasinoElevatorButton.Kind.Call ? .8f : 1.3f, new Color(.16f, .1f, .12f));

        var so = new SerializedObject(button);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("floor").intValue = index;
        so.FindProperty("elevator").objectReferenceValue = elevator;
        so.FindProperty("litVisual").objectReferenceValue = lit;
        so.FindProperty("interactDistance").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================================================================ 店の人
    static void BuildClerkPrefab()
    {
        var root = new GameObject("CasinoClerk");
        try
        {
            root.AddComponent<NetworkObject>();
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, .8f, 0); capsule.radius = .48f; capsule.height = 1.6f;
            var clerk = root.AddComponent<CasinoClerk>();

            // 体は Pivot_Body の中の Visual（うなずきで軸を倒す）
            var pivot = Child(root.transform, "Pivot_Body");
            Art.Place(Child(pivot, "Visual"), "Body", Art.Clerk());

            var so = new SerializedObject(clerk);
            so.FindProperty("bodyPivot").objectReferenceValue = pivot;
            so.FindProperty("interactDistance").floatValue = 3f;
            // 頭の上の吹き出し（全員に見えるひとこと）
            var speech = BuildSpeechBubble(root, 1.95f);
            // カウンターの上の看板（踏み台の上から見て高さ 2.0〜2.6m）より下に、体の横へ出す。
            // 看板より下に収めておけば、近くからも遠くからも看板に隠れない。顔も隠さない
            speech.transform.Find("Anchor_Bubble").localPosition = new Vector3(-1.0f, 1.45f, .2f);
            so.FindProperty("bubble").objectReferenceValue = speech;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ClerkPath);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
