#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 採掘場の入口に「閉鎖中のシャッター」をシーンへ置くツール。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・シャッターは実行時に生成しない。このツールでシーンに実体として置き、
///    位置・大きさ・マテリアルはインスペクターとシーンビューから直せる状態にする。
///  ・`MiningSite.closedGate` に置いた実体を割り当て、実行時は表示の切り替えだけ行う。
///
/// 何度実行しても同じ結果になる（既にあれば中身を作り直す）。
/// 採掘場をアップデートで開放するときは、この `Closed gate` を消して
/// `MiningSite.serviceClosed` をOFFにすればよい。
///
/// メニュー：Roomies/Mining/Build Closed Gate
/// </summary>
internal static class MiningClosedGateBuilder
{
    private const string EntrancePath =
        "Roomies_Mining/Surface - ONE MORE ROCK/Mine entrance beside house";
    private const string GateName = "Closed gate";
    private const string MaterialFolder = "Assets/Resources/RoomiesArt/Materials";

    // 入口のサイズに合わせた既定値。あとからシーンビューで動かしてよい。
    private const float ShutterWidth = 3.4f;
    private const float ShutterHeight = 3.3f;
    private const float ShutterDepth = 0.16f;
    private const float ShutterZ = -0.7f;   // 呼び出しボタン(z=-1.1)より奥
    private const int SlatCount = 6;

    [MenuItem("Roomies/Mining/Build Closed Gate")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Mining] Play Modeを抜けてから実行してください。");
            return;
        }

        GameObject entrance = GameObject.Find(EntrancePath);
        if (entrance == null)
        {
            Debug.LogError($"[Mining] 入口が見つかりません: {EntrancePath}（GameRoomを開いて実行してください）");
            return;
        }

        Transform existing = entrance.transform.Find(GateName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var gate = new GameObject(GateName);
        Undo.RegisterCreatedObjectUndo(gate, "Build mining closed gate");
        gate.transform.SetParent(entrance.transform, false);

        Material frame = LoadMaterial("ink");
        Material slat = LoadMaterial("silver");
        Material stripe = LoadMaterial("mustard");

        // シャッター本体。入口の看板(y=3.54)より下に収め、呼び出しボタンは手前に残す。
        Part(gate.transform, "Shutter",
            new Vector3(0f, ShutterHeight * 0.5f, ShutterZ),
            new Vector3(ShutterWidth, ShutterHeight, ShutterDepth),
            frame, solid: true);

        // 横板。シャッターらしい段をつける（見た目だけなのでコライダーなし）。
        float step = (ShutterHeight - 0.5f) / (SlatCount - 1);
        for (int i = 0; i < SlatCount; i++)
        {
            Part(gate.transform, $"Shutter slat {i + 1}",
                new Vector3(0f, 0.35f + i * step, ShutterZ - 0.09f),
                new Vector3(ShutterWidth - 0.1f, 0.4f, 0.04f),
                slat, solid: false);
        }

        // 警告帯。少し傾けて「立入禁止」に見せる。
        float[] tapeHeights = { 0.8f, ShutterHeight - 0.7f };
        for (int i = 0; i < tapeHeights.Length; i++)
        {
            Transform tape = Part(gate.transform, $"Warning tape {i + 1}",
                new Vector3(0f, tapeHeights[i], ShutterZ - 0.13f),
                new Vector3(ShutterWidth + 0.1f, 0.3f, 0.04f),
                stripe, solid: false);
            tape.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 6f : -6f);
        }

        AssignToMiningSite(gate);
        SetHintsVisible(entrance.transform, false);

        EditorSceneManager.MarkSceneDirty(entrance.scene);
        Selection.activeGameObject = gate;
        Debug.Log($"[Mining] 閉鎖シャッターを置き、操作のヒントを隠しました → {EntrancePath}/{GateName}（シーンを保存してください）");
    }

    [MenuItem("Roomies/Mining/Reopen Mine (remove gate)")]
    private static void Reopen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Mining] Play Modeを抜けてから実行してください。");
            return;
        }

        GameObject entrance = GameObject.Find(EntrancePath);
        if (entrance == null)
        {
            Debug.LogError($"[Mining] 入口が見つかりません: {EntrancePath}");
            return;
        }

        Transform gate = entrance.transform.Find(GateName);
        if (gate != null) Undo.DestroyObjectImmediate(gate.gameObject);

        SetHintsVisible(entrance.transform, true);

        EditorSceneManager.MarkSceneDirty(entrance.scene);
        Debug.Log("[Mining] シャッターを外し、買取機と案内を戻しました。" +
                  "MiningSite の Service Closed もOFFにしてください（シーンを保存してください）");
    }

    // 閉鎖中は「ここで何ができるか」を見せない。
    // 買取機・Eの案内・看板の文言を隠し、入口は「CLOSED」とだけ読めるようにする。
    private static void SetHintsVisible(Transform entrance, bool visible)
    {
        string[] hidden =
        {
            "mine_cashier",
            "Sign - ORE BUYBACK",
            "Sign - HOLD ORE + E",
            "Sign - E  /  DOWN",
            "Lift call button",
        };

        foreach (string name in hidden)
        {
            Transform target = Child(entrance, name);
            if (target == null) continue;
            Undo.RecordObject(target.gameObject, "Toggle mine hints");
            target.gameObject.SetActive(visible);
        }

        // 入口の小看板。閉鎖中は用途が分からない文言にする。
        Transform subtitle = Child(entrance, "Sign - MINING CO.  /  OPEN UNTIL SUNSET");
        if (subtitle != null)
        {
            var label = subtitle.GetComponent<TMPro.TextMeshPro>();
            if (label != null)
            {
                Undo.RecordObject(label, "Toggle mine sign");
                label.text = visible ? "MINING CO.  /  OPEN UNTIL SUNSET" : "CLOSED";
            }
        }
    }

    /// <summary>
    /// 名前で子を探す。`Transform.Find` は名前の中の "/" をパス区切りとして扱うので、
    /// 「Sign - E  /  DOWN」のような名前は見つけられない。ここは総当たりで比べる。
    /// </summary>
    private static Transform Child(Transform parent, string name)
    {
        foreach (Transform child in parent)
            if (child.name == name) return child;
        return null;
    }

    private static Transform Part(Transform parent, string name, Vector3 localPosition,
                                  Vector3 size, Material material, bool solid)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;

        if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;

        var collider = part.GetComponent<Collider>();
        if (solid)
        {
            int wall = LayerMask.NameToLayer("Wall");
            if (wall >= 0) part.layer = wall;
        }
        else if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        return part.transform;
    }

    private static Material LoadMaterial(string id) =>
        AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{id}.mat");

    // MiningSite から表示を切り替えられるように参照を入れる（名前で探させないため）。
    private static void AssignToMiningSite(GameObject gate)
    {
        MiningSite site = Object.FindAnyObjectByType<MiningSite>();
        if (site == null)
        {
            Debug.LogWarning("[Mining] MiningSiteが見つからないので参照は割り当てていません。");
            return;
        }

        var serialized = new SerializedObject(site);
        SerializedProperty property = serialized.FindProperty("closedGate");
        if (property == null)
        {
            Debug.LogWarning("[Mining] MiningSite.closedGate が見つかりません。");
            return;
        }

        property.objectReferenceValue = gate;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(site);
    }
}
#endif
