using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ゲームオーバー画面。家賃を払えなかったときに全員へ出す。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・パネル・文字・ボタンはプレハブの子として置いてある。ここでは表示の切り替えと文言の差し込みだけ。
///  ・置き直しは メニュー Roomies/UI/Build Game Over Screen。
///
/// ボタン：
///  ・「もう一度」… ホストだけに出る。Day1の朝からやり直す（家具と所持金はそのまま）。
///  ・「メインメニューへ」… 全員に出る。ロビーから退出する。
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    [Header("表示（プレハブの子を割り当てる）")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI reasonLabel;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    [Header("文言")]
    [SerializeField] private string title = "GAME OVER";
    [SerializeField] private string reasonFormat = "家賃 ¥{0:N0} を払えなかった。\n大家さんに追い出されました……";

    [Header("メインメニューへ")]
    [Tooltip("「メインメニューへ」で戻るシーン。")]
    [SerializeField] private string menuSceneName = "MainMenuSteam";

    private bool isLeaving;

    // カードの中の並び（上から）。理由の文が3行（34pt）になってもボタンに重ならない高さにしてある。
    // 配置ツール（GameOverScreenBuilder）と、置いてある画面の直し（EditorFixLayout）の両方で使う。
    public const float CardHeight   = 560f;
    public const float TitleTop     = -50f;
    public const float TitleHeight  = 110f;
    public const float ReasonTop    = -176f;
    public const float ReasonHeight = 170f;
    public const float ButtonBottom = 50f;

#if UNITY_EDITOR
    /// <summary>
    /// 置いてある画面の並びを直す（見出し・理由・ボタンを重ならない位置へ）。色・文言・フォントは変えない。
    /// 配置ツール `Roomies/UI/Fix Overlapping Layouts` から呼ぶ。直せたら true。
    /// </summary>
    public bool EditorFixLayout()
    {
        if (titleLabel == null || reasonLabel == null || retryButton == null || menuButton == null) return false;
        if (!(titleLabel.transform.parent is RectTransform card)) return false;

        var rects = new Object[]
        {
            card, titleLabel.rectTransform, reasonLabel.rectTransform,
            retryButton.transform, menuButton.transform,
        };
        UnityEditor.Undo.RecordObjects(rects, "Fix game over layout");

        card.sizeDelta = new Vector2(card.sizeDelta.x, CardHeight);
        SetTop(titleLabel.rectTransform, TitleTop, TitleHeight);
        SetTop(reasonLabel.rectTransform, ReasonTop, ReasonHeight);
        SetBottom((RectTransform)retryButton.transform, ButtonBottom);
        SetBottom((RectTransform)menuButton.transform, ButtonBottom);
        return true;
    }

    private static void SetTop(RectTransform rect, float top, float height)
    {
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
    }

    private static void SetBottom(RectTransform rect, float bottom)
    {
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom);
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (root != null) root.SetActive(false);

        if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        if (menuButton != null) menuButton.onClick.AddListener(OnReturnToMenu);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>全クライアントで呼ばれる（DayManagerのゲームオーバー通知から）。</summary>
    public void Show(int rent)
    {
        if (root == null) return;

        if (titleLabel != null) titleLabel.text = title;
        if (reasonLabel != null) reasonLabel.text = string.Format(reasonFormat, rent);

        // やり直しはホストだけ
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        if (retryButton != null) retryButton.gameObject.SetActive(isHost);

        root.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    private void OnRetry()
    {
        DayManager day = DayManager.Instance;
        if (day == null) return;

        day.ServerRestartGame();
        Hide();
    }

    /// <summary>
    /// ネットワークを切ってからタイトルへ戻る。ポーズの「メインメニューへ」（EscMenuUI）と同じ手順。
    /// セーブはここでは触らない（負けたときの削除は RunSaveManager が行う）。
    /// </summary>
    private void OnReturnToMenu()
    {
        if (isLeaving) return;
        isLeaving = true;
        Hide();
        StartCoroutine(ReturnToMenuRoutine());
    }

    private IEnumerator ReturnToMenuRoutine()
    {
        if (SteamLobby.Instance != null)
            SteamLobby.Instance.LeaveLobby();
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var network = NetworkManager.Singleton;
        while (network != null && network.ShutdownInProgress) yield return null;
        SceneManager.LoadScene(menuSceneName);
    }
}
