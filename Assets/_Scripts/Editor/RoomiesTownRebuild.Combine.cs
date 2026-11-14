#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 町の軽量化（2026/11/13）：見た目だけの部品を、素材ごと・区画ごとに1つのメッシュにまとめる。何度実行しても同じ結果。
/// ・対象は、町の作り直しで置いた部品（Town facades / houses / rooftops / street props / night lights の灯具）と、
///   GameRoom の遠景（Roomies_Backdrop の Ground beyond the fence・Outer town・Mountains）。
/// ・まとめるのは「Transform・MeshFilter・MeshRenderer だけ」の物。光・当たり判定・スクリプトの付いた物、鉱山、電車の高架は触らない。
/// ・小さな部品（いちばん長い辺が ShadowMinSize 未満、ガラス、光る面）は影を落とさない。遠景は影を落とさない（影の届く 50m の外がほとんど）。
/// ・遠景の地面の続きは、町の地面の下を空けた形で作り直してからまとめる（RoomiesBackdropBuilder.RebuildGround）。
/// ・まとめたメッシュは Assets/_Art/Restyle/Town/Combined/〜.asset。まとめた物は各グループの「Combined」の下。
/// ・作り直すときは、各配置ツール（1〜3、遠景の Build Distant Backdrop）が部品を置き直したあとに、ここを呼び直す。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string CombinedDir = "Assets/_Art/Restyle/Town/Combined";
    const string CombinedName = "Combined";
    const float ShadowMinSize = .9f;
    static readonly string[] PrefabGroups = { FacadeGroup, HouseGroup, RooftopGroup, PropGroup, NightLightGroup, "Town jail", "Town main house", "Town casino", "Town road", EnclosureGroup, FootbridgeGroup };
    static readonly string[] BackdropGroups = { "Ground beyond the fence", "Outer town", "Mountains" };

    [MenuItem("Roomies/Art/Town/9 Combine For Speed")]
    internal static void CombineForSpeed()
    {
        int parts = 0, made = 0;
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            foreach (var name in PrefabGroups)
            {
                var g = root.transform.Find(name);
                if (g == null) continue;
                var (p, m) = CombineUnder(g, Slug(name), 24f, true);
                parts += p; made += m;
            }
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var (bp, bm) = CombineBackdrop(true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Town] combined: town {parts} parts -> {made} meshes, backdrop {bp} parts -> {bm} meshes");
    }

    /// <summary>遠景をまとめる（遠景の配置ツールの最後にも呼ぶ）。</summary>
    internal static (int parts, int made) CombineBackdrop(bool save)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var back = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_Backdrop");
        if (back == null) return (0, 0);
        // 地面の続きは、町の地面の下を空けた形で作り直してから素材の型をかける（同じ所を二重に描かないように）
        var ground = RoomiesBackdropBuilder.RebuildGround(back.transform);
        RoomiesMaterialFinishBuilder.ApplyTownUnder(ground, new Dictionary<string, Material>());
        int parts = 0, made = 0;
        foreach (var name in BackdropGroups)
        {
            var g = back.transform.Find(name);
            if (g == null) continue;
            var (p, m) = CombineUnder(g, "backdrop_" + Slug(name), 90f, false);
            parts += p; made += m;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (save) EditorSceneManager.SaveScene(scene);
        return (parts, made);
    }

    static string Slug(string s) => new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    /// <summary>group の下の見た目だけの部品を、素材・区画・影の有無ごとに1つにまとめる。前にまとめた物もまとめ直す。</summary>
    static (int parts, int made) CombineUnder(Transform group, string assetName, float cell, bool shadows)
    {
        var candidates = group.GetComponentsInChildren<MeshRenderer>(true).Where(r => Combinable(r, group)).ToList();
        if (candidates.Count == 0) return (0, 0);
        var toLocal = group.worldToLocalMatrix;
        var buckets = new Dictionary<(Material mat, int cx, int cz, bool shadow), List<CombineInstance>>();
        foreach (var r in candidates)
        {
            var mf = r.GetComponent<MeshFilter>();
            var mat = r.sharedMaterial;
            Vector3 c = toLocal.MultiplyPoint3x4(r.bounds.center);
            bool already = r.transform.parent != null && r.transform.parent.name == CombinedName;
            bool shadow = shadows && (already ? r.shadowCastingMode != ShadowCastingMode.Off : CastsShadow(r, mat));
            // cell が 0 以下なら区画に分けない（素材と影の有無だけで分ける）
            var key = cell > 0 ? (mat, Mathf.FloorToInt(c.x / cell), Mathf.FloorToInt(c.z / cell), shadow) : (mat, 0, 0, shadow);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<CombineInstance>();
            // まとめ直しのときは、まとめた物を1つの部品として数える（区画は中心で決まるので同じ所に入る）
            list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = 0, transform = toLocal * r.localToWorldMatrix });
        }

        // 新しいメッシュを先に作る（前のアセットのメッシュを読むので、消すのはそのあと）
        var meshes = new List<(Mesh mesh, Material mat, bool shadow)>();
        foreach (var kv in buckets.OrderBy(k => k.Key.mat.name).ThenBy(k => k.Key.cx).ThenBy(k => k.Key.cz).ThenBy(k => k.Key.shadow))
        {
            var mesh = new Mesh { name = $"{kv.Key.mat.name}_{kv.Key.cx}_{kv.Key.cz}{(kv.Key.shadow ? "" : "_ns")}", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true, false);
            mesh.RecalculateBounds();
            meshes.Add((mesh, kv.Key.mat, kv.Key.shadow));
        }

        // 元の部品を消す（MeshFilter と MeshRenderer を外し、空になった物を消す。寸法の印は残す）
        foreach (var r in candidates)
        {
            var go = r.gameObject;
            Object.DestroyImmediate(r);
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        }
        var old = group.Find(CombinedName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        RemoveEmpty(group);

        // アセットに書く（前のアセットは消して作り直す）
        Directory.CreateDirectory(CombinedDir);
        string path = $"{CombinedDir}/{assetName}.asset";
        AssetDatabase.DeleteAsset(path);
        var holder = new Mesh { name = assetName };
        AssetDatabase.CreateAsset(holder, path);
        foreach (var m in meshes) AssetDatabase.AddObjectToAsset(m.mesh, path);

        var host = new GameObject(CombinedName) { isStatic = true }.transform;
        host.SetParent(group, false);
        foreach (var (mesh, mat, shadow) in meshes)
        {
            var go = new GameObject(CombinedName + " - " + mesh.name) { isStatic = true };
            go.transform.SetParent(host, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        return (candidates.Count, meshes.Count);
    }

    static bool Combinable(MeshRenderer r, Transform group)
    {
        if (RoomiesMaterialFinishBuilder.InMining(r.transform)) return false;
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount != 1) return false;
        if (r.sharedMaterials.Length != 1 || r.sharedMaterial == null) return false;
        if (!r.enabled || !r.gameObject.activeInHierarchy) return false;
        // LOD の付いた物（木など）と、ほかのプレハブの中身はまとめない（距離で細かさを変えるため）
        if (r.GetComponentInParent<LODGroup>(true) != null || PrefabUtility.IsPartOfPrefabInstance(r)) return false;
        // Transform・MeshFilter・MeshRenderer 以外が付いている物（光・当たり判定・スクリプトなど）はまとめない
        if (!r.GetComponents<Component>().All(c => c is Transform || c is MeshFilter || c is MeshRenderer)) return false;
        // 親にスクリプトなどが付いている物（回る・動く物の部品）もまとめない
        for (var p = r.transform.parent; p != null && p != group.parent; p = p.parent)
            if (p.GetComponents<Component>().Any(c => c is MonoBehaviour || c is Animator || c is Animation || c is Rigidbody)) return false;
        return true;
    }

    static bool CastsShadow(MeshRenderer r, Material mat)
    {
        if (r.shadowCastingMode == ShadowCastingMode.Off) return false;
        string n = mat.name;
        if (n.Contains("glass") || n.Contains("window") || n.Contains("bulb") || n.Contains("interior")) return false;
        var s = r.bounds.size;
        return Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= ShadowMinSize;
    }

    /// <summary>部品がなくなった空の物を、葉から順に消す（寸法の印「Spec 」と、名前のある親で中身の残る物は残る）。</summary>
    static void RemoveEmpty(Transform t)
    {
        foreach (Transform c in t.Cast<Transform>().ToArray()) RemoveEmpty(c);
        foreach (Transform c in t.Cast<Transform>().ToArray())
            if (c.childCount == 0 && c.GetComponents<Component>().Length == 1 && !c.name.StartsWith(SpecMark))
                Object.DestroyImmediate(c.gameObject);
    }
}
#endif
