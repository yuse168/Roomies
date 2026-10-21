#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 家具の仮アセットの状態を調べる調査用ツール。
///
/// 「家具がただの箱に見える」原因を切り分けるために使う。
///  ・カタログの各IDに対して Resources から見た目プレハブが引けているか
///  ・引けている場合、それが本当に箱なのか（頂点数・サブメッシュ・バウンズ）
///
/// 並べて見たい場合は Preview Row で現在のシーンへ一列に出し、消すときは Clear。
/// </summary>
internal static class FurnitureArtInspector
{
    private const string PreviewRootName = "~FurnitureArtPreview";
    private const string ReportFileName = "furniture_art_report.txt";

    [MenuItem("Roomies/Furniture/Inspect Placeholder Art")]
    private static void Inspect()
    {
        var report = new StringBuilder();
        report.AppendLine("[Furniture] 仮アセットの状態");
        report.AppendLine(
            "id".PadRight(22) + "prefab  renderers  submesh  verts  bounds(W×H×D)");

        int missing = 0;
        int boxy = 0;

        foreach (FurnitureItem item in FurnitureCatalog.Items)
        {
            if (item == null) continue;

            GameObject prefab = Resources.Load<GameObject>("RoomiesArt/Furniture/" + item.id);
            if (prefab == null)
            {
                missing++;
                report.AppendLine(item.id.PadRight(22) + "なし   ← Cubeにフォールバックする");
                continue;
            }

            int renderers = 0;
            int submeshes = 0;
            int verts = 0;
            Bounds bounds = new Bounds();
            bool first = true;

            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                renderers++;
                submeshes += mesh.subMeshCount;
                verts += mesh.vertexCount;
                if (first) { bounds = mesh.bounds; first = false; }
                else bounds.Encapsulate(mesh.bounds);
            }

            // 立方体は 24頂点/12三角 が目安。これ以下なら実質「箱」
            bool looksLikeBox = verts <= 32;
            if (looksLikeBox) boxy++;

            report.AppendLine(
                item.id.PadRight(22) +
                "あり   " +
                renderers.ToString().PadLeft(6) +
                submeshes.ToString().PadLeft(9) +
                verts.ToString().PadLeft(8) +
                string.Format("  {0:0.00}×{1:0.00}×{2:0.00}",
                    bounds.size.x, bounds.size.y, bounds.size.z) +
                (looksLikeBox ? "   ← 箱" : ""));
        }

        report.AppendLine();
        report.AppendLine($"合計 {FurnitureCatalog.Count} 件 / プレハブ無し {missing} 件 / 実質箱 {boxy} 件");
        Debug.Log(report.ToString());

        // コンソール取得が当てにならない環境でも読めるようにファイルへも出す
        string outPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath), "Library", ReportFileName);
        try
        {
            System.IO.File.WriteAllText(outPath, report.ToString(), System.Text.Encoding.UTF8);
            Debug.Log($"[Furniture] レポートを書き出しました: {outPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Furniture] レポートの書き出しに失敗: {e.Message}");
        }
    }

    [MenuItem("Roomies/Furniture/Preview Row (place in scene)")]
    private static void PreviewRow()
    {
        ClearPreview();

        var root = new GameObject(PreviewRootName);
        root.transform.position = Vector3.zero;

        float x = 0f;
        var missing = new List<string>();

        foreach (FurnitureItem item in FurnitureCatalog.Items)
        {
            if (item == null) continue;

            GameObject prefab = Resources.Load<GameObject>("RoomiesArt/Furniture/" + item.id);
            GameObject instance;

            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                missing.Add(item.id);
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.localScale = item.placeholderSize;
            }

            instance.name = item.id;
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = new Vector3(x, 0f, 0f);
            x += 2.2f;
        }

        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();
        Debug.Log($"[Furniture] 一列に並べました（{PreviewRootName}）。" +
                  (missing.Count > 0 ? $" プレハブ無し: {string.Join(", ", missing)}" : ""));
    }

    [MenuItem("Roomies/Furniture/Preview Row (clear)")]
    private static void ClearPreviewMenu() => ClearPreview();

    private static void ClearPreview()
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (go != null && go.name == PreviewRootName) Object.DestroyImmediate(go);
        }
    }
}
#endif
