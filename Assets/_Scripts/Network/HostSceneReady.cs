using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ホストが本編のシーンと、ゲーム中の UI のシーン（GameUISceneLoader が追加で読み込む GameRoomUI）の
/// 両方を読み込み終わったときに、1 回だけ知らせる。
///
/// クライアントをつなぐ合図は、これを待ってから出す。
/// ホストがシーンを読み込んでいる最中にクライアントがつながると、同期の一覧と読み込みの指示の両方で UI のシーンが届き、
/// クライアントで UI のシーンが 2 つになって同期が止まる（NGO の例外 "Server Scene Handle already exist"）。
/// 本編の読み込みが終わった直後は、まだ UI のシーンを読み込んでいる途中なので、そこも待つ。
/// </summary>
public static class HostSceneReady
{
    /// <summary>
    /// 待ちはじめる。返り値を呼ぶと、知らせる前に取り消せる。
    /// uiWaitSeconds：UI のシーンがこの秒数たっても来なければ、待つのをやめて知らせる（進行不能にしないため）。
    /// </summary>
    public static Action WhenLoaded(NetworkManager network, MonoBehaviour runner, string gameSceneName, float uiWaitSeconds, Action onReady)
    {
        if (network == null || network.SceneManager == null) { onReady?.Invoke(); return () => { }; }

        bool gameLoaded = false, done = false;
        string uiSceneName = null;
        Coroutine fallback = null;
        NetworkSceneManager.OnLoadCompleteDelegateHandler handler = null;

        void Finish(bool notify)
        {
            if (done) return;
            done = true;
            if (network != null && network.SceneManager != null) network.SceneManager.OnLoadComplete -= handler;
            if (fallback != null && runner != null) runner.StopCoroutine(fallback);
            if (notify) onReady?.Invoke();
        }

        IEnumerator Fallback()
        {
            yield return new WaitForSecondsRealtime(uiWaitSeconds);
            if (done) yield break;
            Debug.LogWarning($"[HostSceneReady] UI のシーン「{uiSceneName}」の読み込みが {uiWaitSeconds:F0} 秒たっても終わりません。待たずに進めます。");
            Finish(true);
        }

        handler = (clientId, sceneName, mode) =>
        {
            if (done || network == null || clientId != network.LocalClientId) return;

            if (!gameLoaded)
            {
                if (sceneName != gameSceneName) return;
                gameLoaded = true;

                // UI のシーンを追加で読み込む物が無ければ、ここで終わり
                var loader = UnityEngine.Object.FindAnyObjectByType<GameUISceneLoader>();
                if (loader == null || !network.NetworkConfig.EnableSceneManagement) { Finish(true); return; }

                uiSceneName = loader.UISceneName;
                Scene ui = SceneManager.GetSceneByName(uiSceneName);
                if (ui.IsValid() && ui.isLoaded) { Finish(true); return; }
                if (runner != null) fallback = runner.StartCoroutine(Fallback());
                return;
            }

            if (sceneName == uiSceneName) Finish(true);
        };

        network.SceneManager.OnLoadComplete += handler;
        return () => Finish(false);
    }
}
