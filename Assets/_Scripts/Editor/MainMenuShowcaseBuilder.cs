#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainMenuSteamへゲーム本編と同じ部屋FBXと表示専用Roomiesを安全に配置する。
/// ネットワークPrefabやゲーム本編のManagerは追加しない。
/// </summary>
[InitializeOnLoad]
internal static class MainMenuShowcaseBuilder
{
    private const string ScenePath = "Assets/_Scenes/MainMenuSteam.unity";
    private const string GameRoomScenePath = "Assets/_Scenes/GameRoom.unity";
    private const string CharacterPath = "Assets/_Models/Bob/Bobs.fbx";
    private const string SkyboxPath =
        "Assets/AllSkyFree/Epic_GloriousPink/Epic_GloriousPink.mat";
    private const string RootName = "MenuRoomShowcase";

    static MainMenuShowcaseBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    [MenuItem("Roomies/Build Main Menu Showcase")]
    private static void BuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
            return;

        Scene menuScene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !menuScene.IsValid() || !menuScene.isLoaded;
        if (openedForBuild)
            menuScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GameObject existingHost =
            menuScene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
        MainMenuRoomShowcase existingShowcase =
            existingHost != null ? existingHost.GetComponent<MainMenuRoomShowcase>() : null;
        if (existingShowcase != null && existingShowcase.IsCurrentBuild)
        {
            if (openedForBuild) EditorSceneManager.CloseScene(menuScene, true);
            return;
        }

        GameObject characterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
        Material skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        if (characterAsset == null || skyboxMaterial == null)
        {
            Debug.LogError(
                "[Roomies] メインメニュー用キャラクターまたはSkyboxが見つかりません。");
            if (openedForBuild) EditorSceneManager.CloseScene(menuScene, true);
            return;
        }

        Scene gameRoomScene = SceneManager.GetSceneByPath(GameRoomScenePath);
        bool openedGameRoom = !gameRoomScene.IsValid() || !gameRoomScene.isLoaded;
        if (openedGameRoom)
        {
            gameRoomScene = EditorSceneManager.OpenScene(
                GameRoomScenePath, OpenSceneMode.Additive);
        }

        GameObject sourceRoom =
            gameRoomScene.GetRootGameObjects().FirstOrDefault(go => go.name == "Demo");
        if (sourceRoom == null)
        {
            Debug.LogError("[Roomies] GameRoom内の部屋モデル「Demo」が見つかりません。");
            if (openedGameRoom) EditorSceneManager.CloseScene(gameRoomScene, true);
            if (openedForBuild) EditorSceneManager.CloseScene(menuScene, true);
            return;
        }

        if (existingHost != null)
            Object.DestroyImmediate(existingHost);

        var host = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(host, menuScene);

        // GameRoom側のマテリアル差し替え・削除オーバーライドも含めて複製する。
        var room = Object.Instantiate(sourceRoom);
        room.name = "MenuRoom";
        SceneManager.MoveGameObjectToScene(room, menuScene);
        room.transform.SetParent(host.transform, true);

        Vector3[] positions =
        {
            new Vector3(15.7f, 5.72f, 16.6f),
            new Vector3(17.3f, 5.72f, 17.1f),
            new Vector3(18.9f, 5.72f, 16.5f),
        };
        float[] rotations = { 18f, 0f, -18f };
        var characters = new Transform[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            var character =
                (GameObject)PrefabUtility.InstantiatePrefab(characterAsset, menuScene);
            character.name = $"MenuRoomie_{i + 1}";
            character.transform.SetParent(host.transform, true);
            character.transform.SetPositionAndRotation(
                positions[i], Quaternion.Euler(0f, 180f + rotations[i], 0f));
            characters[i] = character.transform;
        }

        MainMenuRoomShowcase showcase = host.AddComponent<MainMenuRoomShowcase>();
        showcase.Configure(room.transform, characters, skyboxMaterial);
        EditorUtility.SetDirty(showcase);
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);
        Debug.Log("[Roomies] MainMenuSteamへ3Dルーム背景を配置しました。");

        if (openedGameRoom) EditorSceneManager.CloseScene(gameRoomScene, true);
        if (openedForBuild) EditorSceneManager.CloseScene(menuScene, true);
    }
}
#endif
