using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static RoomiesPlayHarness;
using Object = UnityEngine.Object;

/// <summary>
/// HUD・ESC メニュー・タイトル/ロビーの見た目を撮るための道具（Explicit。名前を指定したときだけ動く）。
/// 撮るだけで、見た目の良し悪しは人が見る。ボタンが押せるか（Raycast を受けるか・interactable か）は数で残す。
/// 環境変数：ROOMIES_CAPTURE_DIR、ROOMIES_CAPTURE_TAG（before / after など）
/// </summary>
[TestFixture, Explicit("UIの撮影用。名前を指定したときだけ動く"), Category("Capture")]
public class RoomiesUICaptureTests : IPrebuildSetup, IPostBuildCleanup
{
    readonly RoomiesPlayLoopTests startScene = new();
    public void Setup() => startScene.Setup();
    public void Cleanup() => startScene.Cleanup();

    static string Tag => Environment.GetEnvironmentVariable("ROOMIES_CAPTURE_TAG") ?? "ui";

    [UnityTest, Order(1)]
    public IEnumerator GameHudAndPauseMenu()
    {
        LogAssert.ignoreFailingMessages = true;
        BackupRealSave();
        try
        {
            yield return BootHost();
            yield return new WaitForSecondsRealtime(0.5f);
            Capture($"{Tag}_hud_day");

            var esc = Object.FindAnyObjectByType(GameType("EscMenuUI"));
            Assert.That(esc, Is.Not.Null);
            Call(esc, "OpenMenu");
            yield return new WaitForSecondsRealtime(0.6f);
            Capture($"{Tag}_pause_main");
            ReportButtons("pause_main");

            Call(esc, "OpenSettings");
            yield return new WaitForSecondsRealtime(0.6f);
            Capture($"{Tag}_pause_settings");
            ReportButtons("pause_settings");

            Call(esc, "OpenKeyBindings");
            yield return new WaitForSecondsRealtime(0.6f);
            Capture($"{Tag}_pause_keys");
            ReportButtons("pause_keys");

            Call(esc, "ShowMain");
            yield return new WaitForSecondsRealtime(0.4f);
            var confirm = Enum.Parse(GameType("EscMenuUI").GetNestedType("ConfirmAction", All), "MainMenu");
            Call(esc, "ShowConfirmation", confirm);
            yield return new WaitForSecondsRealtime(0.6f);
            Capture($"{Tag}_pause_confirm");
            Call(esc, "CloseMenu");
            yield return new WaitForSecondsRealtime(0.4f);

            yield return GoToNight();
            yield return new WaitForSecondsRealtime(4.5f); // 夜のバナーが引っ込んだあとの HUD
            Capture($"{Tag}_hud_night");
        }
        finally
        {
            if (Network != null) { Network.Shutdown(); Object.Destroy(Network.gameObject); }
            RestoreRealSave();
        }
    }

    [UnityTest, Order(2)]
    public IEnumerator TitleAndLobby()
    {
        LogAssert.ignoreFailingMessages = true;
        if (Network != null) yield return ShutdownToEmptyScene();
        var op = SceneManager.LoadSceneAsync("MainMenuSteam", LoadSceneMode.Single);
        while (!op.isDone) yield return null;
        yield return new WaitForSecondsRealtime(2.5f); // 登場アニメ
        Capture($"{Tag}_title");
        ReportButtons("title");

        var themer = Object.FindAnyObjectByType(GameType("MenuThemer"));
        var play = (Button)Get(themer, "playButton");
        play.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1.2f);
        Capture($"{Tag}_title_rooms");
        ReportButtons("title_rooms");

        // 部屋にはいる（コード入力）
        var menu = Object.FindAnyObjectByType(GameType("MainMenuManager"));
        var join = (Button)Get(menu, "joinButton");
        join.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.8f);
        Capture($"{Tag}_join_code");
        ReportButtons("join_code");
        ((Button)Get(menu, "cancelJoinButton"))?.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);

        // ロビー：本物の Steam ロビーは作らず、表示だけ出して仮の参加者を並べる
        var lobby = Object.FindAnyObjectByType(GameType("LobbyUIManager"));
        Assert.That(lobby, Is.Not.Null);
        Call(lobby, "ShowLobbyUI", true);
        var co = (Coroutine)Get(lobby, "refreshCoroutine");
        if (co != null) ((MonoBehaviour)lobby).StopCoroutine(co); // 1.5秒ごとの組み直しで仮の行が消えないように
        var parent = (Transform)Get(lobby, "playerListParent");
        foreach (Transform child in parent) Object.Destroy(child.gameObject);
        yield return null;
        var colors = (Color[])Get(GameType("LobbyUIManager"), "PlayerColors");
        var steamIdType = Type.GetType("Steamworks.CSteamID, com.rlabrecque.steamworks.net", true);
        string[] names = { "ゆせ", "そそ", "りゅうしん", "LongPlayerName_Example", "Leica" };
        for (int i = 0; i < names.Length; i++)
        {
            var id = Activator.CreateInstance(steamIdType, (ulong)(76561190000000000 + (ulong)i));
            Call(lobby, "CreatePlayerRow", id, names[i], colors[i % colors.Length], i == 0, i == 0);
        }
        Call(lobby, "FitListToWidth");
        var code = (Component)Get(lobby, "partyCodeText");
        if (code != null) code.GetType().GetProperty("text").SetValue(code, "AB12C");
        yield return new WaitForSecondsRealtime(1.5f);
        Capture($"{Tag}_lobby_host5");
        ReportButtons("lobby_host5");
    }

    /// <summary>表示中のボタンのうち、押せない物（interactable でない・前に何か重なって Raycast を受けない）を書き出す。</summary>
    static void ReportButtons(string where)
    {
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Where(b => b.isActiveAndEnabled).ToArray();
        var es = UnityEngine.EventSystems.EventSystem.current;
        string lines = "";
        foreach (var b in buttons)
        {
            var rt = (RectTransform)b.transform;
            var canvas = b.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.enabled) continue;
            var group = b.GetComponentsInParent<CanvasGroup>().ToArray();
            if (group.Any(g => g.alpha < 0.05f)) continue; // 見えていない
            string label = b.GetComponentsInChildren<Component>(true).Where(c => c != null).Select(c => c.GetType().GetProperty("text")?.GetValue(c) as string).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? b.name;
            bool blocked = group.Any(g => !g.blocksRaycasts || !g.interactable);
            // 中心に Raycast して、いちばん手前がこのボタン（かその子）か
            bool hit = true;
            string blocker = "";
            if (es != null)
            {
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
                var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = screen }, results);
                hit = results.Count > 0 && results[0].gameObject.transform.IsChildOf(b.transform);
                if (!hit && results.Count > 0)
                {
                    var t = results[0].gameObject.transform;
                    blocker = t.parent != null ? t.parent.name + "/" + t.name : t.name;
                }
            }
            if (!b.interactable || blocked || !hit)
                lines += $"  ✕ {label.Replace('\n', ' ')} ({b.name}) interactable={b.interactable} groupBlocked={blocked} frontmost={hit} {(blocker != "" ? "手前=" + blocker : "")}\n";
        }
        Debug.Log($"[UIButtons] {where}: buttons={buttons.Length}\n{lines}");
        TestContext.WriteLine($"[UIButtons] {where}: buttons={buttons.Length}\n{lines}");
    }
}
