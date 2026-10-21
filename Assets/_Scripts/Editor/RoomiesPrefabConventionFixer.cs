#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AGENTS.md「プレハブと仮アセット」の形に既存プレハブをそろえる (2026-10-21)。
/// 見た目は子の Visual の下だけ、ロジック・通信・判定はその外。何度実行しても同じ結果になる。
/// オブジェクトは移動・名前変更だけ (作り直さない) なので、シリアライズ参照とシーン側のオーバーライドは保たれる。
/// </summary>
internal static class RoomiesPrefabConventionFixer
{
    const string VisualName = "Visual";

    [MenuItem("Roomies/Prefabs/Apply Visual Convention")]
    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var log = new List<string>();

        Edit("Assets/_Prefabs/Facilities/CharacterCloset.prefab", root => Rename(root, "Appearance_REPLACE_ME", VisualName), log);
        Edit("Assets/_Prefabs/Slot.prefab", root => Rename(root, "Model", VisualName), log);
        Edit("Assets/_Prefabs/BlackjackTable.prefab", root => GroupRenderers(root.transform), log);
        Edit("Assets/_Prefabs/DeliveryBox.prefab", root =>
        {
            var visual = EnsureVisual(root.transform);
            var moved = Move(root.transform, "NormalVisual", visual, "Normal");
            moved |= Move(root.transform, "RareVisual", visual, "Rare");
            return moved;
        }, log);
        foreach (string path in AssetDatabase.FindAssets("t:Prefab MiningOre_", new[] { "Assets/_Prefabs/Mining" }).Select(AssetDatabase.GUIDToAssetPath))
            Edit(path, root => GroupRenderers(root.transform), log);
        foreach (string role in new[] { "Dealer", "Giver", "Police", "PaperBag" })
            Edit($"Assets/_Prefabs/Smuggling/{role}_Placeholder.prefab", root => Rename(root, "VisualRoot", VisualName), log);
        Edit("Assets/_Prefabs/Smuggling/JailLabor_Placeholder.prefab", root =>
        {
            var placeholder = root.transform.Find("Placeholder");
            if (placeholder == null) return false;
            placeholder.SetParent(EnsureVisual(root.transform), true);
            return true;
        }, log);

        AssetDatabase.SaveAssets();
        Debug.Log("[RoomiesPrefabConventionFixer]\n" + string.Join("\n", log));
    }

    static void Edit(string path, Func<GameObject, bool> change, List<string> log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add($"missing  {path}"); return; }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = change(root);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            log.Add($"{(changed ? "changed " : "ok      ")} {path}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool Rename(GameObject root, string from, string to)
    {
        var target = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == from);
        if (target == null) return false;
        target.name = to;
        return true;
    }

    static Transform EnsureVisual(Transform root)
    {
        var visual = root.Find(VisualName);
        if (visual != null) return visual;
        visual = new GameObject(VisualName).transform;
        visual.SetParent(root, false);
        return visual;
    }

    static bool Move(Transform root, string childName, Transform parent, string newName)
    {
        var child = root.Find(childName);
        if (child == null) return false;
        child.SetParent(parent, true);
        child.name = newName;
        return true;
    }

    /// <summary>ルート直下の「見た目だけの子」(スクリプトを持たず、描画を含む) を Visual の下へまとめる。</summary>
    static bool GroupRenderers(Transform root)
    {
        var parts = root.Cast<Transform>()
            .Where(t => t.name != VisualName && t.GetComponent<MonoBehaviour>() == null && t.GetComponentInChildren<Renderer>(true) != null)
            .ToList();
        if (parts.Count == 0) return false;
        var visual = EnsureVisual(root);
        foreach (var part in parts) part.SetParent(visual, true);
        return true;
    }
}
#endif
