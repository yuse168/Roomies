using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 本編（GameRoom）に置き、ゲーム中のUIシーン（GameRoomUI）を Additive で読み込む。
///
/// ・ネットワーク中はホスト（サーバー）だけが NetworkManager の SceneManager で読み込む。
///   NGO が全員に1回ずつ読み込ませ、あとから入った人にも同期で届く。
///   クライアントは自分では読み込まない（自分でも読むと2つになる）。
/// ・ネットワークを使っていないとき（シーン単体の確認など）は、その場で読み込む。
/// ・ほかのシーンへ Single で移ると、UIシーンも一緒に外れる。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameUISceneLoader : MonoBehaviour
{
    [Tooltip("ゲーム中のUIシーンの名前（Build Settings に入れておく）。")]
    [SerializeField] private string uiSceneName = "GameRoomUI";

    [Tooltip("ネットワークが始まるのを待つ秒数。過ぎてもネットワークが無ければ、その場で読み込む。")]
    [SerializeField] private float offlineFallbackSeconds = 3f;

    private bool requested;
    private bool reportedFailure;
    private float waitedSeconds;

    public string UISceneName => uiSceneName;

    private void Update()
    {
        if (requested || IsUISceneLoaded()) { enabled = false; return; }

        NetworkManager network = NetworkManager.Singleton;
        bool networkRunning = network != null && network.IsListening;

        if (!networkRunning)
        {
            // まだホスト開始前（エディタでGameRoomを直接再生したとき）なら少し待つ
            waitedSeconds += Time.unscaledDeltaTime;
            if (network != null && waitedSeconds < offlineFallbackSeconds) return;

            SceneManager.LoadScene(uiSceneName, LoadSceneMode.Additive);
            requested = true;
            return;
        }

        // クライアントはホストからの指示（NGO）で読み込むので、何もしない
        if (!network.IsServer) { enabled = false; return; }

        if (!network.NetworkConfig.EnableSceneManagement || network.SceneManager == null)
        {
            // シーン同期を使っていない設定なら、各自がその場で読み込む
            SceneManager.LoadScene(uiSceneName, LoadSceneMode.Additive);
            requested = true;
            return;
        }

        SceneEventProgressStatus status =
            network.SceneManager.LoadScene(uiSceneName, LoadSceneMode.Additive);

        if (status == SceneEventProgressStatus.Started)
        {
            requested = true;
        }
        else if (status != SceneEventProgressStatus.SceneEventInProgress && !reportedFailure)
        {
            // ほかのシーンの読み込み中（SceneEventInProgress）なら次のフレームでもう一度
            reportedFailure = true;
            Debug.LogError($"[UI] ゲーム中のUIシーン「{uiSceneName}」を読み込めません（{status}）。" +
                           "Build Settings に入っているか確かめてください。", this);
            enabled = false;
        }
    }

    private bool IsUISceneLoaded()
    {
        Scene scene = SceneManager.GetSceneByName(uiSceneName);
        return scene.IsValid() && (scene.isLoaded || scene.isSubScene);
    }
}
