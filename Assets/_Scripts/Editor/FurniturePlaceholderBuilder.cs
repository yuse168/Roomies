#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 家具の仮アセットをプリミティブから組み立て直すツール。
///
/// 以前の仮アセットは頂点数こそ多かったが、面が細分化されているだけで
/// 見た目は無地の板だった（自販機はボタンも窓も無い赤い箱）。
/// ここでは「遠目でも何の家具か分かる」ことだけを狙い、
/// シルエットと特徴パーツを数個のプリミティブで作る。
///
/// 規約（AGENTS.md）：
///  ・家具の見た目プレハブは NetworkFurniture が `Visual` という名前の子として生成する。
///    そのためこのプレハブ自体が「見た目」であり、中に Visual を作らない。
///  ・正面は +Z。
///  ・原点は中心のまま（既存の設置位置計算と合わせるため。足元原点にすると全部ずれる）。
///  ・当たり判定はルートの BoxCollider ひとつだけ。飾りの子には付けない。
///
/// メニュー：Roomies/Furniture/Rebuild Placeholder Art
/// </summary>
internal static class FurniturePlaceholderBuilder
{
    private const string OutputFolder = "Assets/Resources/RoomiesArt/Furniture";
    private const string MaterialFolder = "Assets/Resources/RoomiesArt/Materials";

    private static readonly Dictionary<string, Material> MaterialCache = new();

    [MenuItem("Roomies/Furniture/Rebuild Placeholder Art")]
    private static void RebuildAll()
    {
        MaterialCache.Clear();
        int built = 0;

        foreach (FurnitureItem item in FurnitureCatalog.Items)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;

            GameObject root = Build(item);
            if (root == null) continue;

            string path = $"{OutputFolder}/{item.id}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            built++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Furniture] 仮アセットを{built}件つくり直しました → {OutputFolder}");
    }

    // ================================================================
    // 家具ごとの組み立て
    // ================================================================

    private static GameObject Build(FurnitureItem item)
    {
        var root = new GameObject(item.id);
        Vector3 s = item.placeholderSize;

        switch (item.id)
        {
            case "coffee_maker":       BuildCoffeeMaker(root, s); break;
            case "energy_drink_fridge":BuildFridge(root, s); break;
            case "comfy_bed":          BuildBed(root, s); break;
            case "welcome_mat":        BuildMat(root, s); break;
            case "piggy_bank":         BuildPiggyBank(root, s); break;
            case "wall_clock":         BuildWallClock(root, s); break;
            case "delivery_terminal":  BuildTerminal(root, s); break;
            case "lucky_cat":          BuildLuckyCat(root, s); break;
            case "vending_machine":    BuildVendingMachine(root, s); break;
            case "disco_ball":         BuildDiscoBall(root, s); break;
            case "trampoline":         BuildTrampoline(root, s); break;
            case "arcade_machine":     BuildArcadeMachine(root, s); break;
            default:
                Box(root, "Body", Vector3.zero, s, "silver");
                break;
        }

        // 当たり判定はルートに1つだけ。カタログのサイズに合わせる。
        var collider = root.AddComponent<BoxCollider>();
        collider.size = s;
        collider.center = Vector3.zero;
        return root;
    }

    private static void BuildCoffeeMaker(GameObject r, Vector3 s)
    {
        Box(r, "Body",   new Vector3(0, 0, -0.06f * s.z), new Vector3(s.x, s.y, s.z * 0.55f), "ink");
        Box(r, "Tank",   new Vector3(0, s.y * 0.22f, 0.12f * s.z), new Vector3(s.x * 0.62f, s.y * 0.42f, s.z * 0.34f), "teal");
        Box(r, "Spout",  new Vector3(0, s.y * 0.02f, 0.16f * s.z), new Vector3(s.x * 0.16f, s.y * 0.12f, s.z * 0.18f), "silver");
        Box(r, "Carafe", new Vector3(0, -s.y * 0.30f, 0.17f * s.z), new Vector3(s.x * 0.56f, s.y * 0.32f, s.z * 0.40f), "cream");
        Box(r, "Plate",  new Vector3(0, -s.y * 0.46f, 0.17f * s.z), new Vector3(s.x * 0.66f, s.y * 0.05f, s.z * 0.46f), "silver");
        Box(r, "Button", new Vector3(s.x * 0.28f, s.y * 0.30f, 0.26f * s.z), new Vector3(s.x * 0.14f, s.y * 0.07f, s.z * 0.05f), "coral");
    }

    private static void BuildFridge(GameObject r, Vector3 s)
    {
        Box(r, "Body",   Vector3.zero, s, "teal");
        // ガラス扉。暗い面を少し手前に出して「中が見える冷蔵庫」に見せる
        Box(r, "Glass",  new Vector3(0, s.y * 0.04f, s.z * 0.46f), new Vector3(s.x * 0.78f, s.y * 0.74f, s.z * 0.06f), "screen");
        Box(r, "Handle", new Vector3(-s.x * 0.40f, s.y * 0.04f, s.z * 0.50f), new Vector3(s.x * 0.06f, s.y * 0.34f, s.z * 0.06f), "silver");
        Box(r, "Top",    new Vector3(0, s.y * 0.47f, 0), new Vector3(s.x * 1.02f, s.y * 0.07f, s.z * 1.02f), "ink");

        // 棚と缶
        for (int shelf = 0; shelf < 3; shelf++)
        {
            float y = s.y * (0.26f - shelf * 0.22f);
            Box(r, $"Shelf{shelf}", new Vector3(0, y - s.y * 0.07f, s.z * 0.44f),
                new Vector3(s.x * 0.74f, s.y * 0.015f, s.z * 0.05f), "silver");
            for (int c = 0; c < 3; c++)
            {
                float x = s.x * (-0.22f + c * 0.22f);
                Cyl(r, $"Can{shelf}_{c}", new Vector3(x, y, s.z * 0.42f),
                    s.x * 0.13f, s.y * 0.11f, c % 2 == 0 ? "coral" : "mustard");
            }
        }
    }

    private static void BuildBed(GameObject r, Vector3 s)
    {
        Box(r, "Frame",     new Vector3(0, -s.y * 0.26f, 0), new Vector3(s.x, s.y * 0.42f, s.z), "wood");
        Box(r, "Mattress",  new Vector3(0, s.y * 0.16f, 0), new Vector3(s.x * 0.94f, s.y * 0.46f, s.z * 0.96f), "cream");
        Box(r, "Blanket",   new Vector3(0, s.y * 0.30f, -s.z * 0.18f), new Vector3(s.x * 0.96f, s.y * 0.22f, s.z * 0.60f), "coral");
        Box(r, "Pillow",    new Vector3(0, s.y * 0.36f, s.z * 0.36f), new Vector3(s.x * 0.60f, s.y * 0.22f, s.z * 0.16f), "paper");
        Box(r, "Headboard", new Vector3(0, s.y * 0.30f, s.z * 0.50f), new Vector3(s.x, s.y * 1.1f, s.z * 0.05f), "wood");
        float legY = -s.y * 0.60f;
        foreach (var p in Corners(s.x * 0.42f, s.z * 0.44f))
            Box(r, "Leg", new Vector3(p.x, legY, p.y), new Vector3(s.x * 0.09f, s.y * 0.26f, s.z * 0.05f), "ink");
    }

    private static void BuildMat(GameObject r, Vector3 s)
    {
        Box(r, "Mat",    Vector3.zero, s, "wood");
        Box(r, "Inner",  new Vector3(0, s.y * 0.30f, 0), new Vector3(s.x * 0.80f, s.y * 0.5f, s.z * 0.70f), "mustard");
        Box(r, "Stripe", new Vector3(0, s.y * 0.55f, 0), new Vector3(s.x * 0.52f, s.y * 0.4f, s.z * 0.12f), "cream");
    }

    private static void BuildPiggyBank(GameObject r, Vector3 s)
    {
        Sph(r, "Body",  Vector3.zero, new Vector3(s.x, s.y, s.z * 0.86f), "pink");
        Sph(r, "Snout", new Vector3(0, -s.y * 0.04f, s.z * 0.42f), new Vector3(s.x * 0.34f, s.y * 0.30f, s.z * 0.22f), "coral");
        Box(r, "Slot",  new Vector3(0, s.y * 0.46f, 0), new Vector3(s.x * 0.42f, s.y * 0.04f, s.z * 0.08f), "ink");
        Sph(r, "EarL",  new Vector3(-s.x * 0.22f, s.y * 0.36f, s.z * 0.20f), new Vector3(s.x * 0.18f, s.y * 0.18f, s.z * 0.06f), "coral");
        Sph(r, "EarR",  new Vector3(s.x * 0.22f, s.y * 0.36f, s.z * 0.20f), new Vector3(s.x * 0.18f, s.y * 0.18f, s.z * 0.06f), "coral");
        foreach (var p in Corners(s.x * 0.26f, s.z * 0.22f))
            Cyl(r, "Leg", new Vector3(p.x, -s.y * 0.46f, p.y), s.x * 0.11f, s.y * 0.10f, "coral");
    }

    private static void BuildWallClock(GameObject r, Vector3 s)
    {
        // 時計盤は Z 方向に薄い円盤。正面(+Z)を向く
        Cyl(r, "Rim",   Vector3.zero, s.x, s.z * 0.5f, "ink", true);
        Cyl(r, "Face",  new Vector3(0, 0, s.z * 0.18f), s.x * 0.84f, s.z * 0.30f, "paper", true);
        Box(r, "HandH", new Vector3(0, s.y * 0.13f, s.z * 0.38f), new Vector3(s.x * 0.05f, s.y * 0.28f, s.z * 0.06f), "ink");
        Box(r, "HandM", new Vector3(s.x * 0.17f, 0, s.z * 0.38f), new Vector3(s.x * 0.38f, s.y * 0.04f, s.z * 0.06f), "coral");
        Cyl(r, "Pin",   new Vector3(0, 0, s.z * 0.44f), s.x * 0.08f, s.z * 0.10f, "mustard", true);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            Box(r, $"Tick{i}",
                new Vector3(Mathf.Sin(a) * s.x * 0.34f, Mathf.Cos(a) * s.y * 0.34f, s.z * 0.36f),
                new Vector3(s.x * 0.05f, s.y * 0.08f, s.z * 0.05f), "ink");
        }
    }

    private static void BuildTerminal(GameObject r, Vector3 s)
    {
        Box(r, "Base",   new Vector3(0, -s.y * 0.42f, 0), new Vector3(s.x * 1.05f, s.y * 0.16f, s.z * 1.05f), "ink");
        Box(r, "Body",   Vector3.zero, new Vector3(s.x, s.y * 0.92f, s.z * 0.7f), "blue");
        Box(r, "Screen", new Vector3(0, s.y * 0.22f, s.z * 0.37f), new Vector3(s.x * 0.76f, s.y * 0.34f, s.z * 0.06f), "screen");
        Box(r, "Keypad", new Vector3(0, -s.y * 0.10f, s.z * 0.36f), new Vector3(s.x * 0.56f, s.y * 0.18f, s.z * 0.05f), "silver");
        Box(r, "Slot",   new Vector3(0, -s.y * 0.30f, s.z * 0.36f), new Vector3(s.x * 0.64f, s.y * 0.05f, s.z * 0.05f), "ink");
        Box(r, "Light",  new Vector3(0, s.y * 0.44f, s.z * 0.20f), new Vector3(s.x * 0.30f, s.y * 0.05f, s.z * 0.10f), "green");
    }

    private static void BuildLuckyCat(GameObject r, Vector3 s)
    {
        Box(r, "Base", new Vector3(0, -s.y * 0.44f, 0), new Vector3(s.x * 0.86f, s.y * 0.12f, s.z * 0.86f), "mustard");
        Sph(r, "Body", new Vector3(0, -s.y * 0.14f, 0), new Vector3(s.x * 0.86f, s.y * 0.56f, s.z * 0.80f), "paper");
        Sph(r, "Head", new Vector3(0, s.y * 0.26f, 0), new Vector3(s.x * 0.66f, s.y * 0.40f, s.z * 0.62f), "paper");
        Box(r, "EarL", new Vector3(-s.x * 0.20f, s.y * 0.44f, 0), new Vector3(s.x * 0.16f, s.y * 0.12f, s.z * 0.10f), "paper");
        Box(r, "EarR", new Vector3(s.x * 0.20f, s.y * 0.44f, 0), new Vector3(s.x * 0.16f, s.y * 0.12f, s.z * 0.10f), "paper");
        Box(r, "Paw",  new Vector3(-s.x * 0.34f, s.y * 0.10f, s.z * 0.10f), new Vector3(s.x * 0.16f, s.y * 0.30f, s.z * 0.16f), "paper");
        Box(r, "Bib",  new Vector3(0, s.y * 0.02f, s.z * 0.30f), new Vector3(s.x * 0.40f, s.y * 0.10f, s.z * 0.08f), "coral");
        Box(r, "Coin", new Vector3(0, -s.y * 0.16f, s.z * 0.33f), new Vector3(s.x * 0.34f, s.y * 0.18f, s.z * 0.06f), "mustard");
    }

    private static void BuildVendingMachine(GameObject r, Vector3 s)
    {
        Box(r, "Body",    Vector3.zero, s, "coral");
        Box(r, "Window",  new Vector3(-s.x * 0.12f, s.y * 0.14f, s.z * 0.46f), new Vector3(s.x * 0.58f, s.y * 0.62f, s.z * 0.06f), "screen");
        Box(r, "Panel",   new Vector3(s.x * 0.33f, s.y * 0.14f, s.z * 0.46f), new Vector3(s.x * 0.26f, s.y * 0.62f, s.z * 0.06f), "ink");
        Box(r, "Tray",    new Vector3(0, -s.y * 0.34f, s.z * 0.46f), new Vector3(s.x * 0.70f, s.y * 0.14f, s.z * 0.07f), "ink");
        Box(r, "Sign",    new Vector3(0, s.y * 0.44f, s.z * 0.46f), new Vector3(s.x * 0.86f, s.y * 0.10f, s.z * 0.07f), "mustard");

        // 窓の中に並ぶ缶（3段×3列）
        for (int row = 0; row < 3; row++)
        {
            for (int c = 0; c < 3; c++)
            {
                Cyl(r, $"Can{row}_{c}",
                    new Vector3(s.x * (-0.29f + c * 0.17f), s.y * (0.33f - row * 0.19f), s.z * 0.44f),
                    s.x * 0.12f, s.y * 0.07f, (row + c) % 2 == 0 ? "teal" : "mustard");
            }
        }
        // 選択ボタン
        for (int b = 0; b < 3; b++)
            Box(r, $"Btn{b}", new Vector3(s.x * 0.33f, s.y * (0.30f - b * 0.16f), s.z * 0.50f),
                new Vector3(s.x * 0.16f, s.y * 0.06f, s.z * 0.04f), "cream");
    }

    private static void BuildDiscoBall(GameObject r, Vector3 s)
    {
        Sph(r, "Core", Vector3.zero, s * 0.92f, "silver");
        Box(r, "Rod",  new Vector3(0, s.y * 0.62f, 0), new Vector3(s.x * 0.06f, s.y * 0.34f, s.z * 0.06f), "ink");

        // 鏡のタイル。球面に並べて「ミラーボール」のシルエットを出す
        const int rings = 5, perRing = 10;
        for (int i = 0; i < rings; i++)
        {
            float phi = Mathf.PI * (i + 0.5f) / rings;
            float y = Mathf.Cos(phi) * s.y * 0.47f;
            float rad = Mathf.Sin(phi) * s.x * 0.47f;
            for (int j = 0; j < perRing; j++)
            {
                float th = Mathf.PI * 2f * j / perRing;
                Box(r, $"Tile{i}_{j}",
                    new Vector3(Mathf.Cos(th) * rad, y, Mathf.Sin(th) * rad),
                    new Vector3(s.x * 0.12f, s.y * 0.12f, s.z * 0.12f),
                    (i + j) % 3 == 0 ? "cream" : "blue");
            }
        }
    }

    private static void BuildTrampoline(GameObject r, Vector3 s)
    {
        Cyl(r, "Mat",   new Vector3(0, s.y * 0.20f, 0), s.x * 0.78f, s.y * 0.10f, "ink");
        Cyl(r, "Frame", new Vector3(0, s.y * 0.16f, 0), s.x * 0.98f, s.y * 0.16f, "teal");
        Cyl(r, "Pad",   new Vector3(0, s.y * 0.30f, 0), s.x * 0.94f, s.y * 0.06f, "coral");
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI * 2f * i / 6f;
            Cyl(r, $"Leg{i}",
                new Vector3(Mathf.Cos(a) * s.x * 0.40f, -s.y * 0.30f, Mathf.Sin(a) * s.z * 0.40f),
                s.x * 0.06f, s.y * 0.42f, "silver");
        }
    }

    private static void BuildArcadeMachine(GameObject r, Vector3 s)
    {
        Box(r, "Cabinet",  new Vector3(0, -s.y * 0.10f, 0), new Vector3(s.x, s.y * 0.80f, s.z), "blue");
        Box(r, "Marquee",  new Vector3(0, s.y * 0.42f, s.z * 0.10f), new Vector3(s.x * 0.96f, s.y * 0.16f, s.z * 0.70f), "pink");
        Box(r, "Screen",   new Vector3(0, s.y * 0.20f, s.z * 0.34f), new Vector3(s.x * 0.74f, s.y * 0.26f, s.z * 0.08f), "screen");
        Box(r, "Deck",     new Vector3(0, -s.y * 0.02f, s.z * 0.44f), new Vector3(s.x * 0.90f, s.y * 0.08f, s.z * 0.26f), "ink");
        Cyl(r, "Stick",    new Vector3(-s.x * 0.18f, s.y * 0.05f, s.z * 0.44f), s.x * 0.05f, s.y * 0.05f, "silver");
        Sph(r, "Ball",     new Vector3(-s.x * 0.18f, s.y * 0.09f, s.z * 0.44f), Vector3.one * s.x * 0.12f, "coral");
        Box(r, "BtnA",     new Vector3(s.x * 0.10f, s.y * 0.03f, s.z * 0.44f), new Vector3(s.x * 0.12f, s.y * 0.02f, s.z * 0.10f), "mustard");
        Box(r, "BtnB",     new Vector3(s.x * 0.26f, s.y * 0.03f, s.z * 0.44f), new Vector3(s.x * 0.12f, s.y * 0.02f, s.z * 0.10f), "green");
        Box(r, "Coin",     new Vector3(0, -s.y * 0.24f, s.z * 0.50f), new Vector3(s.x * 0.26f, s.y * 0.10f, s.z * 0.04f), "silver");
    }

    // ================================================================
    // プリミティブ生成ヘルパー
    // ================================================================

    private static GameObject Primitive(
        GameObject parent, PrimitiveType type, string name, Vector3 center, Vector3 size, string material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        // 飾りの子に当たり判定は要らない（判定はルートのBoxColliderだけ）
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);

        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = Mat(material);
        return go;
    }

    private static void Box(GameObject p, string n, Vector3 c, Vector3 s, string m)
        => Primitive(p, PrimitiveType.Cube, n, c, s, m);

    private static void Sph(GameObject p, string n, Vector3 c, Vector3 s, string m)
        => Primitive(p, PrimitiveType.Sphere, n, c, s, m);

    /// <summary>円柱。Unityの円柱は高さが2なので scale.y は半分の値を渡す。</summary>
    private static void Cyl(
        GameObject p, string n, Vector3 c, float diameter, float halfHeight, string m, bool faceZ = false)
    {
        GameObject go = Primitive(p, PrimitiveType.Cylinder, n, c,
            new Vector3(diameter, halfHeight, diameter), m);
        if (faceZ) go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static IEnumerable<Vector2> Corners(float x, float z)
    {
        yield return new Vector2(-x, -z);
        yield return new Vector2(x, -z);
        yield return new Vector2(-x, z);
        yield return new Vector2(x, z);
    }

    private static Material Mat(string name)
    {
        if (MaterialCache.TryGetValue(name, out Material cached)) return cached;
        var m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
        if (m == null) Debug.LogWarning($"[Furniture] マテリアルが見つかりません: {name}");
        MaterialCache[name] = m;
        return m;
    }
}
#endif
