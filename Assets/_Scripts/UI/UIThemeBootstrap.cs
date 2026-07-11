using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンロード時にテーマ適用コンポーネントを自動生成する。
/// シーンへの手動配置は不要（スクリプトを入れるだけで全シーンに効く）。
///  ・MainMenuManagerがあるシーン → MenuThemer（メニュー＆ロビーの着せ替え）
///  ・DayManagerがあるシーン      → HudThemer（ゲーム中HUDの着せ替え）
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

        if (Object.FindAnyObjectByType<DayManager>() != null &&
            Object.FindAnyObjectByType<HudThemer>() == null)
        {
            new GameObject("HudThemer").AddComponent<HudThemer>();
        }
    }
}
