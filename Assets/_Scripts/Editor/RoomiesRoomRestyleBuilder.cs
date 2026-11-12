#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static RoomiesRestyleKit;
using Object = UnityEngine.Object;

/// <summary>
/// シェアハウスの部屋の見た目を作る配置ツール（メニュー「Roomies/Art/Restyle/Build Room」）。
/// GameRoom に「Roomies_RoomRestyle」（背景・小物・明かり）と「Roomies_PostProcess」を置いて保存する。
/// 何度実行しても同じ結果（自分で作ったルートだけ作り直す）。スクリプト・通信・ギミックのプレハブには触らない。
/// 元の部屋の小物（ソファ・机・ラグ・天井灯・カーテン・床の継ぎ目）は消さずに非表示にする。
/// </summary>
internal static partial class RoomiesRoomRestyleBuilder
{
    const string ScenePath = "Assets/_Scenes/GameRoom.unity";
    const string MenuScenePath = "Assets/_Scenes/MainMenuSteam.unity";
    internal const string TitleSkyPath = "Assets/AllSkyFree/Cold Night/Cold Night.mat";
    const string RootName = "Roomies_RoomRestyle";
    const string VolumeName = "Roomies_PostProcess";
    const string ProfilePath = "Assets/Settings/RoomiesRestyleVolume.asset";
    const float F = 3.2725f;          // 床の上面
    const float Ceiling = 5.92f;      // 天井の下面
    static readonly Vector3 RoomCenter = new Vector3(17.93f, F, 18.68f);

    static Material sofaY, sofaYDark, woodDark, woodMid, woodLight, metalDark, metalGrey, black, white, paper, cardboard, cardboardDark,
        purple, purpleDark, curtain, red, orange, yellow, green, teal, blue, sharkBlue, sharkBelly, pink, leaf, leafDark, pot, brown,
        floorMat, wallMat, ceilingMat, rugMat, rugEdge, fabricGrey, cream, gold, glassBrown, cheese, crust,
        bulb, neon, neonPink, screenGreen, barLime, tvScreen, slotYellow, slotRed, water, lampShade, fairy, screenWhite, ledGreen, windowGlow;
    static Material[] canColors;
    static Material glowPurple, glowRed, glowWhite;
    static System.Random rng;

    [MenuItem("Roomies/Art/Restyle/Build Room")]
    internal static void Build() => BuildInto(ScenePath, false);

    /// <summary>タイトル（MainMenuSteam）の背景の部屋にも同じ見た目を置く。ギミックのプレハブは置かず、見た目だけ。</summary>
    [MenuItem("Roomies/Art/Restyle/Build Title Room")]
    internal static void BuildTitle() => BuildInto(MenuScenePath, true);

    static void BuildInto(string scenePath, bool menu)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the scene first: " + scenePath);

        Begin();
        Palette();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach (var old in all.Where(t => t != null && t.name == RootName).Select(t => t.gameObject).ToArray()) Object.DestroyImmediate(old);
        var rootGo = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootGo, scene);
        rootGo.isStatic = true;
        var root = rootGo.transform;
        rng = new System.Random(20261113);

        var house = all.FirstOrDefault(t => t != null && t.name == "Shared house - lounge and terrace");
        RestyleExistingHouse(house, root);
        Shell(root);
        Lounge(root);
        TvCorner(root);
        Desk(root);
        Shelf(root);
        Kitchen(root);
        SouthWall(root);
        Corners(root);
        Clutter(root);
        Lights(root);
        if (menu) TitleExtras(scene, root);
        PostProcess(scene);
        End();

        Physics.SyncTransforms();
        if (!menu)
            foreach (var point in scene.GetRootGameObjects().Where(g => g.GetComponent<SpawnPoint>() != null))
            {
                var hits = Physics.OverlapCapsule(point.transform.position + Vector3.up * .5f, point.transform.position - Vector3.up * .4f, .5f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => c.transform.IsChildOf(root)).Select(c => c.name).ToArray();
                if (hits.Length > 0) Debug.LogError($"[Roomies Restyle] {point.name} is blocked by {string.Join(",", hits)}");
            }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Roomies Restyle] Room built in {scene.name}: {root.GetComponentsInChildren<Renderer>().Length} renderers, {root.GetComponentsInChildren<Light>().Length} lights. Scene saved.");
    }

    /// <summary>タイトルの部屋だけに置く物。ベッドと冷蔵庫は見た目だけ（スクリプトなし）、キャラの顔を照らす明かり。</summary>
    static void TitleExtras(Scene scene, Transform root)
    {
        var g = Group(root, "Title extras");
        for (int i = 0; i < 5; i++) BedParts(Group(g, "Bed look " + (i + 1), V(14.3f, F + .03f, 15.2f + i * 1.55f), 90));
        var fridge = Group(g, "Fridge look", V(21.8f, F + .03f, 22.9f));
        FridgeBodyParts(fridge);
        FridgeDoorParts(Group(fridge, "Door", V(-.38f, .9f, -.3f)));
        Lamp(g, "Title key light", V(19.9f, F + 1.9f, 18.7f), new Color(1f, .82f, .62f), 5.5f, 7f);
        Lamp(g, "Title rim light", V(15.0f, F + 2.0f, 14.6f), new Color(.7f, .45f, 1f), 3.2f, 5f);
        var camera = scene.GetRootGameObjects().Select(o => o.GetComponent<UniversalAdditionalCameraData>()).FirstOrDefault(c => c != null);
        if (camera != null) { camera.renderPostProcessing = true; EditorUtility.SetDirty(camera); }
        // 空を本編の夜と同じものにする（値は MainMenuRoomShowcase のインスペクターで直せる）
        var showcase = scene.GetRootGameObjects().Select(o => o.GetComponent<MainMenuRoomShowcase>()).FirstOrDefault(c => c != null);
        var nightSky = AssetDatabase.LoadAssetAtPath<Material>(TitleSkyPath);
        if (showcase != null && nightSky != null)
        {
            var so = new SerializedObject(showcase);
            so.FindProperty("skyboxMaterial").objectReferenceValue = nightSky;
            // 構図：入口側から、こたつとソファのまわりのキャラを写す（右寄せ。左はタイトルとボタンの場所）
            so.FindProperty("cameraAnchor").vector3Value = V(20.6f, 5.0f, 15.6f);
            so.FindProperty("fieldOfView").floatValue = 52f;
            so.FindProperty("subjectEyeHeight").floatValue = -.35f;
            so.FindProperty("frameBias").floatValue = 1.6f;
            var roomies = so.FindProperty("characters");
            Vector3[] spots = { V(18.75f, 4.18f, 19.1f), V(15.35f, 4.18f, 19.0f), V(18.5f, 4.18f, 21.5f) };
            float[] yaws = { 150f, 118f, 158f };
            for (int i = 0; i < roomies.arraySize && i < spots.Length; i++)
            {
                var t = roomies.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (t == null) continue;
                Undo.RecordObject(t, "Place title roomies");
                t.position = spots[i]; t.rotation = Quaternion.Euler(0, yaws[i], 0);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ================================================================ 色と素材
    static void Palette()
    {
        var planks = Texture("floor_planks", 512, (u, v) =>
        {
            float fu = u * 6; int plank = Mathf.FloorToInt(fu); float inPlank = fu - plank;
            float tone = .78f + .22f * Mathf.PerlinNoise(plank * 7.31f, .5f);
            float offset = Mathf.PerlinNoise(plank * 3.7f, 9.1f);
            float vv = Mathf.Repeat(v + offset, 1);
            float board = Mathf.Floor(vv * 2);
            tone *= .93f + .07f * Mathf.PerlinNoise(plank * 5.1f + board * 2.3f, 3.3f);
            float grain = Noise(u, v, 90, 3) * .5f + Mathf.PerlinNoise(u * 300, v * 9) * .5f;
            tone *= .9f + .12f * grain;
            float seam = Mathf.Min(inPlank, 1 - inPlank) * 512f / 6f;
            float joint = Mathf.Min(Mathf.Repeat(vv * 2, 1), 1 - Mathf.Repeat(vv * 2, 1)) * 256f;
            if (seam < 1.6f || joint < 1.2f) tone *= .52f;
            float dirt = Mathf.SmoothStep(.55f, .8f, Noise(u, v, 5, 11));
            tone *= 1 - .16f * dirt;
            return new Color(tone, tone * .97f, tone * .93f, 1);
        });
        var plaster = Texture("wall_plaster", 256, (u, v) =>
        {
            float t = .9f + .1f * Noise(u, v, 6, 2) - .06f * Mathf.SmoothStep(.6f, .85f, Noise(u, v, 13, 7));
            t -= .05f * Mathf.PerlinNoise(u * 120, v * 120);
            return new Color(t, t, t, 1);
        });
        var corrugated = Texture("cardboard", 256, (u, v) =>
        {
            float t = .9f + .08f * Noise(u, v, 8, 4) + .035f * Mathf.Sin(v * 256 * .9f);
            return new Color(t, t, t, 1);
        });
        var weave = Texture("fabric", 256, (u, v) =>
        {
            float t = .86f + .1f * Mathf.PerlinNoise(u * 150, v * 150) + .06f * Noise(u, v, 7, 5);
            return new Color(t, t, t, 1);
        });

        floorMat = Mat("floor_planks", "8A5A3C", .32f, 0, null, 1, planks, new Vector2(7, 7));
        wallMat = Mat("wall_plaster", "B8A98E", .08f, 0, null, 1, plaster, new Vector2(2, 1));
        ceilingMat = Mat("ceiling", "4A4038", .05f);
        rugMat = Mat("rug", "5E5270", .04f, 0, null, 1, weave, new Vector2(4, 4));
        rugEdge = Mat("rug_edge", "C9B58F", .04f, 0, null, 1, weave, new Vector2(4, 4));
        sofaY = Mat("sofa_yellow", "C9A132", .1f, 0, null, 1, weave, new Vector2(2, 2));
        sofaYDark = Mat("sofa_yellow_dark", "A9841F", .1f, 0, null, 1, weave, new Vector2(2, 2));
        fabricGrey = Mat("fabric_grey", "3B3D46", .08f, 0, null, 1, weave, new Vector2(2, 2));
        woodDark = Mat("wood_dark", "4E2E20", .3f); woodMid = Mat("wood_mid", "7A4428", .34f); woodLight = Mat("wood_light", "A9774B", .28f);
        metalDark = Mat("metal_dark", "2A2C33", .5f, .7f); metalGrey = Mat("metal_grey", "9097A0", .6f, .8f);
        black = Mat("plastic_black", "17181C", .45f); white = Mat("plastic_white", "E6E1D6", .4f);
        paper = Mat("paper", "EEE7D6", .05f); cream = Mat("cream", "E3D3B0", .12f);
        cardboard = Mat("cardboard", "B4864F", .06f, 0, null, 1, corrugated); cardboardDark = Mat("cardboard_dark", "8E6A3E", .06f, 0, null, 1, corrugated);
        purple = Mat("purple", "7A4CC4", .25f); purpleDark = Mat("purple_dark", "46306F", .2f);
        curtain = Mat("curtain", "5A3E8A", .05f, 0, null, 1, weave, new Vector2(1, 3));
        red = Mat("red", "C4453A", .3f); orange = Mat("orange", "D8822C", .3f); yellow = Mat("yellow", "E6BE36", .3f);
        green = Mat("green", "6DAA48", .3f); teal = Mat("teal", "3C8A86", .3f); blue = Mat("blue", "3A5FB4", .3f);
        pink = Mat("pink", "D6789A", .25f); brown = Mat("brown", "6A4630", .1f, 0, null, 1, weave);
        sharkBlue = Mat("shark_blue", "4C6C94", .12f, 0, null, 1, weave); sharkBelly = Mat("shark_belly", "D8DDE2", .12f, 0, null, 1, weave);
        leaf = Mat("leaf", "3E7C3A", .35f, 0, null, 1, null, null, true); leafDark = Mat("leaf_dark", "2C5A2E", .35f, 0, null, 1, null, null, true);
        pot = Mat("pot", "B2623A", .2f); gold = Mat("gold", "D9A62E", .7f, .9f);
        glassBrown = Mat("glass_brown", "4A2A16", .85f, .1f); cheese = Mat("cheese", "E2A93B", .25f); crust = Mat("crust", "C98A4A", .15f);
        lampShade = Mat("lamp_shade", "24262C", .45f, .6f, null, 1, null, null, true);

        bulb = Mat("glow_bulb", "FFE3B0", .2f, 0, "FFC06A", 9);
        fairy = Mat("glow_fairy", "FFE0A0", .2f, 0, "FFB45A", 5);
        neon = Mat("glow_neon_purple", "D9A8FF", .2f, 0, "A14DFF", 7);
        neonPink = Mat("glow_neon_pink", "FFB0E0", .2f, 0, "FF4FB4", 6);
        screenGreen = Mat("glow_screen_green", "26331F", .6f, 0, "3D5A2C", .9f);
        screenWhite = Mat("glow_screen_white", "F2F0E6", .4f, 0, "F2EEDC", 1.6f);
        barLime = Mat("glow_bar_lime", "9BE04A", .3f, 0, "8CE23C", 2.6f);
        ledGreen = Mat("glow_led_green", "7CFF8A", .3f, 0, "55FF6A", 4);
        tvScreen = Mat("glow_tv", "1C2C4C", .7f, 0, "3D6FD0", 1.5f);
        slotYellow = Mat("glow_slot_yellow", "FFC62E", .4f, 0, "FFB21E", 3);
        slotRed = Mat("glow_slot_red", "FF4030", .4f, 0, "FF2A1C", 3.5f);
        windowGlow = Mat("glow_window", "FFD88A", .2f, 0, "FFC062", 2.2f);
        water = Mat("glow_water", "38B4E0", .9f, 0, "1E8FC8", 1.3f, null, null, false, .55f);
        canColors = new[] { blue, red, yellow, green, white, purple, orange, teal };
        glowPurple = TextGlow("text_neon_purple", "B45CFF", 5.5f);
        glowRed = TextGlow("text_glow_red", "FF3A2A", 3.2f);
        glowWhite = TextGlow("text_glow_white", "FFFFFF", 1.8f);
    }

    // ================================================================ 元の部屋を整える（消さない）
    static void RestyleExistingHouse(Transform house, Transform root)
    {
        if (house == null) { Debug.LogWarning("[Roomies Restyle] House group not found; existing props left as they are."); return; }
        var hide = new HashSet<string> { "sofa", "coffee_table", "magazine_stack", "tissue_box", "mug", "drink_can", "Woven rug", "Rug border",
            "Ceiling diffuser", "Warm ceiling light", "Floorboard seam", "Curtain", "comfy_bed" };
        var walls = new HashSet<string> { "Sill wall", "Top wall", "Wall pier", "Front left wall", "Front right pier" };
        var curtains = Group(root, "Curtains");
        int hidden = 0;
        foreach (var t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t == house) continue;
            if (hide.Contains(t.name))
            {
                if (t.name == "Curtain") NewCurtain(curtains, t);
                if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden++; }
                continue;
            }
            var r = t.GetComponent<MeshRenderer>();
            if (r == null) continue;
            if (t.name == "Oak floor") r.sharedMaterial = floorMat;
            else if (walls.Contains(t.name)) r.sharedMaterial = wallMat;
        }
        Debug.Log($"[Roomies Restyle] Hid {hidden} original lounge objects (kept in the scene, inactive).");
    }

    static void NewCurtain(Transform parent, Transform old)
    {
        // 元のカーテンの窓の位置に、部屋の内側へ向けて布のひだを並べる
        Vector3 wallPoint = old.parent.position; Vector3 along = old.parent.right;
        Vector3 normal = Vector3.Cross(Vector3.up, along).normalized;
        if (Vector3.Dot(RoomCenter - wallPoint, normal) < 0) normal = -normal;
        float t = Vector3.Dot(old.position - wallPoint, along);
        Vector3 pos = wallPoint + along * t + normal * .15f; pos.y = F;
        var g = Group(parent, "Curtain", pos, Quaternion.LookRotation(normal).eulerAngles.y);
        Box(g, "Rail", V(0, 2.26f, 0), V(.62f, .025f, .03f), metalDark);
        for (int i = 0; i < 6; i++)
            RBox(g, "Fold", V(-.22f + i * .088f, 1.6f, i % 2 == 0 ? 0 : .03f), V(.1f, 1.3f, .045f), .02f, curtain);
    }

    // ================================================================ 天井・ラグ
    static void Shell(Transform root)
    {
        var g = Group(root, "Shell");
        Box(g, "Ceiling liner", V(17.93f, Ceiling - .008f, 18.68f), V(9.5f, .012f, 9.5f), ceilingMat, default, false);
        var rug = Group(g, "Rug", V(16.5f, F, 19.75f));
        Box(rug, "Rug", V(0, .006f, 0), V(3.5f, .012f, 2.8f), rugMat, default, false);
        Box(rug, "Rug band", V(0, .0125f, 1.22f), V(3.3f, .003f, .07f), rugEdge, default, false);
        Box(rug, "Rug band", V(0, .0125f, -1.22f), V(3.3f, .003f, .07f), rugEdge, default, false);
        Box(rug, "Rug band", V(1.57f, .0125f, 0), V(.07f, .003f, 2.5f), rugEdge, default, false);
        Box(rug, "Rug band", V(-1.57f, .0125f, 0), V(.07f, .003f, 2.5f), rugEdge, default, false);
    }

    // ================================================================ ソファとこたつ
    static void Lounge(Transform root)
    {
        var g = Group(root, "Lounge");
        var sofa = Group(g, "Sofa", V(16.5f, F, 21.3f), 180);
        RBox(sofa, "Base", V(0, .25f, 0), V(2.3f, .3f, .95f), .06f, sofaYDark);
        RBox(sofa, "Back", V(0, .58f, -.36f), V(2.3f, .62f, .24f), .08f, sofaY);
        foreach (int s in new[] { -1, 1 }) RBox(sofa, "Arm", V(s * 1.03f, .46f, 0), V(.25f, .56f, .95f), .09f, sofaY);
        for (int i = 0; i < 3; i++)
        {
            RBox(sofa, "Seat cushion", V(-.6f + i * .6f, .47f, .08f), V(.58f, .17f, .68f), .07f, sofaY, V(0, 0, i == 1 ? 1.5f : -1));
            RBox(sofa, "Back cushion", V(-.6f + i * .6f, .76f, -.2f), V(.56f, .42f, .17f), .08f, sofaY, V(-12, 0, i == 0 ? 3 : -2));
        }
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) Cyl(sofa, "Leg", V(sx * 1.02f, .05f, sz * .36f), .06f, .1f, woodDark);
        RBox(sofa, "Throw", V(1.03f, .755f, .05f), V(.3f, .035f, .7f), .016f, brown, V(0, 0, 0));
        RBox(sofa, "Throw fall", V(1.175f, .55f, .05f), V(.035f, .4f, .7f), .016f, brown);
        RBox(sofa, "Pillow", V(.7f, .64f, .02f), V(.34f, .34f, .12f), .055f, Mat("pillow_teal", "3E7F7A", .08f), V(-16, -18, -6));
        RBox(sofa, "Pillow", V(-.78f, .66f, .0f), V(.36f, .36f, .13f), .06f, purple, V(-18, 20, 8));
        Solid(sofa, V(0, .42f, 0), V(2.3f, .84f, .95f));

        var table = Group(g, "Low table", V(16.5f, F, 19.85f));
        RBox(table, "Top", V(0, .44f, 0), V(1.55f, .07f, 1.0f), .02f, woodMid);
        Box(table, "Apron", V(0, .385f, 0), V(1.42f, .05f, .88f), woodDark);
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) Box(table, "Leg", V(sx * .68f, .2f, sz * .41f), V(.09f, .4f, .09f), woodDark);
        Box(table, "Lower shelf", V(0, .14f, 0), V(1.3f, .03f, .76f), woodDark);
        Solid(table, V(0, .24f, 0), V(1.55f, .48f, 1.0f));
        float top = .476f;
        PizzaBox(table, V(-.3f, top, .12f), 14, true);
        SnackBox(table, V(.2f, top, -.05f), -20, red, yellow);
        SnackBox(table, V(.42f, top, -.3f), 35, green, white);
        Can(table, V(-.62f, top, -.28f), 0, false); Can(table, V(-.05f, top, -.33f), 3, false); Can(table, V(.12f, top, .3f), 2, false);
        Can(table, V(.58f, top, .16f), 1, false); Can(table, V(.03f, top, .14f), 5, false); Can(table, V(.5f, top, -.02f), 6, true, 60);
        Bottle(table, V(.3f, top, .24f), 1.25f);
        CupNoodle(table, V(-.58f, top, .34f));
        Bowl(table, V(.62f, top, .36f));
        Box(table, "Remote", V(-.2f, top + .01f, -.4f), V(.05f, .02f, .2f), black, V(0, 70, 0));
        Controller(table, V(.33f, top, -.36f), -25, white);
        Magazine(table, V(0, .158f, .1f), 8, blue); Magazine(table, V(.05f, .171f, .08f), -12, red); Magazine(table, V(-.45f, .158f, -.1f), 30, yellow);
        Box(table, "Game box", V(.45f, .19f, -.05f), V(.3f, .06f, .22f), purpleDark, V(0, 12, 0));
    }

    // ================================================================ テレビ
    static void TvCorner(Transform root)
    {
        var tv = Group(root, "TV corner", V(16.4f, F, 17.75f));
        RBox(tv, "Stand", V(0, .21f, 0), V(1.5f, .42f, .4f), .015f, woodDark);
        Box(tv, "Stand shelf gap", V(0, .2f, .201f), V(1.36f, .16f, .01f), black);
        Box(tv, "TV foot", V(0, .44f, 0), V(.5f, .03f, .2f), black);
        Box(tv, "TV neck", V(0, .5f, -.02f), V(.1f, .1f, .04f), black);
        RBox(tv, "TV", V(0, .92f, 0), V(1.3f, .76f, .06f), .015f, black);
        Plane(tv, "TV screen", V(0, .93f, .032f), new Vector2(1.22f, .68f), tvScreen);
        Plane(tv, "TV game sky", V(0, 1.06f, .033f), new Vector2(1.22f, .3f), Mat("glow_tv_sky", "5A8AD8", .6f, 0, "4F8CE8", .7f));
        Plane(tv, "TV game ground", V(0, .7f, .033f), new Vector2(1.22f, .22f), Mat("glow_tv_ground", "3E8A4A", .6f, 0, "3FA050", .6f));
        Plane(tv, "TV game kart", V(-.1f, .78f, .034f), new Vector2(.16f, .1f), slotRed);
        Text(tv, "2位 / 4人", V(.4f, 1.17f, .035f), new Vector2(.36f, .1f), Color.white, 1.2f, default, glowWhite);
        RBox(tv, "Console", V(.45f, .45f, .05f), V(.3f, .06f, .22f), .012f, white);
        Box(tv, "Console light", V(.34f, .45f, .161f), V(.03f, .008f, .002f), ledGreen);
        Box(tv, "Game cases", V(-.48f, .47f, .03f), V(.16f, .1f, .2f), blue, V(0, 9, 0));
        Box(tv, "Game cases", V(-.5f, .535f, .03f), V(.15f, .03f, .2f), red, V(0, -14, 0));
        Tube(tv, "Controller cable", "tv_cable", new[] { V(.45f, .43f, .16f), V(.5f, .1f, .3f), V(.3f, .012f, .6f), V(-.1f, .012f, .9f), V(-.25f, .012f, 1.25f) }, .006f, black);
        Controller(tv, V(-.27f, 0, 1.3f), 150, black);
        Solid(tv, V(0, .65f, 0), V(1.5f, 1.3f, .4f));
    }

    // ================================================================ パソコン机（東の壁）
    static void Desk(Transform root)
    {
        var d = Group(root, "PC desk", V(22.33f, F, 16.9f), -90);
        RBox(d, "Top", V(0, .74f, 0), V(1.5f, .05f, .7f), .012f, woodLight);
        foreach (int s in new[] { -1, 1 }) Box(d, "Side", V(s * .72f, .36f, 0), V(.04f, .72f, .64f), woodMid);
        Box(d, "Back panel", V(0, .45f, -.32f), V(1.4f, .5f, .02f), woodMid);
        RBox(d, "Drawer unit", V(.47f, .36f, .02f), V(.42f, .68f, .58f), .01f, woodMid);
        for (int i = 0; i < 3; i++) { Box(d, "Drawer front", V(.47f, .16f + i * .21f, .312f), V(.38f, .18f, .01f), woodLight); Box(d, "Drawer pull", V(.47f, .16f + i * .21f, .322f), V(.1f, .015f, .012f), metalDark); }
        // モニター
        Cyl(d, "Monitor base", V(-.1f, .775f, -.16f), .2f, .015f, black);
        Box(d, "Monitor neck", V(-.1f, .86f, -.18f), V(.04f, .18f, .03f), black);
        RBox(d, "Monitor", V(-.1f, 1.08f, -.15f), V(.72f, .44f, .035f), .01f, black);
        Plane(d, "Screen", V(-.1f, 1.085f, -.131f), new Vector2(.67f, .385f), screenGreen);
        Text(d, "バイト中…", V(-.1f, 1.17f, -.129f), new Vector2(.5f, .09f), Color.white, 1.1f, default, glowWhite);
        Plane(d, "Progress track", V(-.1f, 1.075f, -.1295f), new Vector2(.5f, .04f), black);
        Plane(d, "Progress fill", V(-.02f, 1.075f, -.129f), new Vector2(.34f, .028f), barLime);
        Text(d, "稼ぎ：¥18,000", V(-.1f, .99f, -.129f), new Vector2(.5f, .06f), new Color(1, .95f, .7f), .7f, default, glowWhite);
        RBox(d, "Keyboard", V(-.12f, .775f, .12f), V(.44f, .02f, .15f), .008f, black);
        RBox(d, "Mouse", V(.2f, .78f, .14f), V(.06f, .03f, .1f), .014f, black);
        Box(d, "Mouse pad", V(.2f, .767f, .14f), V(.22f, .004f, .18f), purpleDark);
        CupNoodle(d, V(.42f, .766f, .2f)); Can(d, V(.6f, .766f, .05f), 0, false); Can(d, V(.52f, .766f, -.12f), 3, false); Can(d, V(-.55f, .766f, .2f), 5, true, 20);
        RBox(d, "PC tower", V(-.52f, .25f, -.05f), V(.2f, .44f, .46f), .012f, black);
        Box(d, "PC light", V(-.52f, .4f, .182f), V(.012f, .1f, .003f), neon);
        Box(d, "Sticky note", V(.2f, 1.2f, -.13f), V(.07f, .07f, .002f), yellow, V(0, 0, 6));
        Box(d, "Sticky note", V(.22f, 1.11f, -.13f), V(.07f, .07f, .002f), pink, V(0, 0, -5));
        Solid(d, V(0, .39f, 0), V(1.5f, .78f, .7f));
        // いす（当たり判定なし）
        var chair = Group(d, "Office chair", V(-.12f, 0, .78f), 22);
        for (int i = 0; i < 5; i++) Box(chair, "Base leg", V(Mathf.Sin(i * 72 * Mathf.Deg2Rad) * .15f, .05f, Mathf.Cos(i * 72 * Mathf.Deg2Rad) * .15f), V(.045f, .03f, .32f), metalDark, V(0, i * 72, 0));
        for (int i = 0; i < 5; i++) Ball(chair, "Caster", V(Mathf.Sin(i * 72 * Mathf.Deg2Rad) * .29f, .03f, Mathf.Cos(i * 72 * Mathf.Deg2Rad) * .29f), V(.06f, .06f, .06f), black);
        Cyl(chair, "Pole", V(0, .25f, 0), .05f, .38f, metalGrey);
        RBox(chair, "Seat", V(0, .48f, 0), V(.5f, .09f, .48f), .04f, fabricGrey);
        RBox(chair, "Back", V(0, .86f, .24f), V(.46f, .56f, .08f), .04f, fabricGrey, V(8, 0, 0));
        Box(chair, "Back post", V(0, .6f, .25f), V(.06f, .26f, .03f), metalDark, V(8, 0, 0));
        foreach (int s in new[] { -1, 1 }) { Box(chair, "Arm post", V(s * .27f, .58f, .05f), V(.03f, .2f, .04f), metalDark); RBox(chair, "Arm rest", V(s * .27f, .69f, 0), V(.06f, .03f, .26f), .012f, black); }
        RBox(chair, "Hoodie", V(.02f, .98f, .285f), V(.5f, .34f, .035f), .016f, purpleDark, V(8, 0, -3));
        RBox(chair, "Hoodie fold", V(.02f, 1.13f, .255f), V(.5f, .05f, .12f), .022f, purpleDark, V(8, 0, -3));
    }

    // ================================================================ 棚（東の壁）
    static void Shelf(Transform root)
    {
        var s = Group(root, "Shelf", V(22.46f, F, 19.35f), -90);
        const float w = 1.9f, h = 2.05f, dp = .44f;
        foreach (int side in new[] { -1, 1 }) Box(s, "Upright", V(side * (w * .5f - .02f), h * .5f, 0), V(.04f, h, dp), woodDark);
        Box(s, "Divider", V(.05f, h * .5f, 0), V(.03f, h, dp - .02f), woodDark);
        Box(s, "Back", V(0, h * .5f, -dp * .5f + .01f), V(w, h, .02f), Mat("wood_back", "3A2218", .2f));
        float[] levels = { .06f, .52f, .98f, 1.44f, 2.03f };
        foreach (float y in levels) Box(s, "Board", V(0, y, 0), V(w, .035f, dp), woodMid);
        // 上の段：パチスロ台
        var slot = Group(s, "Slot toy", V(.5f, 1.458f, .02f));
        RBox(slot, "Body", V(0, .26f, 0), V(.5f, .52f, .34f), .03f, Mat("slot_body", "232733", .5f, .3f));
        RBox(slot, "Top sign", V(0, .5f, .02f), V(.44f, .1f, .32f), .03f, slotYellow);
        Plane(slot, "Reel window", V(0, .31f, .172f), new Vector2(.36f, .17f), screenWhite);
        Text(slot, "7 7 7", V(0, .31f, .174f), new Vector2(.34f, .15f), Color.white, 2.2f, default, glowRed);
        Box(slot, "Panel", V(0, .13f, .16f), V(.42f, .1f, .06f), Mat("slot_panel", "8A1E22", .4f), V(-25, 0, 0));
        foreach (float x in new[] { -.12f, 0, .12f }) Cyl(slot, "Button", V(x, .15f, .19f), .045f, .02f, slotYellow, V(65, 0, 0));
        Cyl(slot, "Lever", V(.28f, .3f, .05f), .018f, .2f, metalGrey); Ball(slot, "Lever knob", V(.28f, .42f, .05f), V(.05f, .05f, .05f), slotRed);
        // 中身いろいろ
        Plush(s, V(-.55f, 1.458f, 0), purple, .2f); Plush(s, V(-.25f, 1.458f, .03f), Mat("plush_grey", "8E93A8", .1f), .16f);
        for (int i = 0; i < 4; i++) RBox(s, "Box", V(-.75f + i * .2f, .998f + .09f, .02f), V(.17f, .18f, .28f), .01f, canColors[(i * 3 + 1) % canColors.Length], V(0, (i * 37 % 20) - 10, 0));
        RBox(s, "Box", V(-.45f, .998f + .225f, .02f), V(.3f, .09f, .26f), .01f, yellow, V(0, 7, 0));
        Books(s, V(.2f, .998f, 0), .7f, 9);
        for (int i = 0; i < 3; i++) Bottle(s, V(.2f + i * .09f, .538f, .08f), 1.1f);
        RBox(s, "Game console box", V(.62f, .538f + .1f, 0), V(.42f, .2f, .3f), .01f, blue, V(0, -6, 0));
        RBox(s, "Storage bin", V(-.5f, .538f + .13f, 0), V(.7f, .26f, .36f), .02f, purpleDark);
        Box(s, "Bin label", V(-.5f, .538f + .13f, .181f), V(.2f, .08f, .002f), paper);
        RBox(s, "Storage bin", V(-.5f, .078f + .17f, 0), V(.74f, .34f, .36f), .02f, Mat("bin_blue", "34466E", .2f));
        RBox(s, "Shoe box", V(.4f, .078f + .07f, 0), V(.34f, .14f, .3f), .01f, cardboard, V(0, 5, 0));
        RBox(s, "Shoe box", V(.42f, .078f + .2f, 0), V(.32f, .12f, .28f), .01f, red, V(0, -8, 0));
        RBox(s, "Toolbox", V(.77f, .078f + .09f, 0), V(.22f, .18f, .3f), .02f, teal);
        // 棚の上
        LuckyCat(s, V(-.6f, 2.048f, .02f), 0);
        Bottle(s, V(-.2f, 2.048f, 0), 1.3f); Bottle(s, V(-.1f, 2.048f, .06f), 1.15f);
        RBox(s, "Box on top", V(.45f, 2.048f + .1f, 0), V(.4f, .2f, .32f), .01f, teal, V(0, 6, 0));
        RBox(s, "Box on top", V(.4f, 2.048f + .27f, 0), V(.3f, .14f, .26f), .01f, cardboard, V(0, -10, 0));
        Solid(s, V(0, h * .5f, 0), V(w, h, dp));
    }

    // ================================================================ 台所（北の壁）
    static void Kitchen(Transform root)
    {
        var k = Group(root, "Kitchen counter", V(17.8f, F, 23.17f), 180);
        RBox(k, "Cabinet", V(0, .43f, 0), V(1.8f, .84f, .52f), .01f, Mat("cabinet_green", "5D7A52", .25f));
        RBox(k, "Worktop", V(0, .87f, .01f), V(1.86f, .05f, .58f), .012f, Mat("worktop", "CFC6B4", .45f));
        for (int i = 0; i < 4; i++) { Box(k, "Door", V(-.675f + i * .45f, .44f, .262f), V(.42f, .74f, .012f), Mat("cabinet_green_light", "6C8A5E", .25f)); Box(k, "Handle", V(-.675f + i * .45f + (i % 2 == 0 ? .15f : -.15f), .62f, .272f), V(.02f, .12f, .014f), metalGrey); }
        Box(k, "Kick board", V(0, .04f, .22f), V(1.8f, .08f, .02f), black);
        // 水槽
        var tank = Group(k, "Aquarium", V(.5f, .895f, -.02f));
        Box(tank, "Base", V(0, .015f, 0), V(.66f, .03f, .32f), black);
        Box(tank, "Gravel", V(0, .05f, 0), V(.62f, .04f, .28f), Mat("gravel", "C9B48A", .1f));
        Box(tank, "Water", V(0, .22f, 0), V(.62f, .3f, .28f), water, default, false);
        Box(tank, "Lid", V(0, .385f, 0), V(.66f, .03f, .32f), black);
        Box(tank, "Lid light", V(0, .368f, 0), V(.5f, .004f, .12f), Mat("glow_tank_light", "DFF6FF", .3f, 0, "BFEFFF", 3));
        for (int i = 0; i < 5; i++) Ball(tank, "Water plant", V(-.2f + i * .04f, .13f + (i % 2) * .04f, -.05f + (i % 3) * .03f), V(.03f, .16f + i % 3 * .05f, .012f), leaf, V(0, i * 40, (i - 2) * 9));
        Ball(tank, "Fish", V(.1f, .22f, .02f), V(.07f, .035f, .02f), orange); Ball(tank, "Fish", V(-.02f, .27f, -.04f), V(.06f, .03f, .018f), orange, V(0, 30, 0));
        Ball(tank, "Fish", V(.2f, .15f, -.03f), V(.05f, .026f, .016f), slotYellow, V(0, -20, 0));
        // 電子レンジ
        var mw = Group(k, "Microwave", V(-.48f, .895f, 0), 6);
        RBox(mw, "Body", V(0, .16f, 0), V(.52f, .3f, .38f), .02f, white);
        Plane(mw, "Door glass", V(-.07f, .16f, .192f), new Vector2(.33f, .22f), black);
        Box(mw, "Door handle", V(.1f, .16f, .2f), V(.02f, .22f, .02f), metalGrey);
        Box(mw, "Panel", V(.2f, .16f, .191f), V(.1f, .24f, .003f), Mat("mw_panel", "CFC9BC", .4f));
        Plane(mw, "Clock", V(.2f, .24f, .194f), new Vector2(.07f, .03f), barLime);
        foreach (float y in new[] { .16f, .1f }) Cyl(mw, "Dial", V(.2f, y, .198f), .04f, .012f, metalDark, V(90, 0, 0));
        Bottle(k, V(.02f, .895f, .1f), 1.25f); Bottle(k, V(.11f, .895f, .05f), 1.1f); Bottle(k, V(-.08f, .895f, .02f), 1.2f);
        Can(k, V(.05f, .895f, -.12f), 2, false); Can(k, V(.88f, .895f, .15f), 4, false);
        CupNoodle(k, V(-.84f, .895f, .15f)); CupNoodle(k, V(-.84f, .895f + .1f, .15f));
        Solid(k, V(0, .45f, 0), V(1.86f, .9f, .58f));

        // 窓のネオン（ねこ「うえい」）
        var sign = Group(root, "Neon cat sign", V(17.35f, F + 1.72f, 23.33f), 180);
        Vector3 P(float x, float y) => V(x, y, 0);
        Tube(sign, "Cat head", "neon_cat_head", new[] { P(-.19f, -.13f), P(-.235f, 0), P(-.22f, .12f), P(-.215f, .27f), P(-.11f, .17f), P(0, .185f), P(.11f, .17f), P(.215f, .27f), P(.22f, .12f), P(.235f, 0), P(.19f, -.13f), P(.08f, -.185f), P(-.08f, -.185f) }, .011f, neon, true, default, default, false);
        foreach (int sx in new[] { -1, 1 })
        {
            Tube(sign, "Cat eye", "neon_eye" + sx, new[] { P(sx * .13f, .055f), P(sx * .1f, .085f), P(sx * .07f, .055f) }, .01f, neon, false, default, default, false);
            Tube(sign, "Whisker", "neon_wh_a" + sx, new[] { P(sx * .15f, -.035f), P(sx * .3f, -.01f) }, .008f, neonPink, false, default, default, false);
            Tube(sign, "Whisker", "neon_wh_b" + sx, new[] { P(sx * .15f, -.07f), P(sx * .3f, -.095f) }, .008f, neonPink, false, default, default, false);
        }
        Tube(sign, "Cat mouth", "neon_mouth", new[] { P(-.06f, -.06f), P(-.03f, -.095f), P(0, -.06f), P(.03f, -.095f), P(.06f, -.06f) }, .009f, neonPink, false, default, default, false);
        Ball(sign, "Cat nose", V(0, -.035f, 0), V(.03f, .022f, .022f), neonPink);
        Text(sign, "うえい", V(0, -.31f, 0), new Vector2(.5f, .17f), Color.white, 2.4f, default, glowPurple);
        Box(sign, "Back bar", V(0, .31f, -.02f), V(.6f, .02f, .02f), black);
        foreach (int sx in new[] { -1, 1 }) Box(sign, "Hanging wire", V(sx * .25f, .42f, -.02f), V(.004f, .22f, .004f), black);
        Box(sign, "Power cable", V(.28f, -.5f, -.02f), V(.006f, .9f, .006f), black);

        // ブレーカーと冷蔵庫のあいだ：ごみ袋とケース
        var corner = Group(root, "Kitchen corner", V(20.55f, F, 23.0f));
        TrashBag(corner, V(0, 0, 0), white, .34f); TrashBag(corner, V(.42f, 0, .1f), Mat("bag_blue", "5E7FA8", .4f), .28f); TrashBag(corner, V(.15f, 0, -.3f), white, .26f);
        RBox(corner, "Drink crate", V(-.48f, .13f, .12f), V(.38f, .26f, .28f), .015f, yellow);
        for (int i = 0; i < 6; i++) Bottle(corner, V(-.6f + (i % 3) * .12f, .2f, .05f + (i / 3) * .13f), 1.1f);
    }

    // ================================================================ 南の壁（ダーツ・ゴール・貼り紙）
    static void SouthWall(Transform root)
    {
        var w = Group(root, "South wall decor", V(0, F, 13.915f));
        // ダーツ
        var dart = Group(w, "Dartboard", V(17.75f, 1.6f, .02f));
        Cyl(dart, "Rim", V(0, 0, 0), .5f, .03f, black, V(90, 0, 0));
        Cyl(dart, "Face", V(0, 0, .003f), .44f, .03f, cream, V(90, 0, 0));
        for (int i = 0; i < 10; i++) Prism(dart, "Wedge", "dart_wedge", new Vector2(0, 0), new Vector2(-.034f, .215f), new Vector2(.034f, .215f), .004f, V(0, 0, .019f), i % 2 == 0 ? black : Mat("dart_dark", "2F3A2E", .2f), V(0, 0, i * 36));
        Tube(dart, "Double ring", "dart_ring_a", Circle(.2f, 20), .008f, red, true, V(0, 0, .02f));
        Tube(dart, "Triple ring", "dart_ring_b", Circle(.12f, 16), .008f, green, true, V(0, 0, .02f));
        Cyl(dart, "Bull", V(0, 0, .008f), .06f, .03f, green, V(90, 0, 0)); Cyl(dart, "Bullseye", V(0, 0, .01f), .028f, .03f, red, V(90, 0, 0));
        Dart(dart, V(.03f, .05f, .02f), red, V(8, -6, 0)); Dart(dart, V(-.11f, -.07f, .02f), yellow, V(-10, 9, 0)); Dart(dart, V(.3f, -.2f, .0f), blue, V(20, 14, 0));
        // バスケのゴール
        var hoop = Group(w, "Basket hoop", V(16.0f, 2.02f, .02f));
        RBox(hoop, "Backboard", V(0, 0, 0), V(.78f, .54f, .03f), .01f, white);
        foreach (int s in new[] { -1, 1 }) { Box(hoop, "Board edge", V(s * .37f, 0, .017f), V(.03f, .52f, .003f), purple); Box(hoop, "Board edge", V(0, s * .25f, .017f), V(.76f, .03f, .003f), purple); }
        foreach (int s in new[] { -1, 1 }) { Box(hoop, "Target", V(s * .12f, -.1f, .017f), V(.02f, .2f, .003f), red); Box(hoop, "Target", V(0, -.1f + s * .1f, .017f), V(.26f, .02f, .003f), red); }
        Box(hoop, "Bracket", V(0, -.19f, .06f), V(.1f, .04f, .1f), orange);
        Tube(hoop, "Rim", "hoop_rim", Circle(.17f, 18).Select(p => V(p.x, 0, p.y)).ToArray(), .012f, orange, true, V(0, -.19f, .27f));
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36 * Mathf.Deg2Rad, b = (i + .5f) * 36 * Mathf.Deg2Rad;
            Tube(hoop, "Net", "hoop_net" + i, new[] { V(Mathf.Cos(a) * .17f, 0, Mathf.Sin(a) * .17f), V(Mathf.Cos(b) * .13f, -.12f, Mathf.Sin(b) * .13f), V(Mathf.Cos(a) * .1f, -.26f, Mathf.Sin(a) * .1f) }, .004f, paper, false, V(0, -.19f, .27f));
        }
        // 貼り紙「今週の目標」
        var goals = Group(w, "Weekly goals", V(18.72f, 1.52f, .012f));
        Box(goals, "Paper", V(0, 0, 0), V(.62f, .82f, .004f), paper, V(0, 0, -2));
        Text(goals, "今週の目標", V(0, .3f, .004f), new Vector2(.5f, .11f), Hex("3A5FB4"), 1.5f, V(0, 0, -2));
        Text(goals, "・家賃を払う\n・デカいテレビを買う\n・みんなで旅行に行く", V(.0f, .04f, .004f), new Vector2(.5f, .36f), Hex("2A2C33"), .75f, V(0, 0, -2), null, TextAlignmentOptions.Left);
        Text(goals, "絶対ムリ", V(.02f, -.27f, .005f), new Vector2(.5f, .16f), Hex("D2362C"), 1.9f, V(0, 0, 7));
        foreach (int s in new[] { -1, 1 }) Box(goals, "Tape", V(s * .25f, .4f, .004f), V(.1f, .035f, .002f), Mat("tape", "D9CFA0", .3f), V(0, 0, s * 30));
        // ポスター
        Poster(w, V(14.2f, 1.75f, .012f), new Vector2(.6f, .84f), purpleDark, yellow, "家賃\nor\nDIE", Hex("F2D048"), 3);
        Poster(w, V(15.05f, 1.55f, .012f), new Vector2(.5f, .36f), Mat("poster_red", "A8322C", .1f), paper, "大盛り無料", Hex("FFF3D0"), -4);
        Poster(w, V(17.0f, 1.25f, .012f), new Vector2(.42f, .3f), Mat("poster_teal", "2F6F6C", .1f), paper, "ゴミは火・金", Hex("FFF3D0"), 2);
        Poster(w, V(18.95f, 2.2f, .012f), new Vector2(.34f, .24f), yellow, paper, "節電!!", Hex("2A2C33"), -6);
        // 電飾（西の壁の上、ベッドの上を通る）
        var lights = Group(root, "Fairy lights", V(13.22f, F + 2.3f, 0));
        var path = new List<Vector3>();
        for (int i = 0; i <= 32; i++) { float t = i / 32f; path.Add(V(0, -.16f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4)), 14.4f + t * 8.6f)); }
        Tube(lights, "Wire", "fairy_wire", path.ToArray(), .004f, black, false, default, default, false);
        for (int i = 1; i < 32; i += 2) Ball(lights, "Bulb", path[i] + V(.012f, -.02f, 0), V(.035f, .045f, .035f), fairy);
    }

    // ================================================================ すみっこ（段ボールとサメ・扇風機・観葉植物・洗濯物）
    static void Corners(Transform root)
    {
        var boxes = Group(root, "Cardboard stack", V(22.25f, F, 21.25f), -78);
        RBox(boxes, "Big box", V(0, .26f, 0), V(.78f, .52f, .58f), .01f, cardboard);
        Box(boxes, "Tape", V(0, .522f, 0), V(.78f, .003f, .07f), Mat("tape_brown", "9A7446", .3f));
        Text(boxes, "激安まつり", V(0, .3f, .292f), new Vector2(.62f, .2f), Hex("2A2019"), 2.2f, V(0, 0, -3));
        Box(boxes, "Arrow mark", V(-.3f, .44f, .2905f), V(.05f, .08f, .002f), Mat("ink_brown", "2A2019", .1f));
        RBox(boxes, "Small box", V(.62f, .17f, .1f), V(.42f, .34f, .4f), .01f, cardboardDark, V(0, 18, 0));
        Box(boxes, "Open flap", V(.62f, .38f, .32f), V(.4f, .008f, .2f), cardboardDark, V(-50, 18, 0));
        Box(boxes, "Open flap", V(.62f, .38f, -.12f), V(.4f, .008f, .2f), cardboardDark, V(40, 18, 0));
        Solid(boxes, V(0, .26f, 0), V(.78f, .52f, .58f));
        Shark(boxes, V(-.02f, .53f, .0f), 12);

        var fan = Group(root, "Floor fan", V(22.2f, F, 15.75f), -125);
        Cyl(fan, "Base", V(0, .015f, 0), .34f, .03f, white); Cyl(fan, "Pole", V(0, .3f, 0), .035f, .55f, white);
        RBox(fan, "Motor", V(0, .62f, -.06f), V(.12f, .12f, .16f), .04f, white);
        Tube(fan, "Guard", "fan_guard", Circle(.21f, 20), .007f, metalGrey, true, V(0, .62f, .06f));
        Tube(fan, "Guard", "fan_guard_b", Circle(.21f, 20), .007f, metalGrey, true, V(0, .62f, -.0f));
        Tube(fan, "Guard", "fan_guard_c", Circle(.11f, 14), .005f, metalGrey, true, V(0, .62f, .075f));
        for (int i = 0; i < 12; i++) Box(fan, "Guard spoke", V(Mathf.Cos(i * 30 * Mathf.Deg2Rad) * .12f, .62f + Mathf.Sin(i * 30 * Mathf.Deg2Rad) * .12f, .07f), V(.19f, .004f, .004f), metalGrey, V(0, 0, i * 30));
        for (int i = 0; i < 3; i++) Ball(fan, "Blade", V(Mathf.Cos(i * 120 * Mathf.Deg2Rad) * .09f, .62f + Mathf.Sin(i * 120 * Mathf.Deg2Rad) * .09f, .03f), V(.17f, .09f, .012f), Mat("fan_blade", "7FA6C8", .5f), V(0, 18, i * 120 + 30));
        Cyl(fan, "Hub", V(0, .62f, .04f), .06f, .03f, white, V(90, 0, 0));

        Plant(root, V(22.3f, F, 17.98f), 1.25f, 11);

        var laundry = Group(root, "Laundry heap", V(15.75f, F, 16.1f), 20);
        RBox(laundry, "Shirt", V(0, .05f, 0), V(.5f, .1f, .4f), .04f, Mat("cloth_blue", "56719A", .05f), V(0, 10, 4));
        RBox(laundry, "Towel", V(.12f, .12f, .08f), V(.34f, .08f, .3f), .035f, Mat("cloth_cream", "D8CDB4", .05f), V(6, -30, -5));
        RBox(laundry, "Sock", V(-.32f, .025f, .22f), V(.2f, .05f, .08f), .024f, red, V(0, 40, 0));
        RBox(laundry, "Sock", V(.4f, .025f, -.2f), V(.2f, .05f, .08f), .024f, white, V(0, -25, 0));

        var ball = Group(root, "Basketball", V(15.9f, F, 14.5f));
        Ball(ball, "Ball", V(0, .12f, 0), V(.24f, .24f, .24f), orange);
        Tube(ball, "Seam", "ball_seam", Circle(.121f, 16).Select(p => V(p.x, p.y, 0)).ToArray(), .004f, black, true, V(0, .12f, 0), V(0, 30, 0), false);
        Tube(ball, "Seam", "ball_seam", Circle(.121f, 16).Select(p => V(p.x, p.y, 0)).ToArray(), .004f, black, true, V(0, .12f, 0), V(90, 30, 0), false);

        var shoes = Group(root, "Entry shoes", V(19.2f, F, 14.25f));
        for (int i = 0; i < 4; i++)
        {
            var mat = i < 2 ? white : red;
            RBox(shoes, "Sneaker", V(i * .16f - (i >= 2 ? -.1f : 0), .04f, (i % 2) * .05f), V(.1f, .08f, .26f), .035f, mat, V(0, i * 9 - 10, 0));
            RBox(shoes, "Sneaker sole", V(i * .16f - (i >= 2 ? -.1f : 0), .012f, (i % 2) * .05f), V(.105f, .024f, .27f), .01f, paper, V(0, i * 9 - 10, 0));
        }
    }

    // ================================================================ 床の散らかり（当たり判定なし）
    static void Clutter(Transform root)
    {
        var g = Group(root, "Floor clutter");
        // 手前に置く見せ場の雑誌
        OpenMagazine(g, V(18.55f, F, 17.25f), 160);
        Controller(g, V(17.55f, F, 18.75f), 40, purple);
        PizzaBox(g, V(18.35f, F, 20.55f), -30, false, false);
        Magazine(g, V(15.6f, F + .001f, 18.95f), 25, red, "週刊バイト");
        Magazine(g, V(18.9f, F + .001f, 19.4f), -50, teal, "家賃ゼロ生活");
        TrashBag(g, V(19.6f, F, 21.6f), white, .3f);

        var blocked = new[]
        {
            new Rect(15.65f, 19.3f, 1.7f, 1.1f),   // こたつ
            new Rect(15.3f, 20.75f, 2.4f, 1.1f),   // ソファ
            new Rect(15.6f, 17.5f, 1.6f, .5f),     // テレビ台
            new Rect(18.2f, 16.9f, .8f, .7f),      // 開いた雑誌
            new Rect(18.1f, 20.3f, .6f, .6f),      // ピザの箱
        };
        int placed = 0, tries = 0;
        while (placed < 62 && tries++ < 2000)
        {
            float x, z;
            if (placed < 44) { x = Rand(15.45f, 19.6f); z = Rand(18.1f, 21.9f); }
            else if (placed < 54) { x = Rand(20.2f, 22.0f); z = Rand(15.6f, 18.4f); }
            else { x = Rand(15.6f, 20.5f); z = Rand(14.3f, 17.3f); }
            var p = new Vector2(x, z);
            if (blocked.Any(r => r.Contains(p))) continue;
            Vector3 pos = V(x, F, z); float yaw = Rand(0, 360);
            int kind = rng.Next(100);
            if (kind < 30) Can(g, pos, rng.Next(canColors.Length), rng.Next(100) < 55, yaw);
            else if (kind < 46) SnackBox(g, pos, yaw, canColors[rng.Next(canColors.Length)], rng.Next(2) == 0 ? white : yellow);
            else if (kind < 64) PaperScrap(g, pos, yaw);
            else if (kind < 76) ChipBag(g, pos, yaw, canColors[rng.Next(canColors.Length)]);
            else if (kind < 84) Magazine(g, pos + V(0, .001f, 0), yaw, canColors[rng.Next(canColors.Length)]);
            else if (kind < 90) CupNoodle(g, pos);
            else if (kind < 95) RBox(g, "Sock", pos + V(0, .025f, 0), V(.2f, .05f, .08f), .024f, rng.Next(2) == 0 ? white : fabricGrey, V(0, yaw, 0));
            else Bottle(g, pos + V(0, .035f, 0), 1.15f, V(90, yaw, 0));
            placed++;
        }
    }

    static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // ================================================================ 明かり
    static void Lights(Transform root)
    {
        var g = Group(root, "Lighting");
        // 吊りランプ（主役。ここだけ影を落とす）
        var pendant = Group(g, "Pendant lamp", V(16.5f, Ceiling, 19.85f));
        Cyl(pendant, "Ceiling rose", V(0, -.02f, 0), .12f, .04f, metalDark);
        Cyl(pendant, "Cord", V(0, -.4f, 0), .012f, .76f, black);
        Lathe(pendant, "Shade", "pendant_shade", new[] { new Vector2(.415f, -1.09f), new Vector2(.4f, -1.06f), new Vector2(.2f, -.88f), new Vector2(.09f, -.8f), new Vector2(.035f, -.78f) }, V(0, 0, 0), lampShade, default, 20);
        Lathe(pendant, "Shade inside", "pendant_inner", new[] { new Vector2(.39f, -1.065f), new Vector2(.19f, -.89f), new Vector2(.08f, -.81f) }, V(0, 0, 0), Mat("lamp_inner", "F2E2C0", .2f, 0, "FFD9A0", 1.4f, null, null, true), default, 20);
        Ball(pendant, "Bulb", V(0, -.97f, 0), V(.13f, .15f, .13f), bulb);
        foreach (var r in pendant.GetComponentsInChildren<MeshRenderer>()) if (r.name != "Shade") r.shadowCastingMode = ShadowCastingMode.Off;
        Lamp(pendant, "Pendant light", V(0, -1.0f, 0), new Color(1f, .78f, .5f), 15f, 11f, true);

        var kitchen = Group(g, "Kitchen lamp", V(18.9f, Ceiling, 22.2f));
        Cyl(kitchen, "Cord", V(0, -.25f, 0), .01f, .5f, black);
        Lathe(kitchen, "Shade", "kitchen_shade", new[] { new Vector2(.205f, -.64f), new Vector2(.2f, -.62f), new Vector2(.07f, -.52f), new Vector2(.03f, -.5f) }, V(0, 0, 0), Mat("lamp_shade_green", "3F5E52", .4f, .3f, null, 1, null, null, true), default, 16);
        Ball(kitchen, "Bulb", V(0, -.6f, 0), V(.09f, .1f, .09f), bulb);
        foreach (var r in kitchen.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        Lamp(kitchen, "Kitchen light", V(0, -.72f, 0), new Color(1f, .8f, .55f), 5f, 6.5f);

        Lamp(g, "Entry fill", V(20.9f, F + 2.2f, 15.2f), new Color(1f, .78f, .55f), 1.6f, 5.5f);
        Lamp(g, "Neon glow", V(17.35f, F + 1.7f, 22.95f), new Color(.72f, .36f, 1f), 2.6f, 5f);
        Lamp(g, "Monitor glow", V(21.95f, F + 1.1f, 16.8f), new Color(.75f, 1f, .62f), 1.6f, 2.6f);
        Lamp(g, "TV glow", V(16.4f, F + .9f, 18.25f), new Color(.45f, .62f, 1f), 2.2f, 3.2f);
        Lamp(g, "Aquarium glow", V(17.3f, F + 1.25f, 22.85f), new Color(.35f, .8f, 1f), 1.8f, 2.6f);
        Lamp(g, "Slot glow", V(22.0f, F + 1.75f, 18.85f), new Color(1f, .7f, .25f), 1.8f, 2.6f);
        Lamp(g, "Fairy glow", V(13.9f, F + 1.9f, 16.6f), new Color(1f, .72f, .42f), 2.2f, 5f);
        Lamp(g, "Fairy glow", V(13.9f, F + 1.9f, 20.4f), new Color(1f, .72f, .42f), 2.2f, 5f);

        // 入口側のちょうちん型ランプ（南の壁の貼り紙を照らす）
        var lantern = Group(g, "Paper lantern", V(17.3f, Ceiling, 15.7f));
        Cyl(lantern, "Cord", V(0, -.3f, 0), .01f, .6f, black);
        Ball(lantern, "Lantern", V(0, -.78f, 0), V(.46f, .4f, .46f), Mat("glow_lantern", "F4E6C8", .1f, 0, "FFC98A", 2.2f));
        for (int i = 0; i < 5; i++) Tube(lantern, "Rib", "lantern_rib" + i, Circle(.232f * Mathf.Sqrt(1 - Mathf.Pow((i - 2) * .36f, 2)), 18).Select(c => V(c.x, 0, c.y)).ToArray(), .004f, woodLight, true, V(0, -.78f + (i - 2) * .072f, 0), default, false);
        foreach (var r in lantern.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        Lamp(lantern, "Lantern light", V(0, -.78f, 0), new Color(1f, .8f, .56f), 6.5f, 8f);
    }

    // ================================================================ 画面の仕上げ
    static void PostProcess(Scene scene)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        T Get<T>() where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var c)) return c;
            c = profile.Add<T>(true); c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile); return c;
        }
        var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
        // 値は 2026/11/13 の仕上げ（ACES。Docs/restyle_20261113/material/step10_final）にそろえてある
        var bloom = Get<Bloom>(); bloom.threshold.Override(1f); bloom.intensity.Override(.35f); bloom.scatter.Override(.7f); bloom.tint.Override(new Color(1, .92f, .82f));
        var color = Get<ColorAdjustments>(); color.postExposure.Override(.6f); color.contrast.Override(0); color.saturation.Override(10); color.colorFilter.Override(new Color(1, .98f, .95f));
        var white = Get<WhiteBalance>(); white.temperature.Override(0); white.tint.Override(2);
        var split = Get<SplitToning>(); split.shadows.Override(new Color(.42f, .42f, .56f)); split.highlights.Override(new Color(.64f, .57f, .5f)); split.balance.Override(-10);
        var smh = Get<ShadowsMidtonesHighlights>(); smh.shadows.Override(new Vector4(.97f, .965f, 1.06f, .02f)); smh.midtones.Override(new Vector4(1, 1, 1, 0)); smh.highlights.Override(new Vector4(1.02f, 1, .97f, -.08f));
        var vignette = Get<Vignette>(); vignette.intensity.Override(.15f); vignette.smoothness.Override(.4f); vignette.color.Override(new Color(.04f, .03f, .08f));
        EditorUtility.SetDirty(profile);

        var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == VolumeName);
        if (go == null) { go = new GameObject(VolumeName); SceneManager.MoveGameObjectToScene(go, scene); }
        var volume = go.GetComponent<Volume>(); if (volume == null) volume = go.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 1; volume.sharedProfile = profile;
    }

    // ================================================================ 小物
    static Vector3[] Circle(float radius, int count) => Enumerable.Range(0, count).Select(i => V(Mathf.Cos(i * Mathf.PI * 2 / count) * radius, Mathf.Sin(i * Mathf.PI * 2 / count) * radius, 0)).ToArray();

    static readonly Vector2[] CanProfile = { new Vector2(0, 0), new Vector2(.029f, 0), new Vector2(.033f, .008f), new Vector2(.033f, .112f), new Vector2(.027f, .122f), new Vector2(0, .122f) };
    static void Can(Transform p, Vector3 pos, int color, bool tipped, float yaw = 0)
    {
        var c = Group(p, "Can", tipped ? pos + V(0, .033f, 0) : pos, yaw);
        if (tipped) c.localEulerAngles = V(0, yaw, 90);
        var body = canColors[color % canColors.Length];
        Lathe(c, "Body", "can", CanProfile, Vector3.zero, body, default, 12);
        Cyl(c, "Label", V(0, .062f, 0), .0675f, .045f, canColors[(color + 3) % canColors.Length]);
        Cyl(c, "Lid", V(0, .121f, 0), .052f, .004f, metalGrey);
    }

    static readonly Vector2[] BottleProfile = { new Vector2(0, 0), new Vector2(.032f, 0), new Vector2(.035f, .01f), new Vector2(.035f, .13f), new Vector2(.014f, .19f), new Vector2(.013f, .24f), new Vector2(0, .24f) };
    static void Bottle(Transform p, Vector3 pos, float scale, Vector3 rot = default)
    {
        var b = Group(p, "Bottle", pos); b.localEulerAngles = rot; b.localScale = Vector3.one * scale;
        Lathe(b, "Glass", "bottle", BottleProfile, Vector3.zero, glassBrown, default, 12);
        Cyl(b, "Label", V(0, .075f, 0), .072f, .07f, cream);
        Cyl(b, "Cap", V(0, .242f, 0), .03f, .012f, gold);
    }

    static readonly Vector2[] CupProfile = { new Vector2(0, 0), new Vector2(.036f, 0), new Vector2(.052f, .095f), new Vector2(.056f, .1f), new Vector2(0, .1f) };
    static readonly Vector2[] CupBand = { new Vector2(.0425f, .03f), new Vector2(.0495f, .072f) };
    static void CupNoodle(Transform p, Vector3 pos)
    {
        var c = Group(p, "Cup noodle", pos, Rand(0, 360));
        Lathe(c, "Cup", "cup", CupProfile, Vector3.zero, white, default, 12);
        Lathe(c, "Band", "cup_band", CupBand, Vector3.zero, red, default, 12);
    }

    static void Bowl(Transform p, Vector3 pos)
    {
        var b = Group(p, "Snack bowl", pos);
        Lathe(b, "Bowl", "bowl", new[] { new Vector2(0, 0), new Vector2(.05f, 0), new Vector2(.09f, .05f), new Vector2(.095f, .055f), new Vector2(.08f, .05f), new Vector2(0, .035f) }, Vector3.zero, teal, default, 12);
        for (int i = 0; i < 6; i++) Ball(b, "Snack", V(Mathf.Cos(i) * .04f, .05f, Mathf.Sin(i) * .04f), V(.035f, .02f, .03f), cheese, V(0, i * 50, 20));
    }

    static void SnackBox(Transform p, Vector3 pos, float yaw, Material body, Material label)
    {
        var b = Group(p, "Snack box", pos, yaw);
        RBox(b, "Box", V(0, .027f, 0), V(.17f, .054f, .11f), .006f, body);
        Box(b, "Label", V(0, .0545f, 0), V(.1f, .002f, .07f), label);
    }

    static void ChipBag(Transform p, Vector3 pos, float yaw, Material body)
    {
        var b = Group(p, "Chip bag", pos, yaw);
        RBox(b, "Bag", V(0, .03f, 0), V(.2f, .06f, .14f), .028f, body, V(0, 0, Rand(-6, 6)));
        Box(b, "Band", V(0, .061f, 0), V(.2f, .002f, .05f), yellow);
        Box(b, "Seal", V(.105f, .028f, 0), V(.02f, .012f, .13f), metalGrey);
    }

    static void PaperScrap(Transform p, Vector3 pos, float yaw)
    {
        float s = Rand(.08f, .2f);
        Box(p, "Paper scrap", pos + V(0, .002f, 0), V(s, .002f, s * Rand(.6f, 1.3f)), rng.Next(4) == 0 ? cream : paper, V(0, yaw, 0), false);
    }

    static void PizzaBox(Transform p, Vector3 pos, float yaw, bool withPizza, bool open = true)
    {
        var b = Group(p, "Pizza box", pos, yaw);
        Box(b, "Box", V(0, .02f, 0), V(.42f, .04f, .42f), cream);
        if (!open)
        {
            Box(b, "Lid", V(0, .046f, .01f), V(.42f, .008f, .42f), cream, V(0, 6, 0));
            Box(b, "Lid print", V(0, .051f, .01f), V(.24f, .002f, .24f), red, V(0, 6, 0));
            return;
        }
        Box(b, "Lid", V(0, .2f, -.24f), V(.42f, .008f, .42f), cream, V(-78, 0, 0));
        Box(b, "Lid print", V(0, .2f, -.234f), V(.24f, .002f, .24f), red, V(-78, 0, 0));
        if (!withPizza) { Cyl(b, "Grease", V(0, .0415f, 0), .3f, .003f, Mat("grease", "CDBE95", .5f)); Prism(b, "Last slice", "pizza_slice", new Vector2(0, 0), new Vector2(-.05f, .15f), new Vector2(.05f, .15f), .012f, V(.03f, .05f, .02f), cheese, V(90, 40, 0)); return; }
        Cyl(b, "Crust", V(0, .046f, 0), .36f, .012f, crust);
        Cyl(b, "Cheese", V(0, .053f, 0), .32f, .008f, cheese);
        for (int i = 0; i < 7; i++) { float a = i * 51.4f * Mathf.Deg2Rad; float r = i == 0 ? 0 : .1f; Cyl(b, "Pepperoni", V(Mathf.Cos(a) * r, .058f, Mathf.Sin(a) * r), .05f, .004f, red); }
        for (int i = 0; i < 6; i++) { float a = (i * 60 + 25) * Mathf.Deg2Rad; Ball(b, "Pepper", V(Mathf.Cos(a) * .065f, .059f, Mathf.Sin(a) * .065f), V(.03f, .006f, .012f), green, V(0, i * 40, 0)); }
    }

    static void Controller(Transform p, Vector3 pos, float yaw, Material body)
    {
        var c = Group(p, "Game controller", pos, yaw);
        RBox(c, "Body", V(0, .022f, 0), V(.15f, .034f, .075f), .016f, body);
        foreach (int s in new[] { -1, 1 })
        {
            RBox(c, "Grip", V(s * .062f, .02f, -.04f), V(.045f, .036f, .085f), .017f, body, V(0, s * -14, 0));
            Cyl(c, "Stick", V(s * .035f, .045f, -.008f), .02f, .014f, metalDark);
        }
        Cyl(c, "D-pad", V(-.045f, .041f, .018f), .026f, .006f, metalDark);
        Ball(c, "Button", V(.04f, .042f, .02f), V(.012f, .008f, .012f), red); Ball(c, "Button", V(.056f, .042f, .008f), V(.012f, .008f, .012f), yellow);
        Ball(c, "Button", V(.056f, .042f, .032f), V(.012f, .008f, .012f), green); Ball(c, "Button", V(.072f, .042f, .02f), V(.012f, .008f, .012f), blue);
    }

    static void Magazine(Transform p, Vector3 pos, float yaw, Material cover, string title = null)
    {
        var m = Group(p, "Magazine", pos, yaw);
        Box(m, "Pages", V(0, .006f, 0), V(.21f, .012f, .285f), paper);
        Box(m, "Cover", V(0, .0125f, 0), V(.212f, .002f, .287f), cover);
        Box(m, "Cover photo", V(0, .014f, .03f), V(.16f, .001f, .14f), cream);
        if (title != null) Text(m, title, V(0, .0155f, -.1f), new Vector2(.19f, .06f), Color.white, .5f, V(90, 0, 0));
    }

    static void OpenMagazine(Transform p, Vector3 pos, float yaw)
    {
        var m = Group(p, "Open magazine", pos, yaw);
        Box(m, "Left page", V(-.2f, .018f, 0), V(.4f, .012f, .54f), Mat("page_yellow", "E9C83C", .2f), V(0, 0, -4));
        Box(m, "Right page", V(.2f, .018f, 0), V(.4f, .012f, .54f), paper, V(0, 0, 4));
        Box(m, "Left header", V(-.2f, .026f, -.2f), V(.34f, .002f, .09f), Mat("page_ink", "23262E", .2f), V(0, 0, -4));
        Text(m, "パチスロ必勝ガイド", V(-.2f, .0285f, -.2f), new Vector2(.32f, .06f), Hex("F2D048"), .42f, V(90, 0, 0));
        Text(m, "大当り", V(-.2f, .0275f, -.02f), new Vector2(.34f, .2f), Hex("2B4FA8"), 1.6f, V(90, 0, 0));
        Text(m, "の法則", V(-.17f, .0275f, .14f), new Vector2(.3f, .1f), Hex("2A2C33"), .85f, V(90, 0, 0));
        Box(m, "Left stamp", V(-.3f, .026f, .2f), V(.12f, .002f, .07f), red, V(0, 12, -4));
        Box(m, "Right header", V(.2f, .026f, -.2f), V(.34f, .002f, .07f), Mat("page_green", "3C8A4A", .2f), V(0, 0, 4));
        Text(m, "激アツ演出まとめ", V(.2f, .0285f, -.2f), new Vector2(.32f, .05f), Color.white, .36f, V(90, 0, 0));
        Box(m, "Right panel", V(.2f, .026f, .02f), V(.34f, .002f, .22f), Mat("page_red", "C2362E", .2f), V(0, 0, 4));
        Text(m, "777", V(.2f, .029f, .02f), new Vector2(.32f, .2f), Hex("FFF3D0"), 2.4f, V(90, 0, 0));
        for (int i = 0; i < 3; i++) Box(m, "Text lines", V(.2f, .026f, .17f + i * .03f), V(.32f, .002f, .012f), Mat("page_grey", "9A9484", .1f), V(0, 0, 4));
    }

    static void TrashBag(Transform p, Vector3 pos, Material mat, float size)
    {
        var b = Group(p, "Trash bag", pos, Rand(0, 360));
        Ball(b, "Bag", V(0, size * .42f, 0), V(size * 1.15f, size * .9f, size * 1.05f), mat);
        Ball(b, "Bulge", V(size * .25f, size * .3f, size * .1f), V(size * .7f, size * .6f, size * .7f), mat);
        Lathe(b, "Knot", "bag_knot", new[] { new Vector2(.02f, 0), new Vector2(.012f, .03f), new Vector2(.04f, .07f) }, V(0, size * .82f, 0), mat, default, 8);
    }

    static void Books(Transform p, Vector3 start, float width, int count)
    {
        float x = 0; int i = 0;
        while (x < width && i < count * 2)
        {
            float t = .03f + (i * 37 % 5) * .008f, h = .22f + (i * 53 % 6) * .02f;
            bool lean = i % 5 == 4;
            Box(p, "Book", start + V(x + t * .5f, h * .5f + .018f, (i % 3) * .01f), V(t, h, .2f), canColors[(i * 5 + 2) % canColors.Length], V(0, 0, lean ? -12 : 0));
            x += t + (lean ? .05f : .004f); i++;
        }
    }

    static void Plush(Transform p, Vector3 pos, Material mat, float size)
    {
        var g = Group(p, "Plush", pos, Rand(-25, 25));
        Ball(g, "Body", V(0, size * .5f, 0), V(size, size, size * .85f), mat);
        Ball(g, "Head", V(0, size * 1.15f, 0), V(size * .8f, size * .7f, size * .7f), mat);
        foreach (int s in new[] { -1, 1 })
        {
            Ball(g, "Ear", V(s * size * .3f, size * 1.5f, 0), V(size * .25f, size * .28f, size * .12f), mat);
            Ball(g, "Eye", V(s * size * .16f, size * 1.2f, size * .32f), V(size * .08f, size * .08f, size * .05f), black);
            Ball(g, "Arm", V(s * size * .5f, size * .6f, size * .1f), V(size * .28f, size * .4f, size * .28f), mat);
        }
    }

    static void LuckyCat(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "Lucky cat", pos, yaw);
        Lathe(g, "Body", "cat_body", new[] { new Vector2(0, 0), new Vector2(.08f, 0), new Vector2(.11f, .06f), new Vector2(.1f, .15f), new Vector2(.06f, .2f), new Vector2(0, .21f) }, Vector3.zero, gold, default, 14);
        Ball(g, "Head", V(0, .27f, 0), V(.2f, .17f, .17f), gold);
        foreach (int s in new[] { -1, 1 })
        {
            Prism(g, "Ear", "cat_ear", new Vector2(-.035f, 0), new Vector2(.035f, 0), new Vector2(0, .06f), .03f, V(s * .065f, .335f, 0), gold);
            Ball(g, "Eye", V(s * .045f, .285f, .078f), V(.022f, .028f, .012f), black);
        }
        Ball(g, "Paw", V(.11f, .3f, .03f), V(.06f, .12f, .06f), gold, V(0, 0, -12));
        Ball(g, "Nose", V(0, .262f, .084f), V(.016f, .012f, .01f), red);
        Cyl(g, "Collar", V(0, .2f, 0), .13f, .018f, red);
        Cyl(g, "Coin", V(-.02f, .1f, .1f), .09f, .012f, slotYellow, V(80, 0, 0));
    }

    static void Dart(Transform p, Vector3 pos, Material flight, Vector3 rot)
    {
        var d = Group(p, "Dart", pos); d.localEulerAngles = rot;
        Cyl(d, "Shaft", V(0, 0, .07f), .008f, .14f, metalGrey, V(90, 0, 0));
        Box(d, "Flight", V(0, 0, .14f), V(.05f, .002f, .05f), flight); Box(d, "Flight", V(0, 0, .14f), V(.002f, .05f, .05f), flight);
    }

    static void Poster(Transform p, Vector3 pos, Vector2 size, Material bg, Material frame, string text, Color textColor, float roll)
    {
        var g = Group(p, "Poster - " + text.Replace("\n", " "), pos); g.localEulerAngles = V(0, 0, roll);
        Box(g, "Paper", V(0, 0, 0), V(size.x, size.y, .004f), frame);
        Box(g, "Print", V(0, 0, .0025f), V(size.x - .05f, size.y - .05f, .002f), bg);
        Text(g, text, V(0, 0, .0045f), new Vector2(size.x - .1f, size.y - .1f), textColor, 3);
        foreach (int s in new[] { -1, 1 }) Cyl(g, "Pin", V(s * (size.x * .5f - .04f), size.y * .5f - .04f, .006f), .02f, .008f, red, V(90, 0, 0));
    }

    static void Plant(Transform root, Vector3 pos, float height, int leaves)
    {
        var g = Group(root, "House plant", pos, Rand(0, 360));
        Lathe(g, "Pot", "plant_pot", new[] { new Vector2(0, 0), new Vector2(.11f, 0), new Vector2(.15f, .24f), new Vector2(.16f, .25f), new Vector2(.13f, .25f), new Vector2(.12f, .2f), new Vector2(0, .2f) }, Vector3.zero, pot, default, 14);
        Cyl(g, "Soil", V(0, .21f, 0), .25f, .02f, Mat("soil", "3A2A20", .05f));
        for (int i = 0; i < leaves; i++)
        {
            float a = i * 137.5f, tilt = 25 + (i * 29 % 40), len = height * (.45f + (i * 17 % 10) * .04f);
            var stem = Group(g, "Stem", V(0, .22f, 0), a); stem.localEulerAngles = V(tilt, a, 0);
            Cyl(stem, "Stalk", V(0, len * .5f, 0), .014f, len, leafDark);
            Ball(stem, "Leaf", V(0, len + .1f, .03f), V(.2f + (i % 3) * .04f, .3f, .02f), i % 2 == 0 ? leaf : leafDark, V(25, 0, 0));
        }
    }

    static void Shark(Transform p, Vector3 pos, float yaw)
    {
        var s = Group(p, "Shark plush", pos, yaw);
        var body = Group(s, "Body", V(0, .17f, 0)); body.localEulerAngles = V(0, 0, -90);   // +Y を前（+X）へ倒す
        Lathe(body, "Skin", "shark_body", new[] { new Vector2(0, -.46f), new Vector2(.05f, -.42f), new Vector2(.1f, -.3f), new Vector2(.16f, -.08f), new Vector2(.17f, .1f), new Vector2(.14f, .28f), new Vector2(.08f, .4f), new Vector2(0, .46f) }, Vector3.zero, sharkBlue, default, 16);
        Ball(s, "Belly", V(.06f, .12f, 0), V(.62f, .22f, .27f), sharkBelly);
        Prism(s, "Dorsal fin", "shark_dorsal", new Vector2(-.1f, 0), new Vector2(.1f, 0), new Vector2(-.08f, .16f), .03f, V(-.02f, .32f, 0), sharkBlue);
        Prism(s, "Tail fin", "shark_tail_up", new Vector2(0, 0), new Vector2(.1f, 0), new Vector2(-.1f, .2f), .03f, V(-.45f, .17f, 0), sharkBlue);
        Prism(s, "Tail fin", "shark_tail_down", new Vector2(0, 0), new Vector2(.1f, 0), new Vector2(-.08f, -.13f), .03f, V(-.45f, .17f, 0), sharkBlue);
        foreach (int side in new[] { -1, 1 })
        {
            Prism(s, "Side fin", "shark_side", new Vector2(-.07f, 0), new Vector2(.07f, 0), new Vector2(-.06f, -.15f), .03f, V(.08f, .1f, side * .15f), sharkBlue, V(side * 55, 0, 0));
            Ball(s, "Eye", V(.3f, .22f, side * .1f), V(.035f, .035f, .02f), black);
        }
        Ball(s, "Mouth", V(.33f, .1f, 0), V(.16f, .05f, .17f), Mat("shark_mouth", "8E2A2E", .1f));
        for (int i = 0; i < 5; i++) Prism(s, "Tooth", "shark_tooth", new Vector2(-.014f, 0), new Vector2(.014f, 0), new Vector2(0, -.03f), .01f, V(.37f, .115f, -.06f + i * .03f), white, V(0, 90, 0));
    }
}
#endif
