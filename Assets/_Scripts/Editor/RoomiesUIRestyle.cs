#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 置いてあるUIの見た目を、夜の部屋に合わせた「半透明の黒い面＋細い白フチ」へそろえる。
/// メニュー「Roomies/UI/Restyle Game UI」（GameRoomUI を開いて実行）。何度実行しても同じ結果。
/// 作り直しはしない。色・位置・フォントだけを変えるので、参照と部品の名前はそのまま残る。
/// </summary>
internal static class RoomiesUIRestyle
{
    // 2026/11/13 より前の UITheme の色（シーンに保存されている値）
    static readonly Color OldInk = new Color(0.055f, 0.105f, 0.14f);
    static readonly Color OldInkSoft = new Color(0.12f, 0.22f, 0.25f);
    static readonly Color OldEdge = new Color(0.72f, 0.86f, 0.83f);

    [MenuItem("Roomies/UI/Restyle Game UI")]
    internal static void RestyleGameUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath("Assets/_Scenes/GameRoomUI.unity");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open GameRoomUI first.");
        int recolored = Recolor(scene);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var hud in root.GetComponentsInChildren<HudThemer>(true)) hud.EditorApplyRestyle();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Roomies Restyle] Game UI restyled ({recolored} surfaces recolored). GameRoomUI saved.");
    }

    /// <summary>前の色のまま置いてある面とフチを、いまの UITheme の色へ塗り替える。</summary>
    internal static int Recolor(Scene scene)
    {
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                Color c = image.color; Color target;
                if (Near(c, OldInk) || Near(c, UITheme.MenuInk)) target = UITheme.MenuInk;
                else if (Near(c, OldInkSoft) || Near(c, UITheme.MenuInkSoft)) target = UITheme.MenuInkSoft;
                else if (Near(c, OldEdge)) target = UITheme.MenuEdge;
                else continue;
                // もとが半透明だった面は、その薄さを保つ
                if (c.a < 0.7f) target.a = Mathf.Min(target.a, c.a);
                Undo.RecordObject(image, "Restyle UI");
                image.color = target; EditorUtility.SetDirty(image); count++;
            }
        return count;
    }

    static bool Near(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.006f && Mathf.Abs(a.g - b.g) < 0.006f && Mathf.Abs(a.b - b.b) < 0.006f;
}
#endif
