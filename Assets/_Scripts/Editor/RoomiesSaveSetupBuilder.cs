#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// セーブ（続きから）のための実体をシーンへ置くツール。
///
/// 規約（AGENTS.md）：実行時には作らない。ここでシーンに置いて保存する。
///  ・GameRoom      … `RunSaveManager`（保存と復元の係）
///  ・MainMenuSteam … 「続きから」ボタン（HostButtonを複製するので見た目はそろう）
///
/// メニュー：
///  Roomies/Save/Build Save Manager
///  Roomies/Save/Build Continue Button
///  Roomies/Save/Delete Save File
///  Roomies/Save/Open Save Folder
/// </summary>
internal static class RoomiesSaveSetupBuilder
{
    private const string SaveManagerName = "RunSaveManager";
    private const string ContinueButtonName = "ContinueButton";

    // 2行（「続きから」＋内容）が入る高さ。シーンで変えてよい
    private const float ContinueButtonHeight = 96f;

    // ---------------- GameRoom：保存の係 ----------------

    [MenuItem("Roomies/Save/Build Save Manager")]
    private static void BuildSaveManager()
    {
        if (!CheckNotPlaying()) return;

        GameObject existing = GameObject.Find(SaveManagerName);
        if (existing != null && existing.GetComponent<RunSaveManager>() != null)
        {
            Selection.activeGameObject = existing;
            Debug.Log("[Save] RunSaveManager はすでに置かれています。");
            return;
        }

        GameObject go = existing ?? new GameObject(SaveManagerName);
        if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Build save manager");
        if (go.GetComponent<RunSaveManager>() == null) Undo.AddComponent<RunSaveManager>(go);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[Save] RunSaveManager を置きました（シーンを保存してください）");
    }

    // ---------------- MainMenu：続きからボタン ----------------

    [MenuItem("Roomies/Save/Build Continue Button")]
    private static void BuildContinueButton()
    {
        if (!CheckNotPlaying()) return;

        var menu = Object.FindAnyObjectByType<MainMenuManager>();
        if (menu == null)
        {
            Debug.LogError("[Save] MainMenuManager が見つかりません。MainMenuSteam シーンを開いてから実行してください。");
            return;
        }

        GameObject hostObject = FindChildByName(menu.gameObject.scene, "HostButton");
        if (hostObject == null || hostObject.GetComponent<Button>() == null)
        {
            Debug.LogError("[Save] HostButton が見つかりません。見た目の複製元に使うので必要です。");
            return;
        }

        GameObject existing = FindChildByName(menu.gameObject.scene, ContinueButtonName);
        if (existing == null)
        {
            // 見た目はHostButtonの複製でそろえる（テーマを手で真似しない）
            existing = Object.Instantiate(hostObject, hostObject.transform.parent);
            existing.name = ContinueButtonName;
            existing.transform.SetSiblingIndex(hostObject.transform.GetSiblingIndex());
            Undo.RegisterCreatedObjectUndo(existing, "Build continue button");

            // HostButtonの真上に置く。位置はシーンビューで自由に動かせる
            var hostRect = hostObject.GetComponent<RectTransform>();
            var rect = existing.GetComponent<RectTransform>();
            if (hostRect != null && rect != null)
            {
                rect.sizeDelta = new Vector2(hostRect.sizeDelta.x, ContinueButtonHeight);
                rect.anchoredPosition = hostRect.anchoredPosition +
                                        new Vector2(0f, (hostRect.sizeDelta.y + ContinueButtonHeight) / 2f + 14f);
            }
        }

        var button = existing.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();   // 複製元の登録は残さない

        TMP_Text label = existing.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "続きから";

        // MainMenuManager へ割り当てる
        var so = new SerializedObject(menu);
        so.FindProperty("continueButton").objectReferenceValue = button;
        if (label != null) so.FindProperty("continueSummaryText").objectReferenceValue = label;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(existing.scene);
        Selection.activeGameObject = existing;
        Debug.Log("[Save] 「続きから」ボタンを置き、MainMenuManager へ割り当てました" +
                  "（位置はシーンビューで調整してください。シーンの保存も必要です）");
    }

    // ---------------- テスト用 ----------------

    [MenuItem("Roomies/Save/Delete Save File")]
    private static void DeleteSave()
    {
        if (!SaveSystem.Exists())
        {
            Debug.Log($"[Save] セーブはありません（{SaveSystem.FilePath}）");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "セーブを消す",
                $"このセーブを消します。元に戻せません。\n\n{SaveSystem.FilePath}",
                "消す", "やめる"))
            return;

        Debug.Log(SaveSystem.Delete() ? "[Save] セーブを消しました" : "[Save] セーブを消せませんでした");
    }

    [MenuItem("Roomies/Save/Open Save Folder")]
    private static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(SaveSystem.FilePath);
        Debug.Log($"[Save] {SaveSystem.FilePath}");
    }

    // ---------------- 小物 ----------------

    private static bool CheckNotPlaying()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
        Debug.LogError("[Save] Play Modeを抜けてから実行してください。");
        return false;
    }

    /// <summary>
    /// シーン内（非表示のものも含む）から名前で探す。
    /// `GameObject.Find` は非表示を見つけられないのでこちらを使う。
    /// </summary>
    private static GameObject FindChildByName(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
        }
        return null;
    }
}
#endif
