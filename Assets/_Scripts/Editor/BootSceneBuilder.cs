#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 起動ロゴ用のBootSceneを作り、Build Settingsの先頭へ登録する。
/// MainMenuShowcaseBuilderと同じ方式で、必要なときだけ自動実行する。
///
/// BootSceneの中身は最小限（カメラ ＋ LNStudioSplash）で、
/// Canvas以下はLNStudioSplashがランタイム生成する。
/// </summary>
[InitializeOnLoad]
internal static class BootSceneBuilder
{
    private const string BootScenePath = "Assets/_Scenes/BootScene.unity";
    private const string MenuScenePath = "Assets/_Scenes/MainMenuSteam.unity";
    private const string SplashRootName = "LNStudioSplash";

    static BootSceneBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    [MenuItem("Roomies/Build Boot Scene")]
    private static void BuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;

        bool created = EnsureScene();
        EnsureBuildSettings(created);
        EnsurePlayModeStartScene();
    }

    /// <summary>BootSceneが無ければ作る。既にあれば中身だけ確認して補う。</summary>
    private static bool EnsureScene()
    {
        if (System.IO.File.Exists(BootScenePath))
        {
            RepairExistingScene();
            return false;
        }

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        CreateContents(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, BootScenePath);
        EditorSceneManager.CloseScene(scene, true);

        Debug.Log("[Roomies] BootScene（LN Studioロゴ演出）を作成しました。");
        return true;
    }

    /// <summary>既存のBootSceneからLNStudioSplashが失われていた場合に付け直す。</summary>
    private static void RepairExistingScene()
    {
        Scene scene = SceneManager.GetSceneByPath(BootScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Additive);

        bool hasSplash = scene.GetRootGameObjects()
            .Any(go => go.GetComponent<LNStudioSplash>() != null);

        if (!hasSplash)
        {
            CreateContents(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Roomies] BootSceneへLNStudioSplashを再設定しました。");
        }

        if (openedHere) EditorSceneManager.CloseScene(scene, true);
    }

    private static void CreateContents(Scene scene)
    {
        // UIはScreenSpaceOverlayなので描画にカメラは不要だが、
        // 「No cameras rendering」警告を出さないために黒いカメラを置く。
        if (!scene.GetRootGameObjects().Any(go => go.GetComponent<Camera>() != null))
        {
            var cameraObject = new GameObject("Boot Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        var splashObject = new GameObject(SplashRootName, typeof(LNStudioSplash));
        SceneManager.MoveGameObjectToScene(splashObject, scene);
    }

    /// <summary>BootSceneをIndex 0、MainMenuSteamをその次にする。</summary>
    private static void EnsureBuildSettings(bool sceneWasCreated)
    {
        List<EditorBuildSettingsScene> scenes =
            EditorBuildSettings.scenes.ToList();

        int bootIndex = scenes.FindIndex(s => s.path == BootScenePath);
        bool alreadyFirst = bootIndex == 0 && scenes[0].enabled;

        // 既に先頭にあり、シーンも作り直していないなら何もしない
        if (alreadyFirst && !sceneWasCreated) return;

        if (bootIndex >= 0) scenes.RemoveAt(bootIndex);

        var bootEntry = new EditorBuildSettingsScene(BootScenePath, true);
        scenes.Insert(0, bootEntry);

        // メインメニューが登録されていない構成は想定外なので、無ければ足す
        if (!scenes.Any(s => s.path == MenuScenePath) &&
            System.IO.File.Exists(MenuScenePath))
        {
            scenes.Insert(1, new EditorBuildSettingsScene(MenuScenePath, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[Roomies] Build SettingsのIndex 0をBootSceneにしました。");
    }

    /// <summary>
    /// EditorでGameRoomなどを開いたままPlayしても、製品起動と同じBootSceneから始める。
    /// </summary>
    private static void EnsurePlayModeStartScene()
    {
        SceneAsset bootScene =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
        if (bootScene == null ||
            EditorSceneManager.playModeStartScene == bootScene)
            return;

        EditorSceneManager.playModeStartScene = bootScene;
        Debug.Log("[Roomies] EditorのPlay開始SceneをBootSceneにしました。");
    }
}
#endif
