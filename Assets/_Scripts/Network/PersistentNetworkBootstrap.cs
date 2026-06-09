using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistentNetworkBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenuSteam" &&
            scene.name != "GameRoom")
        {
            return;
        }

        EnsurePersistentNetwork();
    }

    private static void EnsurePersistentNetwork()
    {
        if (NetworkManager.Singleton != null ||
            Object.FindAnyObjectByType<NetworkManager>() != null)
        {
            return;
        }

        Debug.LogError(
            "[PersistentNetworkBootstrap] NetworkManager がシーンにありません。MainMenuSteam/GameRoom には PersistentNetwork を事前配置してください。"
        );
    }
}
