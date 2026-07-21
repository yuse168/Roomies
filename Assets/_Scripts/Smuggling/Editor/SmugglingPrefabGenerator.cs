#if UNITY_EDITOR
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

/// <summary>差し替え可能な運び屋用仮プレハブを生成し、Playerへ状態コンポーネントを追加する。</summary>
public static class SmugglingPrefabGenerator
{
    private const string Folder = "Assets/_Prefabs/Smuggling";
    private const string PlayerPath = "Assets/_Prefabs/Player.prefab";
    private const string SessionKey = "Roomies.SmugglingPrefabsGenerated.v1";

    [InitializeOnLoadMethod]
    private static void GenerateOnceAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            GenerateMissingAssets();
            SessionState.SetBool(SessionKey, true);
        };
    }

    [MenuItem("Tools/Roomies/闇バイト/仮プレハブを生成・修復")]
    public static void GenerateMissingAssets()
    {
        EnsureFolder();
        Material brown = GetOrCreateMaterial("Smuggling_Brown", new Color(0.48f, 0.29f, 0.12f));
        Material dark = GetOrCreateMaterial("Smuggling_Dark", new Color(0.07f, 0.08f, 0.1f));
        Material blue = GetOrCreateMaterial("Smuggling_PoliceBlue", new Color(0.07f, 0.18f, 0.55f));
        Material skin = GetOrCreateMaterial("Smuggling_Skin", new Color(0.9f, 0.68f, 0.48f));
        Material gray = GetOrCreateMaterial("Smuggling_JailGray", new Color(0.3f, 0.33f, 0.36f));

        string bagPath = Folder + "/PaperBag_Placeholder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(bagPath) == null) CreateBag(bagPath, brown);

        string giverPath = Folder + "/Giver_Placeholder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(giverPath) == null)
            CreateContact(giverPath, "渡し人（仮）", SmugglingContact.ContactRole.Giver, dark, skin);

        string dealerPath = Folder + "/Dealer_Placeholder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dealerPath) == null)
            CreateContact(dealerPath, "売人（仮）", SmugglingContact.ContactRole.Dealer, brown, skin);

        string policePath = Folder + "/Police_Placeholder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(policePath) == null)
            CreatePolice(policePath, blue, skin);

        string laborPath = Folder + "/JailLabor_Placeholder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(laborPath) == null)
            CreateLabor(laborPath, gray);

        string jailPointPath = Folder + "/JailPoint.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(jailPointPath) == null)
            CreateJailPoint(jailPointPath);

        AddPlayerState(AssetDatabase.LoadAssetAtPath<GameObject>(bagPath));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Smuggling] 仮プレハブの生成・Player設定が完了しました: " + Folder);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Prefabs")) AssetDatabase.CreateFolder("Assets", "_Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Prefabs", "Smuggling");
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        material = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void CreateBag(string path, Material brown)
    {
        GameObject root = new GameObject("PaperBag_Placeholder");
        Transform visualRoot = NewChild(root.transform, "VisualRoot");
        Transform modelRoot = NewChild(visualRoot, "ModelRoot");
        GameObject placeholder = Primitive(PrimitiveType.Cube, visualRoot, "Placeholder", brown);
        placeholder.transform.localScale = new Vector3(0.75f, 1f, 0.36f);

        SmugglingAppearance appearance = root.AddComponent<SmugglingAppearance>();
        SetAppearance(appearance, modelRoot, placeholder);
        SaveAndDestroy(root, path);
    }

    private static void CreateContact(
        string path,
        string name,
        SmugglingContact.ContactRole role,
        Material clothes,
        Material skin)
    {
        GameObject root = new GameObject(name);
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up;
        collider.height = 2f;
        collider.radius = 0.45f;

        Transform visualRoot = NewChild(root.transform, "VisualRoot");
        Transform modelRoot = NewChild(visualRoot, "ModelRoot");
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(visualRoot, false);

        GameObject body = Primitive(PrimitiveType.Capsule, placeholder.transform, "Body", clothes);
        body.transform.localPosition = Vector3.up;
        body.transform.localScale = new Vector3(0.72f, 1f, 0.72f);
        GameObject head = Primitive(PrimitiveType.Sphere, placeholder.transform, "Head", skin);
        head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        head.transform.localScale = Vector3.one * 0.55f;

        SmugglingAppearance appearance = root.AddComponent<SmugglingAppearance>();
        SetAppearance(appearance, modelRoot, placeholder);

        SmugglingContact contact = root.AddComponent<SmugglingContact>();
        SerializedObject so = new SerializedObject(contact);
        so.FindProperty("role").enumValueIndex = (int)role;
        so.FindProperty("interactionCollider").objectReferenceValue = collider;
        so.FindProperty("visibleRoot").objectReferenceValue = visualRoot.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndDestroy(root, path);
    }

    private static void CreatePolice(string path, Material blue, Material skin)
    {
        GameObject root = new GameObject("警察（仮）");
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up;
        collider.height = 2f;
        collider.radius = 0.48f;

        Transform visualRoot = NewChild(root.transform, "VisualRoot");
        Transform modelRoot = NewChild(visualRoot, "ModelRoot");
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(visualRoot, false);

        GameObject body = Primitive(PrimitiveType.Capsule, placeholder.transform, "Body", blue);
        body.transform.localPosition = Vector3.up;
        body.transform.localScale = new Vector3(0.78f, 1f, 0.78f);
        GameObject head = Primitive(PrimitiveType.Sphere, placeholder.transform, "Head", skin);
        head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        head.transform.localScale = Vector3.one * 0.55f;
        GameObject hat = Primitive(PrimitiveType.Cube, placeholder.transform, "Hat", blue);
        hat.transform.localPosition = new Vector3(0f, 2.13f, 0f);
        hat.transform.localScale = new Vector3(0.72f, 0.12f, 0.72f);

        Transform lightTransform = NewChild(visualRoot, "Flashlight");
        lightTransform.localPosition = new Vector3(0.25f, 1.25f, 0.35f);
        Light light = lightTransform.gameObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = 10f;
        light.spotAngle = 58f;
        light.intensity = 6f;
        light.color = new Color(1f, 0.92f, 0.65f);

        Transform p1 = NewChild(root.transform, "Point 1");
        p1.localPosition = new Vector3(-4f, 0f, 0f);
        Transform p2 = NewChild(root.transform, "Point 2");
        p2.localPosition = new Vector3(4f, 0f, 0f);

        SmugglingAppearance appearance = root.AddComponent<SmugglingAppearance>();
        SetAppearance(appearance, modelRoot, placeholder);

        SmugglingPolicePatrol patrol = root.AddComponent<SmugglingPolicePatrol>();
        SerializedObject so = new SerializedObject(patrol);
        so.FindProperty("point1").objectReferenceValue = p1;
        so.FindProperty("point2").objectReferenceValue = p2;
        so.FindProperty("flashlight").objectReferenceValue = light;
        so.FindProperty("visibleRoot").objectReferenceValue = visualRoot.gameObject;
        so.FindProperty("bodyCollider").objectReferenceValue = collider;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndDestroy(root, path);
    }

    private static void CreateLabor(string path, Material gray)
    {
        GameObject root = new GameObject("牢屋の作業台（仮）");
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.55f, 0f);
        collider.size = new Vector3(1.5f, 1.1f, 0.75f);
        root.AddComponent<SmugglingJailLabor>();

        GameObject table = Primitive(PrimitiveType.Cube, root.transform, "Placeholder", gray);
        table.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        table.transform.localScale = new Vector3(1.5f, 1.1f, 0.75f);
        SaveAndDestroy(root, path);
    }

    private static void CreateJailPoint(string path)
    {
        GameObject root = new GameObject("牢屋スポーン地点");
        root.AddComponent<SmugglingJailPoint>();
        SaveAndDestroy(root, path);
    }

    private static void AddPlayerState(GameObject bagPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null)
        {
            Debug.LogWarning("[Smuggling] Player.prefab が見つからず、SmugglingPlayerを追加できませんでした。");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            SmugglingPlayer player = root.GetComponent<SmugglingPlayer>();
            if (player == null) player = root.AddComponent<SmugglingPlayer>();

            SerializedObject so = new SerializedObject(player);
            so.FindProperty("bagVisualPrefab").objectReferenceValue = bagPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform NewChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return go;
    }

    private static void SetAppearance(SmugglingAppearance appearance, Transform modelRoot, GameObject placeholder)
    {
        SerializedObject so = new SerializedObject(appearance);
        so.FindProperty("modelRoot").objectReferenceValue = modelRoot;
        so.FindProperty("placeholder").objectReferenceValue = placeholder;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SaveAndDestroy(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
}
#endif
