using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンロード時にテーマ適用コンポーネントを自動生成する。
/// シーンへの手動配置は不要（スクリプトを入れるだけで全シーンに効く）。
///  ・MainMenuManagerがあるシーン → MenuThemer（メニュー＆ロビーの着せ替え）
///  ・HUD（HudThemer）はシーンに実体で置いてあるので、ここでは作らない
/// </summary>
public static class UIThemeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    private static void Apply()
    {
        if (Object.FindAnyObjectByType<MainMenuManager>() != null &&
            Object.FindAnyObjectByType<MenuThemer>() == null)
        {
            new GameObject("MenuThemer").AddComponent<MenuThemer>();
        }

        // HUDはシーンに実体で置いてある（`Roomies/UI/Build HUD`）。ここでは作らない
        if (Object.FindAnyObjectByType<DayManager>() != null &&
            Object.FindAnyObjectByType<HudThemer>() == null)
        {
            Debug.LogError("[UI] HUDがシーンにありません。メニュー『Roomies/UI/Build HUD』で置いてください。");
        }
    }
}
