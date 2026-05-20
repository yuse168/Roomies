using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

// MainMenuManager
// タイトル画面のボタン管理を行うコード。
public class MainMenuManager : MonoBehaviour
{
    // タイトル画面用のカメラ
    // HOST時に削除して、Player側カメラへ切り替える
    public Camera menuCamera;

    // HOSTボタン
    // Hostとしてゲームを開始する
    public void Host()
    {
        // メニュー用カメラを削除
        if (menuCamera != null)
        {
            Destroy(menuCamera.gameObject);
        }

        // Host開始
        NetworkManager.Singleton.StartHost();

        // GameRoomへ移動
        NetworkManager.Singleton.SceneManager.LoadScene("GameRoom", LoadSceneMode.Single);
    }

    // JOINボタン
    // Clientとしてゲームへ参加する
    public void Join()
    {
        NetworkManager.Singleton.StartClient();
    }

    // QUITボタン
    // ゲームを終了する
    public void Quit()
    {
        Application.Quit();
    }
}