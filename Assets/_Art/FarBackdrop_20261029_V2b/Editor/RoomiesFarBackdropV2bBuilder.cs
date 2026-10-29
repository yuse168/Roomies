#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 遠景 V2b（2026-10-29）。V2 の霞を弱めた版。V2 との違い：
///  ・霞の距離を「カメラから」ではなく「マップの中心からの水平距離」で測る（上空や遠くから見ても、マップの近くは霞まない）
///  ・霞の強さを 3 種に分ける：山（強い・奥ほど空に溶ける）／外の町と遠い高架（中くらい）／柵の外の地面・道路・川（ごく弱い）
///  ・マップの中（道・家・地面）はもともと霞なし（マップのマテリアルを使っているため）。V2b でも変わらない
/// 以下は V2 と同じ。今の遠景（RoomiesBackdropBuilder が作る Roomies_Backdrop）を元に、
///  ・山を、なめらかなメッシュの 3 段の山なみ（手前／中／奥が重なる）に作り直す
///  ・遠景の全部を、遠いほど空の色へ寄る軽いトゥーンのマテリアルにする
/// を行い、新しいプレハブ Roomies_Backdrop_V2b.prefab として保存する。
///
/// 規約（AGENTS.md）：
///  ・実行時には何も生成しない。プレハブとしてシーンに置く
///  ・今のアセット（Roomies_Backdrop、RoomiesArt のマテリアル、backdrop_mountain_a〜c）は上書きしない
///  ・数値はこのツールの上の定数と、マテリアルのインスペクターで直せる
///
/// メニュー：
///  Roomies/Environment/Far Backdrop V2b/Build Prefab From Scene Backdrop … シーンの Roomies_Backdrop から V2b プレハブを作る（何度実行しても同じ結果）
///  Roomies/Environment/Far Backdrop V2b/Swap In Open Scene … 開いているシーンで、旧遠景（と V2 があれば V2）を非表示にして V2b を置く（旧遠景は消さない）
/// </summary>
internal static class RoomiesFarBackdropV2bBuilder
{
    public const string Folder = "Assets/_Art/FarBackdrop_20261029_V2b";
    public const string PrefabPath = Folder + "/Roomies_Backdrop_V2b.prefab";
    const string ShaderName = "Roomies/FarBackdropToonV2b";
    const string OldRootName = "Roomies_Backdrop";
    const string NewRootName = "Roomies_Backdrop_V2b";

    // 空の地平線の色（AllSkyFree Epic_BlueSunset の地平線付近から拾った色）。霞はこの色へ寄る
    static readonly Color HazeColor = new Color(0.72f, 0.80f, 0.90f);
    static readonly Color NightHazeColor = new Color(0.08f, 0.11f, 0.20f);   // 夜空（Night の skybox）の地平線付近

    // 山のメッシュの細かさ。遠景なので控えめ（1 個 48×14 = 1,344 三角形）
    const int Sides = 48;
    const int Rings = 14;

    // 山なみ 3 段：距離・高さ・横幅の倍率・個数・色・雪線（高さの割合。1 以上で雪なし）
    struct Layer { public string name; public float distance, distanceJitter, minH, maxH, minW, maxW; public int count; public Color color; public float snow; }
    static readonly Layer[] Layers =
    {
        new Layer { name = "Near",  distance = 400f, distanceJitter = 40f, minH = 45f,  maxH = 80f,  minW = 3.0f, maxW = 3.8f, count = 18, color = new Color(0.36f, 0.56f, 0.47f), snow = 1.01f },
        new Layer { name = "Mid",   distance = 590f, distanceJitter = 50f, minH = 90f,  maxH = 150f, minW = 2.4f, maxW = 3.1f, count = 16, color = new Color(0.36f, 0.50f, 0.60f), snow = 0.80f },
        new Layer { name = "Far",   distance = 790f, distanceJitter = 30f, minH = 150f, maxH = 230f, minW = 2.0f, maxW = 2.6f, count = 14, color = new Color(0.47f, 0.57f, 0.70f), snow = 0.66f },
    };

    [MenuItem("Roomies/Environment/Far Backdrop V2b/Build Prefab From Scene Backdrop")]
    public static void BuildPrefabMenu()
    {
        var old = FindOld();
        if (old == null) { Debug.LogError("[FarBackdropV2b] シーンに Roomies_Backdrop がありません。GameRoom を開いてから実行してください。"); return; }
        BuildPrefab(old);
    }

    [MenuItem("Roomies/Environment/Far Backdrop V2b/Swap In Open Scene")]
    public static void SwapMenu()
    {
        var old = FindOld();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[FarBackdropV2b] 先に Build Prefab を実行してください。"); return; }
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == NewRootName) Undo.DestroyObjectImmediate(root);
            else if (root.name == "Roomies_Backdrop_V2") { Undo.RecordObject(root, "Hide V2"); root.SetActive(false); }   // V2 と同時に出さない
        }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
        Undo.RegisterCreatedObjectUndo(inst, "Place far backdrop V2");
        if (old != null) { Undo.RecordObject(old, "Hide old backdrop"); old.SetActive(false); }
        EditorSceneManager.MarkSceneDirty(inst.scene);
        Debug.Log("[FarBackdropV2b] V2 を置き、旧 Roomies_Backdrop を非表示にしました（消してはいません）。シーンを保存してください。");
    }

    // 遠景でも、プレイヤーのすぐ近くにある物（霞やトゥーンにすると周りのマップから浮く）
    static readonly string[] NearGroups = { "Railway near (west)" };
    static bool IsNearObject(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (System.Array.IndexOf(NearGroups, p.name) >= 0) return true;
        return false;
    }

    static GameObject FindOld()
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == OldRootName) return root;
        return null;
    }

    /// <summary>旧遠景をコピーして V2 を組み立て、プレハブにする。旧遠景そのものは変えない。</summary>
    public static GameObject BuildPrefab(GameObject old)
    {
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Meshes");

        var copy = Object.Instantiate(old);
        copy.name = NewRootName;
        copy.SetActive(true);
        try
        {
            // 1) 山を作り直す（旧 Mountains/Visual の中身を捨てて、新しい山なみに）
            Transform mountains = copy.transform.Find("Mountains");
            if (mountains == null) { mountains = new GameObject("Mountains").transform; mountains.SetParent(copy.transform, false); }
            Transform visual = mountains.Find("Visual");
            if (visual != null) Object.DestroyImmediate(visual.gameObject);
            visual = new GameObject("Visual").transform;
            visual.SetParent(mountains, false);
            BuildMountainRanges(visual);

            // 2) 残り（外の街・地面・高架・電車）は形はそのまま、マテリアルを霞つきに差し替え
            var cache = new Dictionary<string, Material>();
            foreach (var r in copy.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.transform.IsChildOf(visual)) continue;
                if (IsNearObject(r.transform)) continue;   // 家のすぐ横の高架は遠景ではないので、マップと同じマテリアルのまま
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = HazeVariant(mats[i], KindOf(r.transform), cache);
                r.sharedMaterials = mats;
            }

            // 3) 動かない物は静的バッチングの対象（電車は動くので外す）
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            {
                bool moving = false;
                for (var p = t; p != null; p = p.parent) if (p.name == "Train") { moving = true; break; }
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, moving ? 0 :
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
            AssetDatabase.SaveAssets();
            int tris = 0, rends = 0;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true)) { if (mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3; rends++; }
            Debug.Log($"[FarBackdropV2b] {PrefabPath} を保存。描画物 {rends} 個・三角形 {tris:N0}");
            return prefab;
        }
        finally { Object.DestroyImmediate(copy); }
    }

    // ================================================================ 山なみ
    static void BuildMountainRanges(Transform visual)
    {
        var meshes = new Mesh[4];
        for (int i = 0; i < meshes.Length; i++) meshes[i] = SaveMesh(CreateMountainMesh($"farbg_mountain_v2b_{(char)('a' + i)}", 7100 + i * 131), $"{Folder}/Meshes/farbg_mountain_v2b_{(char)('a' + i)}.asset");

        var rnd = new System.Random(20261029);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        foreach (var layer in Layers)
        {
            var mat = MountainMaterial(layer);
            var group = new GameObject("Range " + layer.name).transform;
            group.SetParent(visual, false);
            float offset = R(0f, 360f);
            for (int i = 0; i < layer.count; i++)
            {
                // 角度は等間隔からずらし、横幅は間隔より広く取って隣と重ねる（つながった山なみにする）
                float angle = offset + 360f / layer.count * (i + R(-0.3f, 0.3f));
                float dist = layer.distance + R(-layer.distanceJitter * 0.4f, layer.distanceJitter);
                float h = R(layer.minH, layer.maxH);
                float w = h * R(layer.minW, layer.maxW);
                var go = new GameObject($"Mountain {layer.name} {i:00}");
                go.transform.SetParent(group, false);
                go.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist + Vector3.down * 4f;
                go.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.transform.localScale = new Vector3(w, h, w * R(0.75f, 1.0f));
                go.AddComponent<MeshFilter>().sharedMesh = meshes[rnd.Next(meshes.Length)];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }
    }

    /// <summary>
    /// なめらかな山の形（単位：幅 1・高さ 1、底面の中心が原点）。
    /// 半径方向は「ふもとはなだらか・頂上は丸い」形、周方向は数本の尾根と谷、頂上の位置も少しずらす。
    /// 頂点は共有して法線をなめらかにする。UV0.y に高さの割合（シェーダーのグラデーションと雪に使う）。
    /// </summary>
    static Mesh CreateMountainMesh(string name, int seed)
    {
        var rnd = new System.Random(seed);
        float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        // 尾根：周方向の倍音（2〜7）
        var amp = new float[8]; var ph = new float[8];
        for (int k = 2; k < 8; k++) { amp[k] = Rf(0.02f, 0.09f) / (k * 0.5f); ph[k] = Rf(0f, Mathf.PI * 2f); }
        Vector2 peakShift = new Vector2(Rf(-0.08f, 0.08f), Rf(-0.08f, 0.08f));
        float shoulderAngle = Rf(0f, Mathf.PI * 2f), shoulderAmt = Rf(0.08f, 0.2f);

        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        verts.Add(new Vector3(peakShift.x, 1f, peakShift.y)); uvs.Add(new Vector2(0.5f, 1f));     // 頂上
        for (int ring = 1; ring <= Rings + 1; ring++)
        {
            float t = Mathf.Min(1f, (float)ring / Rings);                 // 0 頂上 → 1 ふもと
            for (int s = 0; s < Sides; s++)
            {
                float a = Mathf.PI * 2f * s / Sides;
                float ridge = 0f;
                for (int k = 2; k < 8; k++) ridge += amp[k] * Mathf.Sin(k * a + ph[k]);
                float radius = 0.5f * t * (1f + ridge * (0.4f + 0.6f * t));
                // 高さ：丸い頂上（cos）となだらかな裾（べき）を混ぜる。尾根の出っ張りは少し高く
                float profile = Mathf.Lerp(Mathf.Cos(t * Mathf.PI * 0.5f), Mathf.Pow(1f - t, 1.7f), 0.55f);
                float shoulder = shoulderAmt * Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(a * Mathf.Rad2Deg, shoulderAngle * Mathf.Rad2Deg) / 40f, 2f)) * Mathf.Sin(t * Mathf.PI) ;
                float y = Mathf.Clamp01(profile * (1f + ridge * 1.6f * t) + shoulder);
                if (ring == Rings + 1) y = -0.04f;                           // 地面の下へもぐる裾（端が見えないように）
                Vector2 c = Vector2.Lerp(peakShift, Vector2.zero, t);
                verts.Add(new Vector3(c.x + Mathf.Cos(a) * radius * (ring == Rings + 1 ? 1.04f : 1f), y, c.y + Mathf.Sin(a) * radius * (ring == Rings + 1 ? 1.04f : 1f)));
                uvs.Add(new Vector2((float)s / Sides, Mathf.Max(0f, y)));
            }
        }
        for (int s = 0; s < Sides; s++) { tris.Add(0); tris.Add(1 + (s + 1) % Sides); tris.Add(1 + s); }
        for (int ring = 0; ring < Rings; ring++)
        {
            int a0 = 1 + ring * Sides, b0 = 1 + (ring + 1) * Sides;
            for (int s = 0; s < Sides; s++)
            {
                int s1 = (s + 1) % Sides;
                tris.Add(a0 + s); tris.Add(a0 + s1); tris.Add(b0 + s);
                tris.Add(a0 + s1); tris.Add(b0 + s1); tris.Add(b0 + s);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        if (mesh.normals[0].y < 0f)   // 面の向きが内側なら裏返す
        {
            for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); return existing; }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ================================================================ マテリアル
    static Material MountainMaterial(Layer layer)
    {
        var m = LoadOrCreateMaterial($"{Folder}/Materials/FarBg_Mountain_{layer.name}.mat");
        m.SetColor("_BaseColor", layer.color);
        m.SetColor("_TopTint", new Color(1.18f, 1.16f, 1.10f));
        m.SetColor("_BottomTint", new Color(0.80f, 0.86f, 0.88f));
        m.SetFloat("_UseHeightGradient", 1f);
        m.SetColor("_ShadowColor", new Color(0.70f, 0.75f, 0.92f));
        m.SetFloat("_ShadeThreshold", 0.0f);
        m.SetFloat("_ShadeSoftness", 0.45f);
        m.SetFloat("_SnowLine", layer.snow);
        m.SetFloat("_SnowSoftness", 0.05f);
        SetHaze(m, MountainHaze);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 霞の強さ（マップの中心からの水平距離 m で、始まり・終わり・最大・カーブ）
    struct Haze { public float start, end, max, power; }
    static readonly Haze MountainHaze = new Haze { start = 250f, end = 950f, max = 0.80f, power = 0.9f };   // 山：奥ほど空の色に溶ける
    static readonly Haze TownHaze     = new Haze { start = 150f, end = 900f, max = 0.50f, power = 1.2f };   // 外の町・遠い高架：遠い所だけうっすら
    static readonly Haze GroundHaze   = new Haze { start = 200f, end = 950f, max = 0.30f, power = 1.4f };   // 柵の外の地面・道路・川：ほぼかけない

    static string KindOf(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (p.name == "Ground beyond the fence") return "Ground";
        return "Town";
    }

    static Material HazeVariant(Material src, string kind, Dictionary<string, Material> cache)
    {
        string key = kind + "/" + src.name;
        if (cache.TryGetValue(key, out var v)) return v;
        var m = LoadOrCreateMaterial($"{Folder}/Materials/FarBg_{kind}_{src.name}.mat");
        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.gray;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_UseHeightGradient", 0f);                    // 箱などはUVが高さではないので使わない
        m.SetColor("_ShadowColor", new Color(0.74f, 0.77f, 0.90f));
        m.SetFloat("_ShadeThreshold", 0.0f);
        m.SetFloat("_ShadeSoftness", 0.3f);
        m.SetFloat("_SnowLine", 1.01f);
        SetHaze(m, kind == "Ground" ? GroundHaze : TownHaze);
        EditorUtility.SetDirty(m);
        cache[key] = m;
        return m;
    }

    static void SetHaze(Material m, Haze h)
    {
        m.SetColor("_HazeColor", HazeColor);
        m.SetFloat("_HazeStart", h.start);
        m.SetFloat("_HazeEnd", h.end);
        m.SetFloat("_HazeMax", h.max);
        m.SetFloat("_HazePower", h.power);
        m.SetFloat("_HazeWorldRadius", 1f);                 // マップの中心からの距離で霞ませる
        m.SetVector("_HazeCenter", Vector4.zero);           // 遊べる範囲の中心（x ±39.3 / z ±24 の真ん中）
        // 夜（DayManager が太陽を 0.12 に落とす）の暗さと地平線の色
        m.SetFloat("_DayLightLuminance", 1.0f);
        m.SetColor("_NightHazeColor", NightHazeColor);
        m.SetFloat("_NightBrightness", 0.06f);
    }

    static Material LoadOrCreateMaterial(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find(ShaderName)) { enableInstancing = true };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
#endif
