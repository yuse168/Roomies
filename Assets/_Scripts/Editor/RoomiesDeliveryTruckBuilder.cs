#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 配達の作り直し（第1段階）の配置ツール。実行時には動かない。何度実行しても同じ結果。
///
///  1. Build Price Tags … 箱のプレハブ（Nomal_Box / rare_Box）に値札（Anchor_PriceTag/PriceTag）を置く
/// </summary>
internal static class RoomiesDeliveryTruckBuilder
{
    const string NormalBoxPath = "Assets/_Prefabs/Nomal_Box.prefab";
    const string RareBoxPath = "Assets/_Prefabs/rare_Box.prefab";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset";

    // ---- 1. 値札 ----

    [MenuItem("Roomies/Delivery/1 Build Price Tags")]
    internal static void BuildPriceTags()
    {
        foreach (string path in new[] { NormalBoxPath, RareBoxPath })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var item = root.GetComponentInChildren<DeliveryItem>(true);
                if (item == null) { Debug.LogWarning($"[Delivery] {path} に DeliveryItem がない"); continue; }

                // 箱の高さ（ルートから見た見た目の上端）
                float top = 0.5f;
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    top = b.max.y - root.transform.position.y;
                }

                Transform anchor = root.transform.Find("Anchor_PriceTag");
                if (anchor == null) anchor = new GameObject("Anchor_PriceTag").transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(0f, (top + 0.3f) / Mathf.Max(0.0001f, root.transform.lossyScale.y), 0f);
                anchor.localRotation = Quaternion.identity;
                anchor.localScale = new Vector3(1f / Mathf.Max(0.0001f, root.transform.lossyScale.x),
                                                1f / Mathf.Max(0.0001f, root.transform.lossyScale.y),
                                                1f / Mathf.Max(0.0001f, root.transform.lossyScale.z));

                Transform labelT = anchor.Find("PriceTag");
                if (labelT == null) labelT = new GameObject("PriceTag", typeof(RectTransform)).transform;
                labelT.SetParent(anchor, false);
                labelT.localPosition = Vector3.zero;
                labelT.localRotation = Quaternion.identity;
                labelT.localScale = Vector3.one;

                var text = labelT.GetComponent<TextMeshPro>();
                if (text == null) text = labelT.gameObject.AddComponent<TextMeshPro>();
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                text.text = path == RareBoxPath ? "¥300" : "¥100";
                text.fontSize = 3f;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.fontSharedMaterial = text.font != null ? text.font.material : null;
                ((RectTransform)labelT).sizeDelta = new Vector2(2f, 0.6f);

                var so = new SerializedObject(item);
                so.FindProperty("priceLabel").objectReferenceValue = text;
                so.FindProperty("rareByDefault").boolValue = path == RareBoxPath;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Delivery] price tag: {path} (top {top:F2})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
