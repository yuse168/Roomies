#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// マップの外へ出られないように、見えない壁をシーンへ置くツール。
///
/// 柵（Boundary rail）の位置に合わせて x±39.3 / z±24 を既定にしている。
/// 遠景の地面を広げたことで、柵の外へ歩いて行けてしまうのを止めるのが目的。
///
/// 規約（AGENTS.md）：
///  ・実行時には作らない。ここでシーンに実体として置く
///  ・範囲や高さはこのツールの定数で変えて、置き直す
///
/// メニュー：
///  Roomies/Environment/Build Map Boundary
///  Roomies/Environment/Remove Map Boundary
/// </summary>
internal static class RoomiesBoundaryBuilder
{
    private const string RootName = "Roomies_Boundary";

    // 遊べる範囲（柵の内側）
    private const float LimitX = 39.3f;
    private const float LimitZ = 24f;

    // 壁の高さと厚み。ジャンプやトランポリンで越えられない高さにする
    private const float Height = 14f;
    private const float Thickness = 1.2f;

    [MenuItem("Roomies/Environment/Build Map Boundary")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Boundary] Play Modeを抜けてから実行してください。");
            return;
        }

        Remove(silent: true);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build map boundary");

        float spanX = LimitX * 2f + Thickness * 2f;
        float spanZ = LimitZ * 2f + Thickness * 2f;

        Wall(root.transform, "Wall +X", new Vector3(LimitX + Thickness / 2f, Height / 2f, 0f),
            new Vector3(Thickness, Height, spanZ));
        Wall(root.transform, "Wall -X", new Vector3(-LimitX - Thickness / 2f, Height / 2f, 0f),
            new Vector3(Thickness, Height, spanZ));
        Wall(root.transform, "Wall +Z", new Vector3(0f, Height / 2f, LimitZ + Thickness / 2f),
            new Vector3(spanX, Height, Thickness));
        Wall(root.transform, "Wall -Z", new Vector3(0f, Height / 2f, -LimitZ - Thickness / 2f),
            new Vector3(spanX, Height, Thickness));

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Boundary] 見えない壁を置きました（x±{LimitX} / z±{LimitZ}・高さ{Height}m）" +
                  "（シーンを保存してください）");
    }

    [MenuItem("Roomies/Environment/Remove Map Boundary")]
    private static void RemoveMenu() => Remove(silent: false);

    private static void Remove(bool silent)
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing == null)
        {
            if (!silent) Debug.Log("[Boundary] 見えない壁はまだ置かれていません。");
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        if (!silent) Debug.Log("[Boundary] 見えない壁を外しました（シーンを保存してください）");
    }

    private static void Wall(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position;

        var collider = wall.AddComponent<BoxCollider>();
        collider.size = size;

        int layer = LayerMask.NameToLayer("Wall");
        if (layer >= 0) wall.layer = layer;

        // 見た目は付けない（描画物が無いので負荷ゼロ）。
        // シーンビューではコライダーの枠だけが見える。
        GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
    }
}
#endif
