#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 遠景（山なみ・海上の高架を走る電車）をシーンに置くツール。
///
/// 奥行きを出すための飾り。当たり判定も通信も付けない。
///
/// 規約（AGENTS.md）：
///  ・実行時には何も生成しない。ここで実体をシーンへ置く
///  ・見た目は `Visual` の下にまとめ、本アセットが来たら中身を差し替えるだけで済むようにする
///  ・位置・大きさ・速さはインスペクターとシーンビューで直せる
///
/// メニュー：
///  Roomies/Environment/Build Distant Backdrop
///  Roomies/Environment/Remove Distant Backdrop
/// </summary>
internal static class RoomiesBackdropBuilder
{
    private const string RootName = "Roomies_Backdrop";
    private const string MaterialFolder = "Assets/Resources/RoomiesArt/Materials";
    private const string MeshPath = "Assets/_Prefabs/Backdrop/backdrop_mountain.asset";

    // 街のだいたいの中心。山はこの周りに円を描くように置く。
    private static readonly Vector3 MapCenter = new Vector3(18f, 0f, -3f);

    [MenuItem("Roomies/Environment/Build Distant Backdrop")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Backdrop] Play Modeを抜けてから実行してください。");
            return;
        }

        Remove(silent: true);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build distant backdrop");

        BuildMountains(root.transform);

        // 北側（街の奥）と東側（海の上）の2本。どこから見ても電車が通る。
        BuildRailway(root.transform, "Railway north",
            new Vector3(30f, 0f, -120f), 0f, 14f, 4f);
        BuildRailway(root.transform, "Railway east",
            new Vector3(170f, 0f, 0f), 90f, 22f, 26f);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[Backdrop] 遠景（山なみと電車）を置きました。位置はシーンビューで調整できます（シーンを保存してください）");
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
    // 山なみ
    // ================================================================

    private static void BuildMountains(Transform parent)
    {
        Mesh cone = LoadOrCreateConeMesh();

        Transform group = Group(parent, "Mountains");
        Transform visual = Group(group, "Visual");

        // 手前の列は濃く、奥の列は青くして霞ませる（空気遠近）
        (float radius, float minHeight, float maxHeight, string material, int count)[] rings =
        {
            (210f, 38f,  62f,  "teal",  11),
            (300f, 70f, 110f,  "blue",   9),
            (420f, 95f, 150f,  "blue",   7),
        };

        var random = new System.Random(20261022);

        foreach ((float radius, float minHeight, float maxHeight, string material, int count) in rings)
        {
            Material tint = LoadMaterial(material);

            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i + (float)random.NextDouble() * 12f;
                float distance = radius + (float)random.NextDouble() * 40f - 20f;
                float height = Mathf.Lerp(minHeight, maxHeight, (float)random.NextDouble());
                float width = height * Mathf.Lerp(1.3f, 2.1f, (float)random.NextDouble());

                Vector3 position = MapCenter + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                position.y = -4f;   // 海面より少し下から生やす

                var mountain = new GameObject($"Mountain {Mathf.RoundToInt(radius)}-{i}");
                mountain.transform.SetParent(visual, false);
                mountain.transform.position = position;
                mountain.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                mountain.transform.localScale = new Vector3(width, height, width);

                mountain.AddComponent<MeshFilter>().sharedMesh = cone;
                var renderer = mountain.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = tint;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    /// <summary>山用の円錐メッシュ。無ければ作って資産として保存する。</summary>
    private static Mesh LoadOrCreateConeMesh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing != null) return existing;

        System.IO.Directory.CreateDirectory("Assets/_Prefabs/Backdrop");

        const int sides = 10;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        // 側面（面ごとに頂点を分けてローポリの陰影を出す）
        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.PI * 2f * i / sides;
            float a1 = Mathf.PI * 2f * (i + 1) / sides;

            Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
            Vector3 top = new Vector3(0f, 1f, 0f);

            int index = vertices.Count;
            vertices.Add(b0);
            vertices.Add(b1);
            vertices.Add(top);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 1);
        }

        var mesh = new Mesh { name = "backdrop_mountain" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, MeshPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    // ================================================================
    // 海上の高架と電車
    // ================================================================

    private static void BuildRailway(Transform parent, string name, Vector3 position,
                                     float yaw, float waitSeconds, float startDelay)
    {
        Transform group = Group(parent, name);
        group.position = position;
        group.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Transform visual = Group(group, "Visual");

        const float y = 10f;        // 海面より上
        const float length = 620f;

        // 橋げた（親のローカルX方向に伸びる）
        Box(visual, "Viaduct deck", new Vector3(0f, y, 0f), new Vector3(length, 2.2f, 7f), "cream");
        Box(visual, "Rail", new Vector3(0f, y + 1.4f, -1.4f), new Vector3(length, 0.3f, 0.5f), "silver");
        Box(visual, "Rail", new Vector3(0f, y + 1.4f, 1.4f), new Vector3(length, 0.3f, 0.5f), "silver");

        // 橋脚
        for (float x = -length / 2f + 20f; x <= length / 2f - 20f; x += 60f)
            Box(visual, "Pier", new Vector3(x, y / 2f - 3f, 0f), new Vector3(6f, y + 6f, 6f), "cream");

        // 電車（この親ごと動かす）
        var train = new GameObject("Train");
        train.transform.SetParent(group, false);
        train.transform.localPosition = new Vector3(-300f, y + 3.2f, 0f);

        Transform trainVisual = Group(train.transform, "Visual");

        // 先頭車両は鼻先を付けて、遠目でも進行方向が分かるようにする
        Box(trainVisual, "Car 1", new Vector3(0f, 0f, 0f), new Vector3(16f, 4.4f, 4.4f), "coral");
        Box(trainVisual, "Nose", new Vector3(9f, -0.4f, 0f), new Vector3(3f, 3.4f, 4.2f), "coral");
        for (int i = 1; i <= 3; i++)
            Box(trainVisual, $"Car {i + 1}", new Vector3(-18f * i, 0f, 0f), new Vector3(16f, 4.4f, 4.4f), "paper");

        // 窓の帯（両側）
        foreach (float side in new[] { -2.25f, 2.25f })
        {
            Box(trainVisual, "Window band", new Vector3(0f, 0.6f, side), new Vector3(14f, 1.4f, 0.1f), "ink");
            for (int i = 1; i <= 3; i++)
                Box(trainVisual, "Window band", new Vector3(-18f * i, 0.6f, side), new Vector3(14f, 1.4f, 0.1f), "ink");
        }

        var mover = train.AddComponent<BackdropTrain>();
        var serialized = new SerializedObject(mover);
        serialized.FindProperty("startX").floatValue = -300f;
        serialized.FindProperty("endX").floatValue = 300f;
        serialized.FindProperty("waitSeconds").floatValue = waitSeconds;
        serialized.FindProperty("startDelay").floatValue = startDelay;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ================================================================
    // 部品
    // ================================================================

    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Box(Transform parent, string name, Vector3 localPosition, Vector3 size, string material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;

        // 遠景なので当たり判定も影も要らない
        Object.DestroyImmediate(box.GetComponent<Collider>());

        var renderer = box.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = LoadMaterial(material);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material LoadMaterial(string id) =>
        AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{id}.mat");
}
#endif
