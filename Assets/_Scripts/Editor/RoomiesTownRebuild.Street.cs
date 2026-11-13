#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 町の作り直し 3：通りの小物・夜の明かり・遠景。何度実行しても同じ結果。
/// ・夜の明かり（「Town night lights」）：南側の歩道の街灯5本に、北側と同じ光（Street accent S〜）を入れる。
///   店3軒の看板の上にグースネックの灯具と、正面を照らすスポット（Shop light 〜）。エクスプレスの軒下の灯り。
///   朝は消える（6 Time Of Day Lighting の表。この配置ツールの最後にかけ直す）。影は落とさない。
/// ・通りの小物（「Town street props」）：消火栓・マンホール・雨水ます・自転車ラック・郵便ポスト・新聞の箱・道路標識。
///   当たり判定は付けない（AGENTS.md：見た目だけの物に付けてよいのは歩くための床・壁だけ）。道・入口はふさがない位置。
/// ・遠景（GameRoom の Roomies_Backdrop）：ビルの窓の帯を、窓の並んだ素材（一部は明かりが点く）に替える。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string NightLightGroup = "Town night lights";
    const string PropGroup = "Town street props";
    static readonly Color LampColor = new Color(1f, .82f, .6f);

    [MenuItem("Roomies/Art/Town/3 Street And Night Lights")]
    internal static void BuildStreetAndNight()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            NightLights(Fresh(root.transform, NightLightGroup));
            var props = Fresh(root.transform, PropGroup);
            StreetProps(props);
            RoomiesBevelBuilder.ApplyTownUnder(props);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        int bands = DressBackdrop();
        AssetDatabase.SaveAssets();
        // 新しい明かりを朝夜の切り替えに登録し直す（シーンも保存される）
        RoomiesMaterialFinishBuilder.BuildTimeOfDay();
        CombineForSpeed();
        Debug.Log($"[Town] street and night: backdrop window bands {bands}");
    }

    // ================================================================ 夜の明かり
    static void NightLights(Transform g)
    {
        // 南側の街灯（柱は Street の street_lamp。北側の Street accent と同じ高さ・強さ）
        float[] lampX = { -34, -17, 0, 17, 34 };
        for (int i = 0; i < lampX.Length; i++)
            PointLight(g, $"Street accent S{i}", new Vector3(lampX[i], 3.55f, -5.8f), 4f, 10f, LampColor);

        // 店の看板の上のグースネック灯と、正面を照らすスポット：店の名前, 看板の上の高さ, 正面の z, 灯具の x
        foreach (var (shop, y, front, xs) in new[]
        {
            ("CORNER STORE", 6.05f, -8.46f, new[] { 21.0f, 26.1f, 31.2f }),
            ("NEIGHBORHOOD WORKS", 5.05f, -8.96f, new[] { -11.0f, -5.2f }),
        })
        {
            foreach (float x in xs) Gooseneck(g, new Vector3(x, y, front));
            float cx = xs.Average();
            SpotLight(g, "Shop light - " + shop, new Vector3(cx, y + 1.4f, front + 3.2f), new Vector3(cx, y - 2.4f, front), 7f, 11f, 115f, LampColor);
        }
        // エクスプレスの軒下（荷さばき場）と、シャッターの上の灯具
        PointLight(g, "Shop light - EXPRESS canopy", new Vector3(-25.4f, 5.6f, -13.0f), 3.2f, 11f, new Color(1f, .86f, .68f));
        foreach (float x in new[] { -28.1f, -24.2f }) WallLamp(g, new Vector3(x, 3.95f, -18.0f));

        // 裏の家並みの玄関灯（扉の横の壁付け灯）と、家並みを照らす光4つ
        foreach (var (x, spec) in BackRow())
        {
            float doorC = spec.doorLeft ? -spec.width / 2 + 1.25f : spec.width / 2 - 1.25f;
            WallLamp(g, new Vector3(x + doorC + (spec.doorLeft ? .82f : -.82f), 2.15f, -27f));
        }
        foreach (float x in new[] { -29f, -9f, 11f, 30f })
            PointLight(g, "Porch light", new Vector3(x, 2.9f, -25.2f), 2.2f, 9f, new Color(1f, .8f, .58f));
    }

    static Light PointLight(Transform g, string name, Vector3 pos, float intensity, float range, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(g, false); go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.intensity = intensity; l.range = range; l.color = color; l.shadows = LightShadows.None;
        return l;
    }

    static Light SpotLight(Transform g, string name, Vector3 pos, Vector3 target, float intensity, float range, float angle, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(g, false); go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.LookRotation(target - pos);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot; l.intensity = intensity; l.range = range; l.spotAngle = angle; l.innerSpotAngle = angle * .55f; l.color = color; l.shadows = LightShadows.None;
        return l;
    }

    /// <summary>看板を照らす首の曲がった灯具（壁から前へ出て、かさが下を向く）。見た目だけ。</summary>
    static void Gooseneck(Transform g, Vector3 p)
    {
        var dark = Mat("metal_dark");
        var t = new GameObject("Sign lamp") { isStatic = true }.transform; t.SetParent(g, false); t.localPosition = p;
        Box(t, "Sign lamp arm", -.025f, .025f, 0, .05f, 0, .62f, dark);
        Box(t, "Sign lamp arm", -.025f, .025f, -.18f, .05f, .57f, .62f, dark);
        Cyl(t, "Sign lamp shade", new Vector3(0, -.24f, .6f), new Vector3(.3f, .07f, .3f), dark);
        Cyl(t, "Sign lamp bulb", new Vector3(0, -.32f, .6f), new Vector3(.18f, .02f, .18f), Mat("bulb"));
    }

    /// <summary>壁付けの灯具（四角いかさと光る面）。見た目だけ。</summary>
    static void WallLamp(Transform g, Vector3 p)
    {
        var t = new GameObject("Wall lamp") { isStatic = true }.transform; t.SetParent(g, false); t.localPosition = p;
        Box(t, "Wall lamp body", -.16f, .16f, -.1f, .12f, 0, .22f, Mat("metal_dark"));
        Box(t, "Wall lamp lens", -.12f, .12f, -.11f, -.09f, .03f, .19f, Mat("bulb"));
    }

    // ================================================================ 通りの小物（当たり判定なし）
    static void StreetProps(Transform g)
    {
        // 消火栓（歩道の車道寄り）
        foreach (var p in new[] { new Vector3(8.5f, .2f, -4.55f), new Vector3(-25.5f, .2f, 4.55f) }) Hydrant(g, p);
        // マンホール（車道。白線をよける）と雨水ます（縁石の手前）
        foreach (var p in new[] { new Vector3(-21f, 0, 1.6f), new Vector3(11f, 0, -1.6f), new Vector3(29f, 0, 1.6f), new Vector3(-6f, 0, -1.6f) })
        {
            Cyl(g, "Manhole cover", p + new Vector3(0, .108f, 0), new Vector3(.85f, .006f, .85f), Mat("metal_dark"));
            Cyl(g, "Manhole rim", p + new Vector3(0, .104f, 0), new Vector3(.95f, .004f, .95f), Mat("plinth"));
        }
        foreach (var (x, z) in new[] { (-30f, 3.75f), (-9f, -3.75f), (5f, 3.75f), (24f, -3.75f) })
        {
            Box(g, "Storm drain", x - .45f, x + .45f, .1f, .112f, z - .2f, z + .2f, Mat("plinth"));
            for (int i = 0; i < 6; i++) { float gx = x - .36f + i * .144f; Box(g, "Storm drain grate", gx - .025f, gx + .025f, .112f, .118f, z - .17f, z + .17f, Mat("metal_dark")); }
        }
        // 自転車ラック（CORNER STORE の前の空き地。扉の前はよける）
        for (int i = 0; i < 3; i++) BikeHoop(g, new Vector3(20.2f + i * .75f, 0, -7.2f));
        // 郵便ポスト・新聞の箱（北側の歩道の家寄り）
        Mailbox(g, new Vector3(3.2f, .2f, 6.05f));
        NewsBox(g, new Vector3(-20.6f, .2f, 6.1f), "frame_express");
        NewsBox(g, new Vector3(-19.9f, .2f, 6.1f), "frame_h_white");
        // 道路標識（交差点の手前の歩道の端）
        StreetSign(g, new Vector3(38.4f, .2f, -4.55f), "frame_house");
        StreetSign(g, new Vector3(-38.4f, .2f, 4.55f), "frame_house");
    }

    static void Hydrant(Transform g, Vector3 p)
    {
        var red = Mat("hydrant");
        var t = new GameObject("Fire hydrant") { isStatic = true }.transform; t.SetParent(g, false); t.localPosition = p;
        Cyl(t, "Hydrant base", new Vector3(0, .04f, 0), new Vector3(.34f, .04f, .34f), red);
        Cyl(t, "Hydrant body", new Vector3(0, .36f, 0), new Vector3(.24f, .3f, .24f), red);
        Cyl(t, "Hydrant cap", new Vector3(0, .7f, 0), new Vector3(.28f, .05f, .28f), red);
        Cyl(t, "Hydrant top", new Vector3(0, .79f, 0), new Vector3(.12f, .05f, .12f), Mat("handle"));
        var nozzle = Cyl(t, "Hydrant nozzle", new Vector3(0, .48f, 0), new Vector3(.12f, .22f, .12f), red);
        nozzle.transform.localRotation = Quaternion.Euler(90, 0, 0);
    }

    static void BikeHoop(Transform g, Vector3 p)
    {
        var steel = Mat("handle");
        var t = new GameObject("Bike rack") { isStatic = true }.transform; t.SetParent(g, false); t.localPosition = p;
        Box(t, "Bike rack post", -.03f, .03f, 0, .78f, -.32f, -.26f, steel);
        Box(t, "Bike rack post", -.03f, .03f, 0, .78f, .26f, .32f, steel);
        Box(t, "Bike rack post", -.03f, .03f, .78f, .84f, -.32f, .32f, steel);
    }

    static void Mailbox(Transform g, Vector3 p)
    {
        var blue = Mat("frame_h_navy");
        var t = new GameObject("Mailbox") { isStatic = true }.transform; t.SetParent(g, false); t.localPosition = p;
        foreach (float x in new[] { -.2f, .2f }) Box(t, "Mailbox leg", x - .03f, x + .03f, 0, .3f, -.18f, .18f, Mat("metal_dark"));
        Box(t, "Mailbox", -.27f, .27f, .3f, 1.12f, -.24f, .24f, blue);
        Cyl(t, "Mailbox top", new Vector3(0, 1.12f, 0), new Vector3(.54f, .06f, .48f), blue);
        Box(t, "Mailbox slot", -.15f, .15f, .92f, .97f, -.25f, -.23f, Mat("metal_dark"));
    }

    static void NewsBox(Transform g, Vector3 p, string color)
    {
        var t = new GameObject("News box") { isStatic = true }.transform; t.SetParent(g, false); t.localPosition = p;
        Box(t, "News box leg", -.2f, .2f, 0, .35f, -.16f, .16f, Mat("metal_dark"));
        Box(t, "News box", -.26f, .26f, .35f, 1.05f, -.2f, .2f, Mat(color));
        Box(t, "News box window", -.18f, .18f, .62f, .92f, -.21f, -.19f, Mat("glass_home_dark"));
    }

    static void StreetSign(Transform g, Vector3 p, string color)
    {
        var t = new GameObject("Street sign") { isStatic = true }.transform; t.SetParent(g, false); t.localPosition = p;
        Cyl(t, "Sign pole", new Vector3(0, 1.4f, 0), new Vector3(.07f, 1.4f, .07f), Mat("handle"));
        Box(t, "Sign blade", -.04f, .04f, 2.45f, 2.67f, -.55f, .55f, Mat(color));
        Box(t, "Sign blade", -.55f, .55f, 2.72f, 2.94f, -.04f, .04f, Mat(color));
    }

    // ================================================================ 遠景
    /// <summary>遠景のビルの窓の帯を、窓の並んだ素材（2種類を交互）に替える。遠景の配置ツールの最後にも呼ぶ。</summary>
    internal static int DressBackdrop(bool save = true)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var back = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_Backdrop");
        if (back == null) return 0;
        var mats = new[] { Mat("block_windows_a"), Mat("block_windows_b") };
        int n = 0;
        foreach (var r in back.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Window band"))
        {
            var m = mats[(n++ + r.transform.parent.GetSiblingIndex()) % 2];
            if (r.sharedMaterial != m) r.sharedMaterial = m;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (save) EditorSceneManager.SaveScene(scene);
        return n;
    }
}
#endif
