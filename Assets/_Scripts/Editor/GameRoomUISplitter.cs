#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameRoom のゲーム中UIを、UIだけのシーン（GameRoomUI）へ移す配置ツール。
/// 何度実行しても同じ結果になる（移したものは移さない、あるものは作らない）。
///
/// ・UIのルートを GameRoomUI へ移す（見た目・中身はそのまま）
/// ・GameRoomUI に「GameUI」（<see cref="GameUIRefs"/>）を置き、マップ側が使うUIをつなぐ
/// ・マップ側（DayManager など）が持っていたUIへの参照は、シーンをまたげないので外す
/// ・GameRoom に「GameUISceneLoader」を置く（実行時にUIシーンを読み込む）
/// ・Build Settings に GameRoomUI を足す
/// </summary>
internal static class GameRoomUISplitter
{
    private const string GameScenePath = "Assets/_Scenes/GameRoom.unity";
    private const string UIScenePath = "Assets/_Scenes/GameRoomUI.unity";

    // ゲーム中のUI（Canvas・HUD・ポーズ・朝/収支/家賃/夜バナー・ゲームオーバー・逮捕演出・EventSystem）
    private static readonly string[] UIRootNames =
    {
        "EventSystem", "Canvas", "HudThemer", "UI_DayScreens",
        "EscMenuUI", "GameOverScreen", "ArrestCinematic",
    };

    [MenuItem("Roomies/UI/Split GameRoom UI")]
    private static void Split()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        Scene game = SceneManager.GetSceneByPath(GameScenePath);
        if (!game.IsValid() || !game.isLoaded)
        {
            Debug.LogError("[UI] GameRoom シーンを開いてから実行してください。");
            return;
        }

        Scene ui = OpenOrCreateUIScene();
        if (!ui.IsValid()) return;

        // --- 1. マップ側が持っているUIへの参照を先に読む（移すと読めなくなるため） ---
        var dayManager = FindIn<DayManager>(game, ui);
        var moneyManager = FindIn<SharedMoneyManager>(game, ui);
        var nightManager = FindIn<NightEventManager>(game, ui);

        Object dayText = ReadRef(dayManager, "dayText");
        Object timerText = ReadRef(dayManager, "timerText");
        Object transitionUI = ReadRef(dayManager, "transitionUI");
        Object resultUI = ReadRef(dayManager, "resultUI");
        Object rentPaymentUI = ReadRef(dayManager, "rentPaymentUI");
        Object sharedMoneyText = ReadRef(moneyManager, "sharedMoneyText");
        Object nightEventUI = ReadRef(nightManager, "eventUI");

        // --- 2. 移すルートを集める。ネットワークの処理を持つものは移さない ---
        var roots = new List<GameObject>();
        foreach (string name in UIRootNames)
        {
            GameObject root = game.GetRootGameObjects().FirstOrDefault(go => go.name == name);
            if (root == null) continue; // 移し済み
            if (root.GetComponentsInChildren<NetworkBehaviour>(true).Length > 0)
            {
                Debug.LogError($"[UI] {name} にネットワークの処理があるので移しません。", root);
                return;
            }
            roots.Add(root);
        }

        // UIの中のネットワークの部品（処理を持たない NetworkObject だけ）を外す。
        // UIシーンはネットワークで同期しない見た目だけのシーンにする
        foreach (GameObject root in roots)
        {
            foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
            {
                Debug.Log($"[UI] UIの {networkObject.name} から NetworkObject を外しました（処理は付いていない）。",
                    networkObject.gameObject);
                Undo.DestroyObjectImmediate(networkObject);
            }
        }

        // --- 3. マップ側のUI参照を外す（シーンをまたぐ参照は保存できない） ---
        ClearRefs(dayManager, "dayText", "timerText", "transitionUI", "resultUI", "rentPaymentUI");
        ClearRefs(moneyManager, "sharedMoneyText");
        ClearRefs(nightManager, "eventUI");

        // --- 4. 移す ---
        foreach (GameObject root in roots)
        {
            SceneManager.MoveGameObjectToScene(root, ui);
            Debug.Log($"[UI] {root.name} を GameRoomUI へ移しました。", root);
        }

        // --- 5. 受け渡し口（GameUI）をUIシーンに置き、UIをつなぐ ---
        GameObject hub = ui.GetRootGameObjects().FirstOrDefault(go => go.name == "GameUI");
        if (hub == null)
        {
            hub = new GameObject("GameUI");
            SceneManager.MoveGameObjectToScene(hub, ui);
            hub.transform.SetAsFirstSibling();
        }
        var refs = hub.GetComponent<GameUIRefs>();
        if (refs == null) refs = hub.AddComponent<GameUIRefs>();

        var so = new SerializedObject(refs);
        SetIfFound(so, "dayText", dayText);
        SetIfFound(so, "timerText", timerText);
        SetIfFound(so, "sharedMoneyText", sharedMoneyText);
        SetIfFound(so, "transitionUI", transitionUI);
        SetIfFound(so, "resultUI", resultUI);
        SetIfFound(so, "rentPaymentUI", rentPaymentUI);
        SetIfFound(so, "nightEventUI", nightEventUI);
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- 6. GameRoom に読み込み係を置く ---
        if (game.GetRootGameObjects().All(go => go.GetComponent<GameUISceneLoader>() == null))
        {
            var loader = new GameObject("GameUISceneLoader");
            SceneManager.MoveGameObjectToScene(loader, game);
            loader.AddComponent<GameUISceneLoader>();
        }

        // --- 7. Build Settings に足す（GameRoom の次） ---
        AddToBuildSettings();

        SceneManager.SetActiveScene(game);
        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.MarkSceneDirty(ui);
        EditorSceneManager.SaveScene(ui);
        EditorSceneManager.SaveScene(game);
        Debug.Log("[UI] ゲーム中のUIを GameRoomUI に分けました。");
    }

    private static Scene OpenOrCreateUIScene()
    {
        Scene ui = SceneManager.GetSceneByPath(UIScenePath);
        if (ui.IsValid() && ui.isLoaded) return ui;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(UIScenePath) != null)
            return EditorSceneManager.OpenScene(UIScenePath, OpenSceneMode.Additive);

        ui = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        if (!EditorSceneManager.SaveScene(ui, UIScenePath))
        {
            Debug.LogError("[UI] GameRoomUI シーンを保存できませんでした。");
            return default;
        }
        return ui;
    }

    private static T FindIn<T>(params Scene[] scenes) where T : Component
    {
        foreach (Scene scene in scenes)
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    private static Object ReadRef(Object owner, string field)
    {
        if (owner == null) return null;
        SerializedProperty property = new SerializedObject(owner).FindProperty(field);
        return property != null ? property.objectReferenceValue : null;
    }

    private static void ClearRefs(Object owner, params string[] fields)
    {
        if (owner == null) return;
        var so = new SerializedObject(owner);
        foreach (string field in fields)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null) property.objectReferenceValue = null;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetIfFound(SerializedObject so, string field, Object value)
    {
        if (value == null) return; // 2回目以降は、すでに入っている値を残す
        SerializedProperty property = so.FindProperty(field);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == UIScenePath)) return;

        int gameIndex = scenes.FindIndex(s => s.path == GameScenePath);
        var entry = new EditorBuildSettingsScene(UIScenePath, true);
        if (gameIndex >= 0) scenes.Insert(gameIndex + 1, entry);
        else scenes.Add(entry);
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
