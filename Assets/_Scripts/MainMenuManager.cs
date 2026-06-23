using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

/// <summary>
/// MainMenu画面のUI制御。
/// Host/Joinボタン → SteamLobby経由でロビー作成/参加 → LobbyUIManagerが待機画面を管理。
/// LobbyUIManagerと同じGameObjectに付けるか、Inspectorで参照を繋げること。
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu Panel")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Join Panel")]
    [SerializeField] private GameObject   joinPanel;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button        confirmJoinButton;
    [SerializeField] private Button        cancelJoinButton;
    [SerializeField] private TMP_Text      joinStatusText;

    [Header("Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        if (joinPanel != null) joinPanel.SetActive(false);
        HideStatus();
    }

    private void OnEnable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.BusyStateChanged += OnBusyChanged;
            SteamLobby.Instance.OnJoinFailed     += OnJoinFailed;
        }

        // ボタン登録
        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(OnHostClicked);
        }
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinClicked);
        }
        if (confirmJoinButton != null)
        {
            confirmJoinButton.onClick.RemoveAllListeners();
            confirmJoinButton.onClick.AddListener(OnConfirmJoin);
        }
        if (cancelJoinButton != null)
        {
            cancelJoinButton.onClick.RemoveAllListeners();
            cancelJoinButton.onClick.AddListener(OnCancelJoin);
        }

        UpdateButtons();
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.BusyStateChanged -= OnBusyChanged;
            SteamLobby.Instance.OnJoinFailed     -= OnJoinFailed;
        }
    }

    // =========================================================
    // ボタン処理
    // =========================================================

    private void OnHostClicked()
    {
        // 診断ログ
        Debug.Log($"[MainMenuManager] OnHostClicked — SteamLobby.Instance={SteamLobby.Instance != null}, " +
                  $"SteamManager.Initialized={SteamManager.Initialized}");

        if (SteamLobby.Instance == null)
        {
            ShowStatus("SteamLobbyが見つかりません");
            Debug.LogError("[MainMenuManager] SteamLobby.Instance is null");
            return;
        }
        if (!SteamManager.Initialized)
        {
            ShowStatus("Steam未起動です");
            Debug.LogError("[MainMenuManager] SteamManager not initialized");
            return;
        }
        if (SteamLobby.Instance.IsBusy)
        {
            ShowStatus("処理中...");
            return;
        }

        HideStatus();
        ShowStatus("ロビーを作成中...");
        SteamLobby.Instance.CreateLobby();
    }

    private void OnJoinClicked()
    {
        if (SteamLobby.Instance == null)
        {
            ShowStatus("Steamが起動していません");
            return;
        }
        if (SteamLobby.Instance.IsBusy)
        {
            ShowStatus("処理中...");
            return;
        }

        HideStatus();

        // Join入力パネルを表示
        if (joinPanel != null) joinPanel.SetActive(true);
        if (joinCodeInput != null) joinCodeInput.text = "";
        UpdateButtons();
    }

    private void OnConfirmJoin()
    {
        if (joinCodeInput == null || SteamLobby.Instance == null) return;

        string code = joinCodeInput.text.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            ShowStatus("コードを入力してください");
            return;
        }

        // 18桁以上のulongはLobbyID直接参加（デバッグ用）
        if (code.Length >= 10 && ulong.TryParse(code, out ulong lobbyId))
        {
            Debug.Log($"[MainMenuManager] LobbyID直接参加: {lobbyId}");
            if (joinPanel != null) joinPanel.SetActive(false);
            ShowStatus("参加中...");
            SteamLobby.Instance.JoinLobby(new CSteamID(lobbyId));
        }
        else
        {
            if (joinPanel != null) joinPanel.SetActive(false);
            ShowStatus("ロビーを検索中...");
            SteamLobby.Instance.JoinLobbyWithCode(code);
        }
    }

    private void OnCancelJoin()
    {
        if (joinPanel != null) joinPanel.SetActive(false);
        HideStatus();
        UpdateButtons();
    }

    // =========================================================
    // イベントハンドラ
    // =========================================================

    private void OnBusyChanged(bool isBusy)
    {
        UpdateButtons();
        if (!isBusy) HideStatus();
    }

    private void OnJoinFailed(string reason)
    {
        ShowStatus($"参加失敗: {reason}");
        if (joinPanel != null) joinPanel.SetActive(false);
        UpdateButtons();
    }

    // =========================================================
    // UI ユーティリティ
    // =========================================================

    private void UpdateButtons()
    {
        bool busy = SteamLobby.Instance != null && SteamLobby.Instance.IsBusy;
        bool joinOpen = joinPanel != null && joinPanel.activeSelf;

        if (hostButton != null) hostButton.interactable = !busy && !joinOpen;
        if (joinButton != null) joinButton.interactable = !busy && !joinOpen;
    }

    private void ShowStatus(string msg)
    {
        if (joinStatusText == null) return;
        joinStatusText.text = msg;
        joinStatusText.gameObject.SetActive(true);
    }

    private void HideStatus()
    {
        if (joinStatusText != null)
            joinStatusText.gameObject.SetActive(false);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
