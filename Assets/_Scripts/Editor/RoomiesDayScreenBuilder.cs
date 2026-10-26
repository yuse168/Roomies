#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 朝の演出・今日の収支・家賃の支払いの3画面を、シーンへ実体として置くツール。
///
/// もともとは実行時に作っていたので、Play Modeに入らないと見えなかった。
/// ここで置いておけば、文字・色・位置をインスペクターとシーンビューで直せる。
///
/// 置いたあとは `DayManager` の `Transition UI` / `Result UI` / `Rent Payment UI` に
/// 自動で割り当てる。
///
/// 何度実行しても同じ結果（すでにある物は作り直さない。`Rebuild` は作り直す）。
///
/// メニュー：
///  Roomies/UI/Build Day Screens
///  Roomies/UI/Rebuild Day Screens
///  Roomies/UI/Build Night Banner
///  Roomies/UI/Build Earning List
///  Roomies/UI/Build HUD
///  Roomies/UI/Build Menu
///  Roomies/UI/Build Pause Menu
/// </summary>
internal static class RoomiesDayScreenBuilder
{
    private const string RootName = "UI_DayScreens";

    [MenuItem("Roomies/UI/Build Day Screens")]
    private static void Build() => Build(rebuild: false);

    [MenuItem("Roomies/UI/Rebuild Day Screens")]
    private static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog(
                "画面を作り直す",
                "3つの画面を作り直します。シーンで手を入れた位置や文字は元に戻ります。",
                "作り直す", "やめる"))
            return;

        Build(rebuild: true);
    }

    private static void Build(bool rebuild)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        var dayManager = Object.FindAnyObjectByType<DayManager>();
        if (dayManager == null)
        {
            Debug.LogError("[UI] DayManager が見つかりません。GameRoom シーンを開いてから実行してください。");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build day screens");
        }

        var transition = Ensure<DayTransitionUI>(root.transform, "DayTransitionUI", rebuild,
            ui => ui.EditorBuildUI());
        var result = Ensure<DayResultUI>(root.transform, "DayResultUI", rebuild,
            ui => ui.EditorBuildUI());
        var rent = Ensure<RentPaymentUI>(root.transform, "RentPaymentUI", rebuild,
            ui => ui.EditorBuildUI());

        // UIは GameRoomUI シーンにある。受け渡し口（GameUIRefs）へ割り当てる
        if (!AssignGameUIRefs(root, ("transitionUI", transition), ("resultUI", result), ("rentPaymentUI", rent)))
            return;

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[UI] 朝・収支・家賃の3画面を置き、GameUIRefs へ割り当てました（シーンを保存してください）");
    }

    /// <summary>
    /// ゲーム中のUIを GameRoomUI の受け渡し口（GameUIRefs）へ割り当てる。
    /// UIのルートが GameRoom 側にできていたら GameRoomUI へ移す（シーンをまたぐ参照は保存できないため）。
    /// GameRoom と GameRoomUI を両方開いて実行する。
    /// </summary>
    private static bool AssignGameUIRefs(GameObject uiRoot, params (string field, Object value)[] refs)
    {
        var hub = Object.FindAnyObjectByType<GameUIRefs>(FindObjectsInactive.Include);
        if (hub == null)
        {
            Debug.LogError("[UI] GameUIRefs が見つかりません。GameRoomUI シーンも開いてから実行してください" +
                           "（まだ分けていないなら『Roomies/UI/Split GameRoom UI』）。");
            return false;
        }

        if (uiRoot.scene != hub.gameObject.scene)
            SceneManager.MoveGameObjectToScene(uiRoot, hub.gameObject.scene);

        var so = new SerializedObject(hub);
        foreach (var (field, value) in refs)
            so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
        return true;
    }

    /// <summary>
    /// 子オブジェクトを用意して、中身が空なら組み立てさせる。
    /// すでに中身があるときは触らない（手で直した位置を壊さないため）。
    /// </summary>
    // ---------------- 夜イベントのバナー ----------------

    /// <summary>
    /// 置いてある夜バナーの見出しを「消えない」設定へ直す。作り直さないので、
    /// シーンで変えた位置や色は残る。何度実行しても同じ結果。
    /// </summary>
    [MenuItem("Roomies/UI/Fix Night Banner Title")]
    private static void FixNightBannerTitle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        var banner = Object.FindAnyObjectByType<NightEventUI>(FindObjectsInactive.Include);
        if (banner == null || !banner.EditorFixTitle())
        {
            Debug.LogError("[UI] 夜バナーが見つかりません。GameRoom シーンを開き、先に『Roomies/UI/Build Night Banner』を実行してください。");
            return;
        }

        EditorSceneManager.MarkSceneDirty(banner.gameObject.scene);
        Selection.activeGameObject = banner.gameObject;
        Debug.Log("[UI] 夜バナーの見出しを直しました（シーンを保存してください）");
    }

    [MenuItem("Roomies/UI/Build Night Banner")]
    private static void BuildNightBanner()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        if (Object.FindAnyObjectByType<DayManager>() == null)
        {
            Debug.LogError("[UI] DayManager が見つかりません。GameRoom シーンを開いてから実行してください。");
            return;
        }

        // 夜イベント管理もこれまで実行時に作っていたので、ここでシーンへ置く
        var manager = Object.FindAnyObjectByType<NightEventManager>();
        if (manager == null)
        {
            var managerObject = new GameObject("NightEventManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Build night event manager");
            manager = Undo.AddComponent<NightEventManager>(managerObject);
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build night banner");
        }

        var banner = Ensure<NightEventUI>(root.transform, "NightEventUI", false,
            ui => ui.EditorBuildUI());

        // 家具の模様替えもこれまで実行時に作っていたので、ここでシーンへ置く
        if (Object.FindAnyObjectByType<FurnitureEditController>() == null)
        {
            var editObject = new GameObject("FurnitureEditController");
            Undo.RegisterCreatedObjectUndo(editObject, "Build furniture edit controller");
            Undo.AddComponent<FurnitureEditController>(editObject);
        }

        // バナーは GameRoomUI シーンにある。受け渡し口（GameUIRefs）へ割り当てる
        if (!AssignGameUIRefs(root, ("nightEventUI", banner)))
            return;

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = banner.gameObject;
        Debug.Log("[UI] 夜イベントのバナー・NightEventManager・FurnitureEditController を置きました（シーンを保存してください）");
    }

    // ---------------- みんなの稼ぎ ----------------

    [MenuItem("Roomies/UI/Build Earning List")]
    private static void BuildEarningList()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        var list = Object.FindAnyObjectByType<PlayerEarningListUI>(FindObjectsInactive.Include);
        if (list == null)
        {
            Debug.LogError("[UI] PlayerEarningListUI が見つかりません。GameRoom シーンを開いてから実行してください。");
            return;
        }

        if (list.transform.Find("EarningListCanvas") != null)
        {
            Debug.Log("[UI] みんなの稼ぎの画面はすでに置かれています。");
            Selection.activeGameObject = list.gameObject;
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(list.gameObject, "Build earning list");
        list.EditorBuildUI();

        EditorSceneManager.MarkSceneDirty(list.gameObject.scene);
        Selection.activeGameObject = list.gameObject;
        Debug.Log("[UI] みんなの稼ぎの画面を置きました（シーンを保存してください）");
    }

    // ---------------- HUD ----------------

    [MenuItem("Roomies/UI/Build HUD")]
    private static void BuildHud()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        if (Object.FindAnyObjectByType<DayManager>() == null)
        {
            Debug.LogError("[UI] DayManager が見つかりません。GameRoom シーンを開いてから実行してください。");
            return;
        }

        var hud = Object.FindAnyObjectByType<HudThemer>(FindObjectsInactive.Include);
        if (hud == null)
        {
            var go = new GameObject("HudThemer");
            Undo.RegisterCreatedObjectUndo(go, "Build HUD");
            hud = Undo.AddComponent<HudThemer>(go);
        }

        if (hud.transform.Find("HudCanvas") != null)
        {
            Debug.Log("[UI] HUDはすでに置かれています。作り直すときは HudCanvas を消してから実行してください。");
            Selection.activeGameObject = hud.gameObject;
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Build HUD");
        hud.EditorBuildUI();

        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        Selection.activeGameObject = hud.gameObject;
        Debug.Log("[UI] HUDを置きました。既存の DayText / Timer Text / SharedMoneyText も中へ移しました" +
                  "（シーンを保存してください）");
    }

    // ---------------- メニューとロビー ----------------

    [MenuItem("Roomies/UI/Build Menu")]
    private static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        if (Object.FindAnyObjectByType<MainMenuManager>() == null)
        {
            Debug.LogError("[UI] MainMenuManager が見つかりません。MainMenuSteam シーンを開いてから実行してください。");
            return;
        }

        var themer = Object.FindAnyObjectByType<MenuThemer>(FindObjectsInactive.Include);
        if (themer == null)
        {
            var go = new GameObject("MenuThemer");
            Undo.RegisterCreatedObjectUndo(go, "Build menu");
            themer = Undo.AddComponent<MenuThemer>(go);
        }

        themer.EditorBuildUI();

        EditorSceneManager.MarkSceneDirty(themer.gameObject.scene);
        Selection.activeGameObject = themer.gameObject;
        Debug.Log("[UI] メニューとロビーの見た目を置きました（シーンを保存してください）");
    }

    // ---------------- ポーズ・設定 ----------------

    [MenuItem("Roomies/UI/Build Pause Menu")]
    private static void BuildPauseMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        if (Object.FindAnyObjectByType<DayManager>() == null)
        {
            Debug.LogError("[UI] DayManager が見つかりません。GameRoom シーンを開いてから実行してください。");
            return;
        }

        var esc = Object.FindAnyObjectByType<EscMenuUI>(FindObjectsInactive.Include);
        if (esc == null)
        {
            var go = new GameObject("EscMenuUI");
            Undo.RegisterCreatedObjectUndo(go, "Build pause menu");
            esc = Undo.AddComponent<EscMenuUI>(go);
        }

        if (esc.transform.Find("EscMenuCanvas") != null)
        {
            Debug.Log("[UI] ポーズ画面はすでに置かれています。作り直すときは EscMenuCanvas を消してから実行してください。");
            Selection.activeGameObject = esc.gameObject;
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(esc.gameObject, "Build pause menu");
        esc.EditorBuildUI();

        EditorSceneManager.MarkSceneDirty(esc.gameObject.scene);
        Selection.activeGameObject = esc.gameObject;
        Debug.Log("[UI] ポーズ・設定の画面を置きました（シーンを保存してください）");
    }

    private static T Ensure<T>(Transform parent, string name, bool rebuild, System.Action<T> build)
        where T : MonoBehaviour
    {
        Transform existing = parent.Find(name);

        if (existing != null && rebuild)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
            existing = null;
        }

        if (existing == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Build day screen");
            existing = go.transform;
        }

        T component = existing.GetComponent<T>();
        if (component == null) component = Undo.AddComponent<T>(existing.gameObject);

        // 中身（Canvas）がまだ無いときだけ組み立てる
        if (existing.childCount == 0) build(component);

        return component;
    }
}
#endif
