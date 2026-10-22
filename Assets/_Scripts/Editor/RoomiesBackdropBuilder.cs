#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 遠景（山なみ・街の続き・高架を走る電車）をシーンに置くツール。
///
/// 目的：
///  ・マップの端（x±39.3 / z±24）で世界が切れて見えないように、外側へ街と地面を続ける
///  ・家を出て左（-X側）のすぐ近くに高架を通し、電車が頭上を走るようにする
///  ・遠くに山なみを置いて奥行きを出す
///
/// 規約（AGENTS.md）：
///  ・実行時には何も生成しない。ここで実体をシーンへ置く
///  ・見た目は `Visual` の下。電車の動きだけスクリプトが位置を変える
///  ・当たり判定は「近い高架の橋脚」だけ。遠景には付けない（負荷と事故を避ける）
///
/// メニュー：
///  Roomies/Environment/Build Distant Backdrop
///  Roomies/Environment/Remove Distant Backdrop
/// </summary>
internal static class RoomiesBackdropBuilder
{
    private const string RootName = "Roomies_Backdrop";
    private const string MaterialFolder = "Assets/Resources/RoomiesArt/Materials";
    private const string MeshFolder = "Assets/_Prefabs/Backdrop";

    // 遊べる範囲：x ±39.3 / z ±24（Boundary rail の位置）。街の中心はほぼ原点。
    private static readonly Vector3 MapCenter = new Vector3(0f, 0f, 0f);

    private static System.Random random;

    [MenuItem("Roomies/Environment/Build Distant Backdrop")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Backdrop] Play Modeを抜けてから実行してください。");
            return;
        }

        Remove(silent: true);
        random = new System.Random(20261022);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build distant backdrop");

        BuildGroundExtension(root.transform);
        BuildOuterTown(root.transform);
        BuildMountains(root.transform);

        // 家を出て左（-X側）のすぐ近く。頭上を電車が通る
        BuildRailway(root.transform, "Railway near (west)",
            new Vector3(-13f, 0f, 0f), 90f, deckHeight: 12f, length: 520f,
            pierCollider: true, waitSeconds: 16f, startDelay: 3f, speed: 26f);

        // 海の上の遠い高架（奥行き用）
        BuildRailway(root.transform, "Railway far (east)",
            new Vector3(180f, 0f, 0f), 90f, deckHeight: 10f, length: 620f,
            pierCollider: false, waitSeconds: 24f, startDelay: 18f, speed: 42f,
            detailed: false);

        MarkStatic(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        int renderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;
        Debug.Log($"[Backdrop] 遠景（街の続き・山なみ・電車）を置きました。描画物 {renderers} 個・" +
                  "動かないものは静的バッチング対象（シーンを保存してください）");
    }

    [MenuItem("Roomies/Environment/Remove Distant Backdrop")]
    private static void RemoveMenu() => Remove(silent: false);

    private static void Remove(bool silent)
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing == null)
        {
            if (!silent) Debug.Log("[Backdrop] 遠景はまだ置かれていません。");
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        if (!silent) Debug.Log("[Backdrop] 遠景を外しました（シーンを保存してください）");
    }

    // ================================================================
    // 地面の続き
    // ================================================================

    private static void BuildGroundExtension(Transform parent)
    {
        Transform group = Group(parent, "Ground beyond the fence");
        Transform visual = Group(group, "Visual");

        // 既存の地面よりほんの少し下に敷いて、Zファイティングを避ける
        Box(visual, "Outer ground", new Vector3(0f, -0.08f, 0f),
            new Vector3(900f, 0.1f, 900f), "green");

        // 街路の続き（マップの道路と同じ z=0 の東西方向）。これだけは通しの道にする
        Box(visual, "Main road", new Vector3(0f, -0.02f, 0f),
            new Vector3(900f, 0.06f, 7.4f), "ink");
        for (float x = -440f; x <= 440f; x += 14f)
        {
            if (Mathf.Abs(x) < 42f) continue;      // 遊べる範囲の道路は既存のものを使う
            Box(visual, "Lane dash", new Vector3(x, 0.01f, 0f),
                new Vector3(5f, 0.02f, 0.3f), "mustard");
        }

        BuildIrregularRoads(visual);
    }

    /// <summary>
    /// 枝分かれする道。碁盤の目にせず、行き止まりやT字、斜めの道を混ぜる。
    /// （「全部が交差点」に見えないようにするため）
    /// </summary>
    private static void BuildIrregularRoads(Transform visual)
    {
        // 本道から南北へ伸びる枝道。左右で本数も位置も変える
        float[] branches = { -352f, -268f, -184f, -96f, -58f, 74f, 128f, 212f, 296f, 388f };

        foreach (float x in branches)
        {
            bool north = random.Next(2) == 0;
            float length = Range(70f, 280f);
            float width = Range(5f, 8f);
            float z = north ? length / 2f : -length / 2f;

            Box(visual, "Side road", new Vector3(x, -0.02f, z),
                new Vector3(width, 0.06f, length), "ink");

            // 半分くらいはT字に曲がって終わる（十字にはしない）
            if (random.Next(10) < 5)
            {
                float turnZ = north ? length : -length;
                float turnLength = Range(60f, 190f);
                float direction = random.Next(2) == 0 ? 1f : -1f;

                Box(visual, "Side road turn",
                    new Vector3(x + direction * turnLength / 2f, -0.02f, turnZ),
                    new Vector3(turnLength, 0.06f, width), "ink");
            }

            // 行き止まりの広場
            if (random.Next(10) < 3)
            {
                float endZ = north ? length : -length;
                Box(visual, "Cul-de-sac", new Vector3(x, -0.02f, endZ),
                    new Vector3(width * 2.6f, 0.06f, width * 2.6f), "ink");
            }
        }

        // 斜めに走る旧道を2本（碁盤の目をくずす）
        foreach ((Vector3 position, float angle, float length) in new[]
        {
            (new Vector3(-210f, -0.02f, 150f), 34f, 420f),
            (new Vector3(250f, -0.02f, -190f), -27f, 380f),
        })
        {
            Transform road = Group(visual, "Diagonal road");
            road.localPosition = position;
            road.localRotation = Quaternion.Euler(0f, angle, 0f);
            Box(road, "Surface", Vector3.zero, new Vector3(length, 0.06f, 6.6f), "ink");
        }

        // 川（道ばかりだと単調なので）
        Transform river = Group(visual, "River");
        river.localPosition = new Vector3(-120f, -0.05f, -300f);
        river.localRotation = Quaternion.Euler(0f, 18f, 0f);
        Box(river, "Water", Vector3.zero, new Vector3(620f, 0.06f, 26f), "blue");
        Box(river, "Bank", new Vector3(0f, 0.02f, 14.5f), new Vector3(620f, 0.1f, 3f), "cream");
        Box(river, "Bank", new Vector3(0f, 0.02f, -14.5f), new Vector3(620f, 0.1f, 3f), "cream");
        Box(river, "Bridge", new Vector3(90f, 0.6f, 0f), new Vector3(9f, 1.2f, 34f), "cream");
    }

    // ================================================================
    // 外側の街並み
    // ================================================================

    private static void BuildOuterTown(Transform parent)
    {
        Transform group = Group(parent, "Outer town");
        Transform visual = Group(group, "Visual");

        // 街は「かたまり」で置く。密なところと、畑や林だけのところを作る
        (Vector3 center, float spread, int count, int kind)[] clusters =
        {
            (new Vector3(-150f, 0f,  -90f), 90f, 16, 0),   // 住宅
            (new Vector3( 140f, 0f,   80f), 85f, 15, 0),
            (new Vector3(-250f, 0f,  170f), 70f, 10, 1),   // 商店・ビル
            (new Vector3( 230f, 0f, -210f), 95f, 14, 1),
            (new Vector3(  60f, 0f,  260f), 80f, 10, 2),   // 倉庫
            (new Vector3(-330f, 0f, -250f), 90f,  9, 3),   // 工場
            (new Vector3( 340f, 0f,  230f), 70f,  8, 1),
        };

        foreach ((Vector3 center, float spread, int count, int kind) in clusters)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = center + new Vector3(Range(-spread, spread), 0f, Range(-spread, spread));
                if (Mathf.Abs(position.x) < 52f && Mathf.Abs(position.z) < 44f) continue;
                if (Mathf.Abs(position.z) < 7f) continue;   // 本道の上には置かない

                switch (kind)
                {
                    case 0: House(visual, position); break;
                    case 1: Block(visual, position); break;
                    case 2: Warehouse(visual, position); break;
                    default: Factory(visual, position); break;
                }
            }
        }

        BuildLandmarks(visual);
        BuildFieldsAndWoods(visual);
    }

    // ---- 建物の種類 ----

    private static void House(Transform parent, Vector3 position)
    {
        Transform house = Group(parent, "House");
        house.position = position;
        house.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

        float width = Range(8f, 13f);
        float depth = Range(8f, 12f);
        float height = Range(4.5f, 7f);
        string wall = Pick("cream", "paper", "pink", "mustard");

        Box(house, "Body", new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), wall);

        // 切妻屋根（板を2枚傾けて合わせる）
        float slope = Range(26f, 36f);
        foreach (float side in new[] { -1f, 1f })
        {
            Transform roof = Group(house, "Roof");
            roof.localPosition = new Vector3(0f, height + depth * 0.13f, side * depth * 0.25f);
            roof.localRotation = Quaternion.Euler(side * slope, 0f, 0f);
            Box(roof, "Panel", Vector3.zero, new Vector3(width + 1f, 0.35f, depth * 0.62f),
                Pick("coral", "teal", "ink"));
        }

        Box(house, "Chimney", new Vector3(width * 0.28f, height + 1.6f, depth * 0.2f),
            new Vector3(0.9f, 2.4f, 0.9f), "ink");
        Box(house, "Door", new Vector3(0f, 1.1f, depth / 2f + 0.06f), new Vector3(1.2f, 2.2f, 0.12f), "wood");
        Box(house, "Window", new Vector3(width * 0.28f, 2.4f, depth / 2f + 0.06f),
            new Vector3(1.6f, 1.2f, 0.12f), "blue");
    }

    private static void Block(Transform parent, Vector3 position)
    {
        Transform building = Group(parent, "Block");
        building.position = position;
        building.localRotation = Quaternion.Euler(0f, random.Next(8) * 45f, 0f);

        float width = Range(12f, 20f);
        float depth = Range(10f, 18f);
        float height = Range(12f, 34f);
        string wall = Pick("cream", "paper", "teal", "coral");

        Box(building, "Body", new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), wall);
        Box(building, "Roof", new Vector3(0f, height + 0.3f, 0f),
            new Vector3(width + 0.9f, 0.6f, depth + 0.9f), "ink");

        // 階数ぶんの窓。全部同じ高さに並べない
        int floors = Mathf.Clamp(Mathf.FloorToInt(height / 3.4f), 2, 8);
        for (int f = 1; f <= floors; f++)
        {
            float y = f * 3.4f;
            if (y > height - 1.6f) break;
            Box(building, "Window band", new Vector3(0f, y, depth / 2f + 0.06f),
                new Vector3(width * 0.76f, 1.4f, 0.12f), "blue");
            if (random.Next(10) < 6)
                Box(building, "Window band", new Vector3(width / 2f + 0.06f, y, 0f),
                    new Vector3(0.12f, 1.4f, depth * 0.76f), "blue");
        }

        // 屋上の設備（同じ形が並ばないように）
        if (random.Next(10) < 6)
            Box(building, "Rooftop unit", new Vector3(Range(-3f, 3f), height + 1.2f, Range(-3f, 3f)),
                new Vector3(Range(2f, 4f), 1.8f, Range(2f, 4f)), "silver");
        if (random.Next(10) < 4)
            Box(building, "Antenna", new Vector3(Range(-4f, 4f), height + 3.5f, Range(-4f, 4f)),
                new Vector3(0.2f, 5f, 0.2f), "ink");
    }

    private static void Warehouse(Transform parent, Vector3 position)
    {
        Transform shed = Group(parent, "Warehouse");
        shed.position = position;
        shed.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

        float width = Range(22f, 38f);
        float depth = Range(14f, 22f);
        float height = Range(6f, 9f);

        Box(shed, "Body", new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), "silver");
        Box(shed, "Roof", new Vector3(0f, height + 0.25f, 0f),
            new Vector3(width + 1.2f, 0.5f, depth + 1.2f), "teal");

        // シャッター（規則的に並ぶが、ここは倉庫らしさなのでよい）
        int doors = Mathf.Clamp(Mathf.FloorToInt(width / 9f), 2, 4);
        for (int i = 0; i < doors; i++)
        {
            float x = -width / 2f + width / (doors + 1f) * (i + 1);
            Box(shed, "Shutter", new Vector3(x, 2.2f, depth / 2f + 0.08f),
                new Vector3(5f, 4.2f, 0.16f), "mustard");
        }

        if (random.Next(10) < 5)
            Box(shed, "Yard crate", new Vector3(Range(-width / 2f, width / 2f), 1.2f, depth / 2f + Range(4f, 9f)),
                new Vector3(Range(2f, 4f), 2.4f, Range(2f, 4f)), "wood");
    }

    private static void Factory(Transform parent, Vector3 position)
    {
        Transform factory = Group(parent, "Factory");
        factory.position = position;
        factory.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

        float width = Range(20f, 32f);
        float depth = Range(16f, 26f);
        float height = Range(8f, 13f);

        Box(factory, "Body", new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), "ink");
        Box(factory, "Roof", new Vector3(0f, height + 0.3f, 0f),
            new Vector3(width + 1f, 0.6f, depth + 1f), "silver");

        // 煙突（高さを変える）
        int stacks = random.Next(2, 4);
        for (int i = 0; i < stacks; i++)
        {
            float stackHeight = Range(14f, 26f);
            Cylinder(factory, "Chimney",
                new Vector3(Range(-width / 3f, width / 3f), stackHeight / 2f + height, Range(-depth / 3f, depth / 3f)),
                Range(2f, 3.2f), stackHeight, "cream", upright: true);
        }

        // タンク
        for (int i = 0; i < random.Next(1, 4); i++)
            Cylinder(factory, "Tank",
                new Vector3(width / 2f + Range(5f, 12f), 4f, Range(-depth / 2f, depth / 2f)),
                Range(6f, 9f), 8f, "paper", upright: true);
    }

    // ---- 目印になる大きな物 ----

    private static void BuildLandmarks(Transform parent)
    {
        // 電波塔
        Transform tower = Group(parent, "Radio tower");
        tower.position = new Vector3(-196f, 0f, 256f);
        for (int i = 0; i < 4; i++)
        {
            float angle = 90f * i;
            Transform leg = Group(tower, "Leg");
            leg.localRotation = Quaternion.Euler(0f, angle, 0f);
            Box(leg, "Steel", new Vector3(2.4f, 28f, 0f), new Vector3(0.7f, 58f, 0.7f), "coral");
        }
        for (int i = 1; i <= 5; i++)
            Box(tower, "Brace", new Vector3(0f, i * 9.5f, 0f), new Vector3(6.4f, 0.4f, 6.4f), "coral");
        Box(tower, "Mast", new Vector3(0f, 68f, 0f), new Vector3(0.5f, 24f, 0.5f), "silver");
        Box(tower, "Beacon", new Vector3(0f, 80f, 0f), new Vector3(1.2f, 1.2f, 1.2f), "coral");

        // 観覧車
        Transform wheel = Group(parent, "Ferris wheel");
        wheel.position = new Vector3(188f, 0f, 188f);
        wheel.localRotation = Quaternion.Euler(0f, -28f, 0f);
        const float radius = 26f;
        Box(wheel, "Base", new Vector3(0f, 1.2f, 0f), new Vector3(14f, 2.4f, 10f), "cream");
        foreach (float side in new[] { -3.2f, 3.2f })
        {
            Box(wheel, "Support", new Vector3(-6f, radius / 2f + 2f, side),
                new Vector3(1.2f, radius + 4f, 1.2f), "silver");
            Box(wheel, "Support", new Vector3(6f, radius / 2f + 2f, side),
                new Vector3(1.2f, radius + 4f, 1.2f), "silver");
        }
        for (int i = 0; i < 16; i++)
        {
            float angle = 360f / 16f * i;
            Vector3 offset = Quaternion.Euler(angle, 0f, 0f) * new Vector3(0f, radius, 0f);

            Transform spoke = Group(wheel, "Spoke");
            spoke.localPosition = new Vector3(0f, radius + 4f, 0f);
            spoke.localRotation = Quaternion.Euler(angle, 0f, 0f);
            Box(spoke, "Arm", new Vector3(0f, radius / 2f, 0f), new Vector3(0.35f, radius, 0.35f), "silver");

            Box(wheel, "Cabin", new Vector3(0f, radius + 4f + offset.y, offset.z),
                new Vector3(2.6f, 2.6f, 2.6f), Pick("coral", "mustard", "teal", "pink"));
        }

        // 球形のガスタンク（丸い物が1つあると目を引く）
        Transform gas = Group(parent, "Gas holder");
        gas.position = new Vector3(-300f, 0f, -120f);
        Sphere(gas, "Sphere", new Vector3(0f, 16f, 0f), 28f, "paper");
        for (int i = 0; i < 6; i++)
        {
            float angle = 60f * i;
            Transform leg = Group(gas, "Leg");
            leg.localRotation = Quaternion.Euler(0f, angle, 0f);
            Box(leg, "Steel", new Vector3(11f, 6f, 0f), new Vector3(0.8f, 12f, 0.8f), "silver");
        }

        // 丘の上の時計塔
        Transform clock = Group(parent, "Clock tower");
        clock.position = new Vector3(96f, 0f, -300f);
        Box(clock, "Base", new Vector3(0f, 4f, 0f), new Vector3(12f, 8f, 12f), "cream");
        Box(clock, "Shaft", new Vector3(0f, 18f, 0f), new Vector3(7f, 20f, 7f), "paper");
        Box(clock, "Face", new Vector3(0f, 25f, 3.6f), new Vector3(4f, 4f, 0.3f), "ink");
        Box(clock, "Roof", new Vector3(0f, 29f, 0f), new Vector3(9f, 2f, 9f), "coral");
        Box(clock, "Spire", new Vector3(0f, 33f, 0f), new Vector3(0.6f, 6f, 0.6f), "mustard");
    }

    // ---- 畑と林 ----

    private static void BuildFieldsAndWoods(Transform parent)
    {
        // 畑（道の向こうが全部街だと単調なので）
        (Vector3 center, int rows)[] farms =
        {
            (new Vector3(-80f, 0f, 300f), 6),
            (new Vector3(300f, 0f, -80f), 5),
            (new Vector3(-360f, 0f, 90f), 4),
        };

        foreach ((Vector3 center, int rows) in farms)
        {
            Transform farm = Group(parent, "Farm");
            farm.position = center;
            farm.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

            for (int i = 0; i < rows; i++)
            {
                Box(farm, "Plot", new Vector3(0f, 0.02f, -rows * 7f / 2f + i * 7f),
                    new Vector3(Range(50f, 80f), 0.08f, 5.6f), i % 2 == 0 ? "wood" : "green");
            }
        }

        // 林（かたまりで生やす）
        (Vector3 center, float spread, int count)[] woods =
        {
            (new Vector3(-40f, 0f, -320f), 60f, 26),
            (new Vector3(330f, 0f, 60f), 50f, 20),
            (new Vector3(-280f, 0f, 320f), 70f, 24),
        };

        foreach ((Vector3 center, float spread, int count) in woods)
        {
            Transform wood = Group(parent, "Woods");
            wood.position = center;

            for (int i = 0; i < count; i++)
            {
                Transform tree = Group(wood, "Tree");
                tree.localPosition = new Vector3(Range(-spread, spread), 0f, Range(-spread, spread));

                float trunk = Range(3f, 6f);
                Box(tree, "Trunk", new Vector3(0f, trunk / 2f, 0f), new Vector3(0.7f, trunk, 0.7f), "wood");
                Sphere(tree, "Canopy", new Vector3(0f, trunk + 2f, 0f), Range(4f, 7f), "green");
            }
        }

        // 道沿いの街路樹（まばらに）
        for (int i = 0; i < 34; i++)
        {
            float x = Range(-400f, 400f);
            if (Mathf.Abs(x) < 50f) continue;
            float z = (random.Next(2) == 0 ? 1f : -1f) * Range(8f, 14f);

            Transform tree = Group(parent, "Street tree");
            tree.position = new Vector3(x, 0f, z);

            float trunk = Range(2.6f, 4f);
            Box(tree, "Trunk", new Vector3(0f, trunk / 2f, 0f), new Vector3(0.5f, trunk, 0.5f), "wood");
            Sphere(tree, "Canopy", new Vector3(0f, trunk + 1.6f, 0f), Range(3.2f, 4.6f), "green");
        }
    }

    private static string Pick(params string[] options) => options[random.Next(options.Length)];

    // ================================================================
    // 山なみ
    // ================================================================

    private static void BuildMountains(Transform parent)
    {
        Transform group = Group(parent, "Mountains");
        Transform visual = Group(group, "Visual");

        // 形の違う山を3種類作って使い分ける（同じ形が並ぶと作り物っぽく見える）
        Mesh[] shapes =
        {
            LoadOrCreateMountainMesh("backdrop_mountain_a", 1001),
            LoadOrCreateMountainMesh("backdrop_mountain_b", 2002),
            LoadOrCreateMountainMesh("backdrop_mountain_c", 3003),
        };

        (float radius, float minHeight, float maxHeight, string material, int count, bool snow)[] rings =
        {
            (330f,  55f,  90f,  "teal",  13, false),
            (470f,  95f, 150f,  "blue",  11, true),
            (640f, 150f, 230f,  "blue",   9, true),
        };

        foreach ((float radius, float minHeight, float maxHeight, string material, int count, bool snow) in rings)
        {
            Material tint = LoadMaterial(material);
            Material snowTint = LoadMaterial("paper");

            for (int i = 0; i < count; i++)
            {
                // 等間隔に置くと輪っかに見えるので角度はばらす。
                // 距離は外側にだけばらす（内側へ寄せると街の中に山が生えてしまう）
                float angle = (360f / count) * i + Range(-22f, 22f);
                float distance = Mathf.Max(300f, radius + Range(-30f, 150f));
                float height = Range(minHeight, maxHeight);
                float width = height * Range(1.25f, 2.0f);

                Vector3 position = MapCenter + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                position.y = -6f;

                var mountain = new GameObject($"Mountain {Mathf.RoundToInt(radius)}-{i}");
                mountain.transform.SetParent(visual, false);
                mountain.transform.position = position;
                mountain.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                mountain.transform.localScale = new Vector3(width, height, width);

                mountain.AddComponent<MeshFilter>().sharedMesh = shapes[random.Next(shapes.Length)];
                Paint(mountain.AddComponent<MeshRenderer>(), tint);

                // 高い山には雪をかぶせる
                if (!snow || height < 120f) continue;

                var cap = new GameObject("Snow cap");
                cap.transform.SetParent(mountain.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.72f, 0f);
                cap.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                cap.AddComponent<MeshFilter>().sharedMesh = shapes[0];
                Paint(cap.AddComponent<MeshRenderer>(), snowTint);
            }
        }
    }

    /// <summary>
    /// 山のメッシュ。円錐を縦横に分割し、尾根が出るように半径を揺らす。
    /// 24面×7段で、以前の10面の円錐よりはっきり形が出る。
    /// </summary>
    private static Mesh LoadOrCreateMountainMesh(string name, int seed)
    {
        string path = $"{MeshFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        System.IO.Directory.CreateDirectory(MeshFolder);

        const int sides = 24;
        const int rings = 7;
        var shape = new System.Random(seed);

        // 段ごと・方向ごとの半径の揺らぎ（尾根と谷）
        var ridge = new float[sides];
        for (int i = 0; i < sides; i++) ridge[i] = 0.78f + (float)shape.NextDouble() * 0.44f;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        Vector3 Point(int ring, int side)
        {
            float t = (float)ring / rings;                 // 0 = 地面, 1 = 頂上
            float y = t;
            float baseRadius = Mathf.Lerp(0.5f, 0f, Mathf.Pow(t, 0.85f));
            float wobble = Mathf.Lerp(ridge[side % sides], 1f, t);   // 上へ行くほど揃える
            float angle = Mathf.PI * 2f * side / sides;
            float r = baseRadius * wobble;
            return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        }

        for (int ring = 0; ring < rings; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                Vector3 a = Point(ring, side);
                Vector3 b = Point(ring, side + 1);
                Vector3 c = Point(ring + 1, side);
                Vector3 d = Point(ring + 1, side + 1);

                int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);

                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
                triangles.Add(index + 1); triangles.Add(index + 2); triangles.Add(index + 3);
            }
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    // ================================================================
    // 高架と電車
    // ================================================================

    private static void BuildRailway(Transform parent, string name, Vector3 position, float yaw,
                                     float deckHeight, float length, bool pierCollider,
                                     float waitSeconds, float startDelay, float speed,
                                     bool detailed = true)
    {
        Transform group = Group(parent, name);
        group.position = position;
        group.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Transform visual = Group(group, "Visual");

        // 橋げた（親のローカルX方向に伸びる）
        Box(visual, "Deck", new Vector3(0f, deckHeight, 0f), new Vector3(length, 1.6f, 9f), "cream");
        Box(visual, "Deck shadow line", new Vector3(0f, deckHeight - 0.85f, 0f),
            new Vector3(length, 0.3f, 9.4f), "ink");

        // 高欄（両側）
        foreach (float side in new[] { -4.2f, 4.2f })
        {
            Box(visual, "Parapet", new Vector3(0f, deckHeight + 1.5f, side),
                new Vector3(length, 1.4f, 0.4f), "cream");
            Box(visual, "Parapet cap", new Vector3(0f, deckHeight + 2.25f, side),
                new Vector3(length, 0.2f, 0.7f), "silver");
        }

        // 枕木とレール（遠い高架は枕木を省く。見えない作り込みは入れない）
        foreach (float side in new[] { -1.6f, 1.6f })
            Box(visual, "Rail", new Vector3(0f, deckHeight + 1.0f, side),
                new Vector3(length, 0.22f, 0.3f), "silver");

        if (detailed)
        {
            for (float x = -length / 2f + 4f; x <= length / 2f - 4f; x += 10f)
                Box(visual, "Sleeper", new Vector3(x, deckHeight + 0.88f, 0f),
                    new Vector3(0.6f, 0.2f, 4.4f), "wood");
        }

        // 橋脚（近い高架だけ当たり判定を付ける）
        float pierStep = detailed ? 34f : 56f;
        for (float x = -length / 2f + 18f; x <= length / 2f - 18f; x += pierStep)
        {
            // 道路や歩道の真上に橋脚が落ちないようにずらす（通行の邪魔になるため）
            if (Mathf.Abs(x) < 9f) continue;

            Transform pier = Group(visual, "Pier");
            pier.localPosition = new Vector3(x, 0f, 0f);

            Box(pier, "Column", new Vector3(0f, deckHeight / 2f, 0f),
                new Vector3(3.4f, deckHeight, 3.4f), "cream", pierCollider);
            Box(pier, "Capital", new Vector3(0f, deckHeight - 0.6f, 0f),
                new Vector3(6.4f, 1.2f, 7.2f), "cream");
            Box(pier, "Footing", new Vector3(0f, 0.4f, 0f),
                new Vector3(5.4f, 0.8f, 5.4f), "ink");
        }

        BuildTrain(group, deckHeight, waitSeconds, startDelay, speed, length, detailed);
    }

    private static void BuildTrain(Transform parent, float deckHeight,
                                   float waitSeconds, float startDelay, float speed, float length,
                                   bool detailed)
    {
        var train = new GameObject("Train");
        train.transform.SetParent(parent, false);
        train.transform.localPosition = new Vector3(-length / 2f, deckHeight + 1.1f, 0f);

        Transform visual = Group(train.transform, "Visual");

        for (int car = 0; car < 4; car++)
        {
            Transform body = Group(visual, car == 0 ? "Car (lead)" : $"Car {car + 1}");
            body.localPosition = new Vector3(-car * 19f, 0f, 0f);

            string paint = car == 0 ? "coral" : "paper";

            Box(body, "Body", new Vector3(0f, 2.2f, 0f), new Vector3(17f, 3.4f, 3.6f), paint);
            Box(body, "Roof", new Vector3(0f, 4.0f, 0f), new Vector3(16.4f, 0.5f, 3.2f), "silver");
            Box(body, "Skirt", new Vector3(0f, 0.6f, 0f), new Vector3(16.6f, 0.7f, 3.2f), "ink");
            Box(body, "Belt line", new Vector3(0f, 3.1f, 1.81f), new Vector3(16.8f, 0.35f, 0.08f), "coral");
            Box(body, "Belt line", new Vector3(0f, 3.1f, -1.81f), new Vector3(16.8f, 0.35f, 0.08f), "coral");

            // 遠くの電車は、窓や台車まで作り込まない（見えないので）
            if (!detailed)
            {
                if (car == 0)
                    Box(body, "Nose", new Vector3(9.4f, 1.9f, 0f), new Vector3(2.4f, 2.6f, 3.4f), paint);
                continue;
            }

            // 窓（両側に4枚ずつ）とドア
            for (int w = 0; w < 4; w++)
            {
                float x = -6f + w * 4f;
                foreach (float side in new[] { 1.82f, -1.82f })
                    Box(body, "Window", new Vector3(x, 2.5f, side), new Vector3(2.6f, 1.2f, 0.1f), "blue");
            }
            foreach (float side in new[] { 1.83f, -1.83f })
            {
                Box(body, "Door", new Vector3(-2f, 2.0f, side), new Vector3(1.4f, 2.6f, 0.08f), "ink");
                Box(body, "Door", new Vector3(6f, 2.0f, side), new Vector3(1.4f, 2.6f, 0.08f), "ink");
            }

            // 台車と車輪
            foreach (float x in new[] { -5.5f, 5.5f })
            {
                Box(body, "Bogie", new Vector3(x, 0.2f, 0f), new Vector3(3.6f, 0.8f, 2.6f), "ink");
                foreach (float side in new[] { 1.2f, -1.2f })
                    Cylinder(body, "Wheel", new Vector3(x, -0.1f, side), 0.8f, 0.25f, "silver");
            }

            if (car == 0)
            {
                // 先頭の鼻と前照灯
                Box(body, "Nose", new Vector3(9.4f, 1.9f, 0f), new Vector3(2.4f, 2.6f, 3.4f), paint);
                Box(body, "Windshield", new Vector3(10.4f, 2.6f, 0f), new Vector3(0.5f, 1.1f, 2.6f), "blue");
                foreach (float side in new[] { 1.0f, -1.0f })
                    Box(body, "Headlight", new Vector3(10.5f, 1.3f, side), new Vector3(0.4f, 0.4f, 0.5f), "mustard");
            }

            // パンタグラフ
            if (car == 0 || car == 3)
            {
                Box(body, "Pantograph base", new Vector3(car == 0 ? -6f : 6f, 4.35f, 0f),
                    new Vector3(1.6f, 0.2f, 1.8f), "ink");
                Box(body, "Pantograph arm", new Vector3(car == 0 ? -6f : 6f, 4.9f, 0f),
                    new Vector3(0.18f, 1.1f, 0.18f), "silver");
                Box(body, "Pantograph shoe", new Vector3(car == 0 ? -6f : 6f, 5.45f, 0f),
                    new Vector3(1.4f, 0.12f, 1.2f), "silver");
            }
        }

        var mover = train.AddComponent<BackdropTrain>();
        var serialized = new SerializedObject(mover);
        serialized.FindProperty("startX").floatValue = -length / 2f;
        serialized.FindProperty("endX").floatValue = length / 2f;
        serialized.FindProperty("speed").floatValue = speed;
        serialized.FindProperty("waitSeconds").floatValue = waitSeconds;
        serialized.FindProperty("startDelay").floatValue = startDelay;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 動かない遠景をまとめて静的にする（Unityがまとめて描けるようになり、描画負荷が下がる）。
    /// 走る電車だけは動くので対象から外す。
    /// </summary>
    private static void MarkStatic(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.GetComponentInParent<BackdropTrain>() != null) continue;

            GameObjectUtility.SetStaticEditorFlags(
                child.gameObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
        }
    }

    // ================================================================
    // 部品
    // ================================================================

    private static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Box(Transform parent, string name, Vector3 localPosition, Vector3 size,
                            string material, bool keepCollider = false)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;

        if (!keepCollider) Object.DestroyImmediate(box.GetComponent<Collider>());
        else box.layer = Mathf.Max(0, LayerMask.NameToLayer("Wall"));

        Paint(box.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }

    private static void Sphere(Transform parent, string name, Vector3 localPosition, float diameter,
                               string material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = localPosition;
        sphere.transform.localScale = Vector3.one * diameter;
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        Paint(sphere.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }

    private static void Cylinder(Transform parent, string name, Vector3 localPosition,
                                 float diameter, float thickness, string material,
                                 bool upright = false)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        // 立てる＝煙突やタンク、寝かせる＝車輪
        cylinder.transform.localRotation = upright ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
        cylinder.transform.localScale = upright
            ? new Vector3(diameter, thickness / 2f, diameter)
            : new Vector3(diameter, thickness, diameter);
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        Paint(cylinder.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }

    private static void Paint(MeshRenderer renderer, Material material)
    {
        if (material != null) renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material LoadMaterial(string id) =>
        AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{id}.mat");
}
#endif
