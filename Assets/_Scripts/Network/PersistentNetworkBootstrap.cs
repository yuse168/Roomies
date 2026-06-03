using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistentNetworkBootstrap
{
    private const string PersistentNetworkResourcePath = "PersistentNetwork";

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

        GameObject prefab =
            Resources.Load<GameObject>(PersistentNetworkResourcePath);

        if (prefab == null)
        {
            Debug.LogError(
                "[PersistentNetworkBootstrap] Resources/PersistentNetwork.prefab が見つかりません。"
            );
            return;
        }

        GameObject instance = Object.Instantiate(prefab);
        instance.name = prefab.name;
        Object.DontDestroyOnLoad(instance);
    }
}
