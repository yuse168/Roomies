#if UNITY_EDITOR
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 配達の作り直し（第1段階）の配置ツール。実行時には動かない。何度実行しても同じ結果。
///
///  1. Build Price Tags            … 箱のプレハブ（Nomal_Box / rare_Box）に値札（Anchor_PriceTag/PriceTag）を置く
///  2. Build Truck Prefab          … Assets/_Prefabs/Delivery/DeliveryTruck.prefab を作る
///                                   （ルート：NetworkObject・DeliveryTruck・荷台の判定／Pivot_Drive：走り去るときに動かす軸／
///                                     Pivot_Drive/Visual/Placeholder：仮の見た目／Pivot_Drive/Anchor_Sign：看板の文字／
///                                     Anchor_Board：乗る位置の中心／Anchor_Sign：看板の文字）
///  3. Place Truck In Open Scene   … 開いている GameRoom の配達ゾーンの所にトラックを置き、集荷場とつなぎ、
///                                   古い納品ボタン（DeliveryButton）を止める
///
/// トラックの位置・向きは、置いたあとシーンビューで動かしてよい（もう一度 3 を実行すると下の値に戻る）。
/// </summary>
internal static class RoomiesDeliveryTruckBuilder
{
    const string NormalBoxPath = "Assets/_Prefabs/Nomal_Box.prefab";
    const string RareBoxPath = "Assets/_Prefabs/rare_Box.prefab";
    const string TruckFolder = "Assets/_Prefabs/Delivery";
    const string TruckPath = TruckFolder + "/DeliveryTruck.prefab";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset";

    // トラックを置く所：配達ゾーンの位置から少し北。後ろ（荷台の口）が南＝集荷場からの道の方を向く
    static readonly Vector3 TruckOffsetFromZone = new Vector3(0f, 0f, 0.6f);
    const float TruckYaw = 0f;

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

    // ---- 2. トラックのプレハブ ----

    [MenuItem("Roomies/Delivery/2 Build Truck Prefab")]
    internal static void BuildTruckPrefab()
    {
        if (!AssetDatabase.IsValidFolder(TruckFolder))
            AssetDatabase.CreateFolder("Assets/_Prefabs", "Delivery");

        var root = new GameObject("DeliveryTruck");
        try
        {
            root.AddComponent<NetworkObject>();

            // 荷台の判定（ルート）。床の上（y 0.95）から高さ 1.5、口は後ろ（-Z）
            var bed = root.AddComponent<BoxCollider>();
            bed.isTrigger = true;
            bed.center = new Vector3(0f, 1.7f, -1.35f);
            bed.size = new Vector3(2.0f, 1.5f, 3.1f);

            var truck = root.AddComponent<DeliveryTruck>();

            // 走り去るときに動かす軸。見た目と看板はこの下（荷台の判定と乗る位置はルートに残す）
            var pivot = new GameObject("Pivot_Drive").transform;
            pivot.SetParent(root.transform, false);

            // 見た目（仮）。ぶつかるだけの当たり判定は見た目と一緒に替わるので Visual の中に置く
            var visual = new GameObject("Visual").transform;
            visual.SetParent(pivot, false);
            var ph = new GameObject("Placeholder").transform;
            ph.SetParent(visual, false);

            Material body = Mat("PaintedMetal/Town/mustard.mat");
            Material bedMat = Mat("PaintedMetal/Town/cream.mat");
            Material dark = Mat("Rubber/plastic_black.mat");
            Material glass = Mat("Glass/Town/blue.mat");
            Material ink = Mat("PaintedMetal/Town/ink.mat");

            Box(ph, "Chassis", new Vector3(0f, 0.6f, -0.3f), new Vector3(2.2f, 0.3f, 5.2f), dark, true);
            Box(ph, "BedFloor", new Vector3(0f, 0.85f, -1.35f), new Vector3(2.2f, 0.2f, 3.1f), bedMat, true);
            Box(ph, "BedSide_L", new Vector3(-1.05f, 1.3f, -1.35f), new Vector3(0.1f, 0.7f, 3.1f), body, true);
            Box(ph, "BedSide_R", new Vector3(1.05f, 1.3f, -1.35f), new Vector3(0.1f, 0.7f, 3.1f), body, true);
            Box(ph, "BedFront", new Vector3(0f, 1.3f, 0.15f), new Vector3(2.2f, 0.7f, 0.1f), body, true);
            // 後ろのあおり：床から 0.3m（持った高さから投げても越えられる低さ）
            Box(ph, "Tailgate", new Vector3(0f, 1.1f, -2.86f), new Vector3(2.2f, 0.3f, 0.12f), body, true);

            // 荷台の床・板は跳ね返らず、すべりにくい（投げ込んだ箱が前の板で跳ね返って後ろへ出ないように）
            var bedSurface = BedPhysicsMaterial();
            foreach (var part in new[] { "BedFloor", "BedSide_L", "BedSide_R", "BedFront", "Tailgate" })
                ph.Find(part).GetComponent<Collider>().sharedMaterial = bedSurface;
            Box(ph, "Cab", new Vector3(0f, 1.6f, 1.25f), new Vector3(2.2f, 1.9f, 1.8f), body, true);
            Box(ph, "Windshield", new Vector3(0f, 2.05f, 2.16f), new Vector3(1.9f, 0.8f, 0.04f), glass, false);
            Box(ph, "Window_L", new Vector3(-1.11f, 2.05f, 1.35f), new Vector3(0.04f, 0.7f, 1.1f), glass, false);
            Box(ph, "Window_R", new Vector3(1.11f, 2.05f, 1.35f), new Vector3(0.04f, 0.7f, 1.1f), glass, false);
            Box(ph, "SignBoard", new Vector3(0f, 3.05f, 0.95f), new Vector3(2.0f, 0.75f, 0.08f), ink, false);
            foreach (var (n, x, z) in new[] { ("Wheel_FL", -1.0f, 1.3f), ("Wheel_FR", 1.0f, 1.3f), ("Wheel_RL", -1.0f, -1.9f), ("Wheel_RR", 1.0f, -1.9f) })
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                w.name = n;
                Object.DestroyImmediate(w.GetComponent<Collider>());
                w.transform.SetParent(ph, false);
                w.transform.localPosition = new Vector3(x, 0.42f, z);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                w.transform.localScale = new Vector3(0.84f, 0.15f, 0.84f);
                w.GetComponent<Renderer>().sharedMaterial = dark;
            }

            // 乗った・乗らないを測る中心（荷台の口の後ろ）
            var board = new GameObject("Anchor_Board").transform;
            board.SetParent(root.transform, false);
            board.localPosition = new Vector3(0f, 0f, -2.2f);

            // 看板の文字（荷台の口の方＝後ろから読める）
            var signAnchor = new GameObject("Anchor_Sign").transform;
            signAnchor.SetParent(pivot, false);
            signAnchor.localPosition = new Vector3(0f, 3.05f, 0.9f);
            var signT = new GameObject("SignText", typeof(RectTransform)).transform;
            signT.SetParent(signAnchor, false);
            var sign = signT.gameObject.AddComponent<TextMeshPro>();
            sign.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            sign.text = "発車まで 3:00\n荷台 0個 ¥0";
            sign.fontSize = 2.4f;
            sign.alignment = TextAlignmentOptions.Center;
            sign.textWrappingMode = TextWrappingModes.NoWrap;
            sign.color = new Color(1f, 0.92f, 0.62f);
            sign.fontSharedMaterial = sign.font != null ? sign.font.material : null;
            ((RectTransform)signT).sizeDelta = new Vector2(1.9f, 0.7f);

            var so = new SerializedObject(truck);
            so.FindProperty("boardAnchor").objectReferenceValue = board;
            so.FindProperty("drivePivot").objectReferenceValue = pivot;
            so.FindProperty("signLabel").objectReferenceValue = sign;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, TruckPath);
            Debug.Log("[Delivery] truck prefab: " + TruckPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    const string BedMaterialPath = TruckFolder + "/TruckBedSurface.physicsMaterial";

    /// <summary>
    /// 荷台の物理の素材（跳ね返り 0・摩擦 ふつう）。無ければ作り、あれば値をそろえる。
    /// 摩擦を強くしすぎると、あおりの上に乗った箱がすべり落ちずに口をふさぐので、ふつうにしている。
    /// </summary>
    static PhysicsMaterial BedPhysicsMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BedMaterialPath);
        bool create = m == null;
        if (create) m = new PhysicsMaterial("TruckBedSurface");
        m.bounciness = 0f;
        m.bounceCombine = PhysicsMaterialCombine.Minimum;
        m.dynamicFriction = 0.45f;
        m.staticFriction = 0.5f;
        m.frictionCombine = PhysicsMaterialCombine.Average;
        if (create) AssetDatabase.CreateAsset(m, BedMaterialPath);
        else { EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m); }
        return m;
    }

    static Material Mat(string relative)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/Templates/Variants/" + relative);
        if (m == null) Debug.LogWarning("[Delivery] material not found: " + relative);
        return m;
    }

    static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collide)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        if (!collide) Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ---- 3. シーンに置く ----

    [MenuItem("Roomies/Delivery/3 Place Truck In Open Scene")]
    internal static void PlaceTruck()
    {
        var zone = Object.FindObjectsByType<DeliveryZone>(FindObjectsInactive.Include).FirstOrDefault();
        if (zone == null) { Debug.LogWarning("[Delivery] DeliveryZone がない（GameRoom を開いて実行する）"); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPath);
        if (prefab == null) BuildTruckPrefab();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPath);

        var scene = zone.gameObject.scene;
        var truck = Object.FindObjectsByType<DeliveryTruck>(FindObjectsInactive.Include).FirstOrDefault();
        if (truck == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            truck = go.GetComponent<DeliveryTruck>();
        }

        // 地面の高さ（トラック自身の当たり判定は除く）
        Vector3 p = zone.transform.position + TruckOffsetFromZone;
        var groundHits = Physics.RaycastAll(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !h.transform.IsChildOf(truck.transform)).OrderBy(h => h.distance).ToArray();
        if (groundHits.Length > 0) p.y = groundHits[0].point.y;
        truck.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, TruckYaw, 0f));

        var tso = new SerializedObject(truck);
        tso.FindProperty("deliveryZone").objectReferenceValue = zone;
        tso.ApplyModifiedProperties();

        var zso = new SerializedObject(zone);
        zso.FindProperty("truck").objectReferenceValue = truck;
        zso.ApplyModifiedProperties();

        // 古い納品ボタンは止める（お金になるのはトラックの発車だけ）
        foreach (var button in Object.FindObjectsByType<DeliveryButton>(FindObjectsInactive.Include))
            button.gameObject.SetActive(false);

        // 置いた場所に何か重なっていないか（地面と配達ゾーン以外）
        var overlaps = Physics.OverlapBox(truck.transform.TransformPoint(new Vector3(0f, 1.5f, -0.3f)),
                new Vector3(1.1f, 1.2f, 2.6f), truck.transform.rotation, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => !c.transform.IsChildOf(truck.transform) && c.transform.position.y > p.y + 0.15f)
            .Select(c => c.name).Distinct().Take(8).ToArray();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string report = $"truck placed at {p} yaw {TruckYaw} ground {(groundHits.Length > 0 ? groundHits[0].collider.name : "none")}. overlaps: {(overlaps.Length == 0 ? "none" : string.Join(", ", overlaps))}";
        System.IO.Directory.CreateDirectory("Temp/RoomiesDelivery");
        System.IO.File.WriteAllText("Temp/RoomiesDelivery/place.txt", report);
        Debug.Log("[Delivery] " + report);
    }
}
#endif
