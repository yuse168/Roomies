#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// カジノのギミックのプレハブを作る（Roomies/Casino/2 Build Gimmick Prefabs）。置き場所は Assets/_Prefabs/Casino/。
///
///  CasinoElevator.prefab … ルート：NetworkObject・CasinoElevator。原点は 1 階の箱の床の真ん中、扉の向きが +Z。
///     Floor_1F / Floor_2F / Floor_3F（階の高さだけ上に置く）それぞれに：
///       Visual            … 壁・箱の内装・ぶつかるだけの当たり判定（差し替えるときはここを入れ替える）
///       Anchor_Cabin      … 箱の床の真ん中（人を移すときの基準）
///       Pivot_DoorL / R   … 引き戸の軸（当たり判定つき。見た目は中の Visual）
///       Button_Call       … 外の呼びボタン
///       Button_Go_1F〜3F  … 箱の中の行き先ボタン（Lit が点いた見た目）
///       Button_Key        … 箱の中の鍵穴
///       Lamp_Hall_1〜3 / Lamp_Cabin_1〜3 … 階数のランプ（箱がある階だけ出す）
///       Light_Cabin       … 箱の明かり
///  CasinoClerk.prefab … ルート：NetworkObject・CasinoClerk・話しかける当たり判定。Pivot_Body/Visual が体。
///
/// 見た目は Look で始まる関数にまとめてある（形を替えるときはそこだけ直す）。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    internal const string CasinoFolder = "Assets/_Prefabs/Casino";
    internal const string ElevatorPath = CasinoFolder + "/CasinoElevator.prefab";
    internal const string ClerkPath = CasinoFolder + "/CasinoClerk.prefab";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset";

    // 箱の内のりの半分・壁の厚さ・扉の開口の半分・扉の高さ・箱の天井
    const float EvHalfX = 1.2f, EvHalfZ = 1.025f, EvWall = .2f, EvDoorHalf = .7f, EvDoorH = 2.3f, EvCeil = 2.6f;
    // 箱の奥の内張りを、建物の奥の壁の飾り（腰壁・柱）より手前に出す量
    const float EvBack = .1f;

    // エレベーターを置く所（1 階の箱の床の真ん中）。建物の側で空けてある範囲（KEvX0〜KEvX1、奥の壁〜KEvZ1）の真ん中
    internal static Vector3 ElevatorPosition => new Vector3(
        (RoomiesTownRebuild.KEvX0 + RoomiesTownRebuild.KEvX1) / 2f,
        RoomiesTownRebuild.KFloorY(1),
        RoomiesTownRebuild.KEvZ1 - EvWall - EvHalfZ);

    [MenuItem("Roomies/Casino/2 Build Gimmick Prefabs")]
    internal static void BuildGimmickPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(CasinoFolder)) AssetDatabase.CreateFolder("Assets/_Prefabs", "Casino");
        BuildElevatorPrefab();
        BuildClerkPrefab();
        BuildRateVariants();
        AssetDatabase.SaveAssets();
        Debug.Log("[Casino] gimmick prefabs built");
    }

    // ================================================================ 部品を置く道具
    static Material Mat(string key) => RoomiesMaterialFinishBuilder.TownMat(key);

    static Transform Child(Transform parent, string name, Vector3 localPosition = default)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    /// <summary>ローカル座標の範囲で箱を置く（見た目だけ）。</summary>
    static GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (min + max) / 2f;
        go.transform.localScale = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z));
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>ぶつかるだけの当たり判定つきの箱。</summary>
    static GameObject SolidBox(Transform parent, string name, Vector3 min, Vector3 max, Material mat)
    {
        var go = Box(parent, name, min, max, mat);
        go.AddComponent<BoxCollider>();
        return go;
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
        return tmp;
    }

    // ================================================================ エレベーター
    static void BuildElevatorPrefab()
    {
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

                var visual = Child(fl, "Visual");
                LookElevatorShaft(Child(visual, "Placeholder"), height, f);

                var anchor = Child(fl, "Anchor_Cabin");
                float doorZ = EvHalfZ + EvWall / 2f;
                var doorL = ElevatorDoor(fl, "Pivot_DoorL", new Vector3(-EvDoorHalf / 2f, 0, doorZ));
                var doorR = ElevatorDoor(fl, "Pivot_DoorR", new Vector3(EvDoorHalf / 2f, 0, doorZ));

                // 外の呼びボタン（扉の右の壁）
                ElevatorButton(fl, "Button_Call", CasinoElevatorButton.Kind.Call, f, elevator,
                    new Vector3(EvDoorHalf + .32f, 1.15f, EvHalfZ + EvWall + .02f), new Vector3(.24f, .3f, .08f), "▲▼", false);

                // 箱の中の盤（奥の壁）。上から 3 階・2 階・1 階・鍵穴
                float panelZ = -EvHalfZ + EvBack + .06f;
                Box(visual, "Panel plate", new Vector3(-.3f, .72f, -EvHalfZ + EvBack + .02f), new Vector3(.3f, 1.98f, -EvHalfZ + EvBack + .04f), Mat("casino_black"));
                for (int k = 2; k >= 0; k--)
                    ElevatorButton(fl, $"Button_Go_{k + 1}F", CasinoElevatorButton.Kind.Go, k, elevator,
                        new Vector3(0, 1.22f + k * .26f, panelZ), new Vector3(.4f, .2f, .08f), $"{k + 1}F", true);
                ElevatorButton(fl, "Button_Key", CasinoElevatorButton.Kind.KeySlot, f, elevator,
                    new Vector3(0, .92f, panelZ), new Vector3(.4f, .22f, .08f), "KEY", false);

                // 階数のランプ：外の扉の上と、箱の中の盤の上
                var lamps = new List<GameObject>();
                foreach (var (prefix, pos) in new[] { ("Lamp_Hall", new Vector3(0, EvDoorH + .3f, EvHalfZ + EvWall + .02f)), ("Lamp_Cabin", new Vector3(0, 2.2f, -EvHalfZ + EvBack + .05f)) })
                {
                    Box(visual, prefix + " plate", pos + new Vector3(-.42f, -.1f, -.02f), pos + new Vector3(.42f, .1f, 0), Mat("casino_black"));
                    for (int k = 0; k < 3; k++)
                    {
                        var p = pos + new Vector3((1 - k) * .26f, 0, 0);   // 扉の側から見て、左から 1・2・3
                        Box(visual, $"{prefix} off {k + 1}", p + new Vector3(-.09f, -.06f, 0), p + new Vector3(.09f, .06f, .012f), Mat("slot_screen"));
                        var lamp = Child(fl, $"{prefix}_{k + 1}", p);
                        Box(Child(lamp, "Visual"), "Lit", new Vector3(-.09f, -.06f, .012f), new Vector3(.09f, .06f, .024f), Mat("neon_pink"));
                        Label(lamp, "Number", (k + 1).ToString(), new Vector3(0, 0, .03f), 1.1f, new Color(.15f, .02f, .08f));
                        lamps.Add(lamp.gameObject);
                    }
                }

                // 箱の明かり
                var lightGo = Child(fl, "Light_Cabin", new Vector3(0, EvCeil - .25f, 0)).gameObject;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point; light.intensity = 2.6f; light.range = 4.2f; light.color = new Color(1f, .86f, .66f); light.shadows = LightShadows.None;
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
            so.FindProperty("cabinSize").vector3Value = new Vector3(EvHalfX * 2, EvCeil, EvHalfZ * 2);
            so.FindProperty("doorClosedOffset").floatValue = EvDoorHalf / 2f;
            so.FindProperty("doorSlide").floatValue = EvDoorHalf - .02f;
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
        c.center = new Vector3(0, EvDoorH / 2f, 0);
        c.size = new Vector3(EvDoorHalf, EvDoorH, .1f);
        LookElevatorDoor(Child(pivot, "Visual"));
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
        GameObject lit = LookElevatorButton(visual, size, kind, withLit);
        Label(b, "Anchor_Label", text, new Vector3(0, 0, size.z / 2f + .012f), kind == CasinoElevatorButton.Kind.Call ? .9f : 1.3f, new Color(.12f, .08f, .1f));

        var so = new SerializedObject(button);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("floor").intValue = index;
        so.FindProperty("elevator").objectReferenceValue = elevator;
        so.FindProperty("litVisual").objectReferenceValue = lit;
        so.FindProperty("interactDistance").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- 見た目（仮。段階 6 で作り直す） ----

    /// <summary>その階の、箱を囲む壁（外から見える面）と、箱の内装。壁と天井にはぶつかる。</summary>
    static void LookElevatorShaft(Transform g, float height, int floor)
    {
        var outer = Mat("casino_wood"); var inner = Mat("casino_cream"); var trim = Mat("casino_gold");
        float ox = EvHalfX + EvWall, fz0 = EvHalfZ, fz1 = EvHalfZ + EvWall;
        // 左右の壁、扉の左右と上の壁（その階の天井まで）
        SolidBox(g, "Shaft wall", new Vector3(-ox, 0, -EvHalfZ), new Vector3(-EvHalfX, height, fz1), outer);
        SolidBox(g, "Shaft wall", new Vector3(EvHalfX, 0, -EvHalfZ), new Vector3(ox, height, fz1), outer);
        SolidBox(g, "Shaft front", new Vector3(-EvHalfX, 0, fz0), new Vector3(-EvDoorHalf, height, fz1), outer);
        SolidBox(g, "Shaft front", new Vector3(EvDoorHalf, 0, fz0), new Vector3(EvHalfX, height, fz1), outer);
        SolidBox(g, "Shaft front", new Vector3(-EvDoorHalf, EvDoorH, fz0), new Vector3(EvDoorHalf, height, fz1), outer);
        // 扉の枠
        Box(g, "Door frame", new Vector3(-EvDoorHalf - .08f, 0, fz1), new Vector3(-EvDoorHalf, EvDoorH + .08f, fz1 + .04f), trim);
        Box(g, "Door frame", new Vector3(EvDoorHalf, 0, fz1), new Vector3(EvDoorHalf + .08f, EvDoorH + .08f, fz1 + .04f), trim);
        Box(g, "Door frame", new Vector3(-EvDoorHalf - .08f, EvDoorH, fz1), new Vector3(EvDoorHalf + .08f, EvDoorH + .08f, fz1 + .04f), trim);
        // 箱の内装：床・天井・左右と奥の内張り・腰の手すり
        Box(g, "Cabin floor", new Vector3(-EvHalfX, 0, -EvHalfZ), new Vector3(EvHalfX, .012f, fz1), Mat("casino_red"));
        SolidBox(g, "Cabin ceiling", new Vector3(-EvHalfX, EvCeil, -EvHalfZ), new Vector3(EvHalfX, EvCeil + .1f, fz0), inner);
        Box(g, "Cabin lining", new Vector3(-EvHalfX, 0, -EvHalfZ), new Vector3(-EvHalfX + .02f, EvCeil, fz0), inner);
        Box(g, "Cabin lining", new Vector3(EvHalfX - .02f, 0, -EvHalfZ), new Vector3(EvHalfX, EvCeil, fz0), inner);
        Box(g, "Cabin lining", new Vector3(-EvHalfX, 0, -EvHalfZ + EvBack), new Vector3(EvHalfX, EvCeil, -EvHalfZ + EvBack + .02f), inner);
        Box(g, "Cabin rail", new Vector3(-EvHalfX + .02f, .95f, -EvHalfZ + .3f), new Vector3(-EvHalfX + .07f, 1.0f, fz0 - .2f), trim);
        Box(g, "Cabin rail", new Vector3(EvHalfX - .07f, .95f, -EvHalfZ + .3f), new Vector3(EvHalfX - .02f, 1.0f, fz0 - .2f), trim);
        Box(g, "Cabin lamp", new Vector3(-.3f, EvCeil - .04f, -.3f), new Vector3(.3f, EvCeil, .3f), Mat("bulb"));
    }

    static void LookElevatorDoor(Transform g)
    {
        Box(g, "Door panel", new Vector3(-EvDoorHalf / 2f, 0, -.04f), new Vector3(EvDoorHalf / 2f, EvDoorH, .04f), Mat("slot_chrome"));
        Box(g, "Door inlay", new Vector3(-EvDoorHalf / 2f + .1f, .3f, .04f), new Vector3(EvDoorHalf / 2f - .1f, EvDoorH - .3f, .05f), Mat("casino_gold"));
    }

    /// <summary>ボタンの見た目。withLit のときは「点いた」見た目（Lit）を返す。</summary>
    static GameObject LookElevatorButton(Transform g, Vector3 size, CasinoElevatorButton.Kind kind, bool withLit)
    {
        var h = size / 2f;
        Box(g, "Button base", new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, h.y, 0), Mat("casino_gold"));
        Box(g, "Button cap", new Vector3(-h.x + .03f, -h.y + .03f, 0), new Vector3(h.x - .03f, h.y - .03f, h.z), Mat(kind == CasinoElevatorButton.Kind.KeySlot ? "slot_chrome" : "casino_cream"));
        if (!withLit) return null;
        return Box(g, "Lit", new Vector3(-h.x + .03f, -h.y + .03f, h.z), new Vector3(h.x - .03f, h.y - .03f, h.z + .006f), Mat("bulb"));
    }

    // ================================================================ 店の人
    static void BuildClerkPrefab()
    {
        var root = new GameObject("CasinoClerk");
        try
        {
            root.AddComponent<NetworkObject>();
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, .85f, 0); capsule.radius = .48f; capsule.height = 1.7f;
            var clerk = root.AddComponent<CasinoClerk>();

            var pivot = Child(root.transform, "Pivot_Body");
            LookClerk(Child(Child(pivot, "Visual"), "Placeholder"));

            var so = new SerializedObject(clerk);
            so.FindProperty("bodyPivot").objectReferenceValue = pivot;
            so.FindProperty("interactDistance").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ClerkPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// <summary>店の人の見た目（仮。段階 6 で作り直す）。正面は +Z。プレイヤーのキャラ（Bob）の素材や形は使わない。</summary>
    static void LookClerk(Transform g)
    {
        GameObject Ball(string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(g, false);
            go.transform.localPosition = center; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
        Ball("Body", new Vector3(0, .8f, 0), new Vector3(.95f, 1.55f, .85f), Mat("casino_cream"));
        Box(g, "Vest", new Vector3(-.36f, .45f, -.3f), new Vector3(.36f, 1.0f, .36f), Mat("casino_black"));
        Box(g, "Bow tie", new Vector3(-.12f, 1.02f, .36f), new Vector3(.12f, 1.1f, .4f), Mat("casino_red"));
        Box(g, "Visor", new Vector3(-.4f, 1.36f, -.1f), new Vector3(.4f, 1.42f, .52f), Mat("casino_black"));
        Ball("Eye", new Vector3(-.14f, 1.22f, .37f), new Vector3(.07f, .11f, .05f), Mat("casino_black"));
        Ball("Eye", new Vector3(.14f, 1.22f, .37f), new Vector3(.07f, .11f, .05f), Mat("casino_black"));
    }
}
#endif
