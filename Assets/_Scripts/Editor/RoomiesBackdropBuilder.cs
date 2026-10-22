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

        // 街路の続き（マップの道路と同じ z=0 の東西方向）
        Box(visual, "Outer road", new Vector3(0f, -0.02f, 0f),
            new Vector3(900f, 0.06f, 7.4f), "ink");
        for (float x = -440f; x <= 440f; x += 14f)
        {
            if (Mathf.Abs(x) < 42f) continue;      // 遊べる範囲の道路は既存のものを使う
            Box(visual, "Outer lane dash", new Vector3(x, 0.01f, 0f),
                new Vector3(5f, 0.02f, 0.3f), "mustard");
        }

        // 交差する道（奥行きが出る）
        foreach (float z in new[] { -120f, 120f, -260f, 260f })
            Box(visual, "Cross road", new Vector3(0f, -0.02f, z),
                new Vector3(900f, 0.06f, 7.4f), "ink");
        foreach (float x in new[] { -150f, 150f, -320f, 320f })
            Box(visual, "Cross road", new Vector3(x, -0.02f, 0f),
                new Vector3(7.4f, 0.06f, 900f), "ink");
    }

    // ================================================================
    // 外側の街並み
    // ================================================================

    private static void BuildOuterTown(Transform parent)
    {
        Transform group = Group(parent, "Outer town");
        Transform visual = Group(group, "Visual");

        string[] wallMaterials = { "cream", "paper", "coral", "teal", "pink", "mustard" };

        // 遊べる範囲のすぐ外から、遠くへ向かって建物を散らす
        for (int i = 0; i < 90; i++)
        {
            float x = Range(-420f, 420f);
            float z = Range(-420f, 420f);

            // マップの中（と道路の上）には置かない
            if (Mathf.Abs(x) < 52f && Mathf.Abs(z) < 40f) continue;
            if (Mathf.Abs(z) < 6f || Mathf.Abs(Mathf.Abs(z) - 120f) < 6f) continue;

            float distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float height = Range(6f, 10f) + distance * 0.035f;   // 遠いほど高いビルにして層を作る
            float width = Range(10f, 22f);
            float depth = Range(10f, 22f);

            string wall = wallMaterials[random.Next(wallMaterials.Length)];
            var building = Group(visual, "Outer building");
            building.position = new Vector3(x, 0f, z);
            building.localRotation = Quaternion.Euler(0f, random.Next(4) * 90f, 0f);

            Box(building, "Body", new Vector3(0f, height / 2f, 0f),
                new Vector3(width, height, depth), wall);
            Box(building, "Roof", new Vector3(0f, height + 0.2f, 0f),
                new Vector3(width + 0.8f, 0.5f, depth + 0.8f), "ink");

            // 窓の帯（近いものだけ。遠いものは省いて軽くする）
            if (distance < 150f)
            {
                int floors = Mathf.Clamp(Mathf.FloorToInt(height / 3.2f), 1, 4);
                for (int f = 0; f < floors; f++)
                {
                    float y = 2.2f + f * 3.2f;
                    if (y > height - 1f) break;
                    Box(building, "Window band", new Vector3(0f, y, depth / 2f + 0.05f),
                        new Vector3(width * 0.78f, 1.3f, 0.1f), "blue");
                    Box(building, "Window band", new Vector3(0f, y, -depth / 2f - 0.05f),
                        new Vector3(width * 0.78f, 1.3f, 0.1f), "blue");
                }
            }
        }

        // 街路樹（道沿いに点々と）
        for (int i = 0; i < 60; i++)
        {
            float x = Range(-400f, 400f);
            float z = random.Next(2) == 0 ? Range(-400f, -46f) : Range(46f, 400f);
            if (Mathf.Abs(x) < 46f && Mathf.Abs(z) < 46f) continue;

            var tree = Group(visual, "Outer tree");
            tree.position = new Vector3(x, 0f, z);

            float trunk = Range(2.4f, 4.2f);
            Box(tree, "Trunk", new Vector3(0f, trunk / 2f, 0f), new Vector3(0.6f, trunk, 0.6f), "wood");
            Sphere(tree, "Canopy", new Vector3(0f, trunk + 1.6f, 0f), Range(3.4f, 5.2f), "green");
        }
    }

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
                float angle = (360f / count) * i + Range(-9f, 9f);
                float distance = radius + Range(-45f, 45f);
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
                                 float diameter, float thickness, string material)
    {
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // 車輪として立てる
        cylinder.transform.localScale = new Vector3(diameter, thickness, diameter);
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
