using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 見た目の係がシーンに置かれているかを見張るだけのクラス。
/// 以前は実行時に作っていたが、いまはシーンに実体で置いてある（AGENTS.mdの規約）。
///  ・メニューとロビー（MenuThemer）もシーンに実体で置いてあるので、ここでは作らない
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
        // メニューとロビーもシーンに実体で置いてある（`Roomies/UI/Build Menu`）
        if (Object.FindAnyObjectByType<MainMenuManager>() != null &&
            Object.FindAnyObjectByType<MenuThemer>() == null)
        {
            Debug.LogError("[UI] メニューの見た目がシーンにありません。メニュー『Roomies/UI/Build Menu』で置いてください。");
        }

        // HUDはゲーム中のUIシーン（GameRoomUI）に実体で置いてある（`Roomies/UI/Build HUD`）。ここでは作らない。
        // UIシーンはマップより後に読み込まれるので、UIシーンが読み込まれてから確かめる
        if (SceneManager.GetSceneByName("GameRoomUI").isLoaded &&
            Object.FindAnyObjectByType<DayManager>() != null &&
            Object.FindAnyObjectByType<HudThemer>() == null)
        {
            Debug.LogError("[UI] HUDがシーンにありません。メニュー『Roomies/UI/Build HUD』で置いてください。");
        }
    }
}
