using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メインメニュー＆ロビー画面（MainMenuSteamシーン）の着せ替え。
/// シーン編集はせず、既存のUIオブジェクトを名前で見つけてランタイムでレストアする。
/// UIThemeBootstrapが自動生成する。
///
/// 対象（MainMenuSteamシーンのオブジェクト名）:
///  BG / Title / Subtitle / HostButton / JoinButton / QuitButton / StatusText
///  JoinPanel / Card / CodeLabel / CodeInputField / ConfirmJoinButton / CancelButton
///  LobbyPanel / CodeBox / PartyCodeText / CopyButton / CopyFeedbackText
///  PlayersLabel / StartButton / LeaveButton / InviteButton / WaitingText / ErrorText
/// </summary>
public class MenuThemer : MonoBehaviour
{
    private TMP_Text waitingText;
    private float pulseTime;

    private void Awake()
    {
        ApplyBackground();
        ApplyTitle();
        ApplyMainButtons();
        ApplyJoinPanel();
        ApplyLobbyPanel();
        ApplyStatusTexts();
    }

    // ================================================================
    // 背景・タイトル
    // ================================================================

    private void ApplyBackground()
    {
        // 夜の街っぽい紺グラデーション
        var bg = UITheme.FindDeep("BG");
        if (bg != null && bg.TryGetComponent(out Image bgImg))
        {
            bgImg.sprite = UITheme.VerticalGradient(
                new Color(0.13f, 0.16f, 0.28f),   // 上：宵の紺
                new Color(0.04f, 0.05f, 0.10f));  // 下：夜の底
            bgImg.color = Color.white;
        }

        // ロビーパネルがフルスクリーン背景を持つ場合も同じトーンに
        var lobbyPanel = UITheme.FindDeep("LobbyPanel");
        if (lobbyPanel != null && lobbyPanel.TryGetComponent(out Image lobbyBg))
        {
            lobbyBg.sprite = UITheme.VerticalGradient(
                new Color(0.13f, 0.16f, 0.28f),
                new Color(0.04f, 0.05f, 0.10f));
            lobbyBg.color = Color.white;
        }
    }

    private void ApplyTitle()
    {
        var title = UITheme.FindDeep("Title");
        if (title != null && title.TryGetComponent(out TMP_Text titleText))
        {
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = UITheme.TextMain;
            titleText.characterSpacing = 8f;
            UITheme.AddTextOutline(titleText, 0.12f);
        }

        var subtitle = UITheme.FindDeep("Subtitle");
        if (subtitle != null && subtitle.TryGetComponent(out TMP_Text subText))
        {
            subText.color = UITheme.TextSub;
        }
    }

    // ================================================================
    // メインメニューのボタン
    // ================================================================

    private void ApplyMainButtons()
    {
        StyleButtonByName("HostButton", UITheme.Accent,     Color.white, 34f);
        StyleButtonByName("JoinButton", UITheme.Blue,       Color.white, 34f);
        StyleButtonByName("QuitButton", UITheme.DarkButton, UITheme.TextSub, 28f);
    }

    // ================================================================
    // Join入力パネル
    // ================================================================

    private void ApplyJoinPanel()
    {
        // モーダルの暗幕
        var joinPanel = UITheme.FindDeep("JoinPanel");
        if (joinPanel != null && joinPanel.TryGetComponent(out Image dim))
        {
            dim.sprite = null;
            dim.color  = new Color(0f, 0f, 0f, 0.60f);
        }

        // 中央カード
        StyleCardByName("Card");

        var codeLabel = UITheme.FindDeep("CodeLabel");
        if (codeLabel != null && codeLabel.TryGetComponent(out TMP_Text labelText))
        {
            labelText.color = UITheme.TextSub;
            labelText.fontStyle = FontStyles.Bold;
        }

        // コード入力欄：大きく・太く・字間を空けて「コード感」を出す
        var inputGo = UITheme.FindDeep("CodeInputField");
        if (inputGo != null && inputGo.TryGetComponent(out TMP_InputField input))
        {
            if (input.TryGetComponent(out Image inputBg))
            {
                inputBg.sprite = UITheme.RoundedSprite;
                inputBg.type   = Image.Type.Sliced;
                inputBg.color  = new Color(0.03f, 0.04f, 0.08f, 0.95f);
            }

            if (input.textComponent != null)
            {
                input.textComponent.fontStyle = FontStyles.Bold;
                input.textComponent.color = UITheme.Gold;
                input.textComponent.characterSpacing = 10f;
                input.textComponent.alignment = TextAlignmentOptions.Center;
            }

            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.color = new Color(1f, 1f, 1f, 0.22f);
                placeholder.alignment = TextAlignmentOptions.Center;
                placeholder.fontStyle = FontStyles.Italic;
            }
        }

        StyleButtonByName("ConfirmJoinButton", UITheme.Green,      Color.white, 30f);
        StyleButtonByName("CancelButton",      UITheme.DarkButton, UITheme.TextSub, 26f);
    }

    // ================================================================
    // ロビー待機パネル
    // ================================================================

    private void ApplyLobbyPanel()
    {
        // パーティーコードの箱：暗い角丸＋金色の大きなコード
        var codeBox = UITheme.FindDeep("CodeBox");
        if (codeBox != null && codeBox.TryGetComponent(out Image boxImg))
        {
            boxImg.sprite = UITheme.RoundedSprite;
            boxImg.type   = Image.Type.Sliced;
            boxImg.color  = new Color(0.03f, 0.04f, 0.08f, 0.95f);
            UITheme.AddShadow(codeBox);
        }

        var codeText = UITheme.FindDeep("PartyCodeText");
        if (codeText != null && codeText.TryGetComponent(out TMP_Text code))
        {
            code.fontStyle = FontStyles.Bold;
            code.color = UITheme.Gold;
            code.characterSpacing = 14f;
        }

        var playersLabel = UITheme.FindDeep("PlayersLabel");
        if (playersLabel != null && playersLabel.TryGetComponent(out TMP_Text players))
        {
            players.color = UITheme.TextSub;
            players.fontStyle = FontStyles.Bold;
        }

        StyleButtonByName("CopyButton",   UITheme.DarkButton, UITheme.TextMain, 24f);
        StyleButtonByName("StartButton",  UITheme.Green,      Color.white, 34f);
        StyleButtonByName("LeaveButton",  UITheme.Red,        Color.white, 26f);
        StyleButtonByName("InviteButton", UITheme.Blue,       Color.white, 26f);

        var waiting = UITheme.FindDeep("WaitingText");
        if (waiting != null && waiting.TryGetComponent(out TMP_Text waitText))
        {
            waitingText = waitText;
            waitText.color = UITheme.TextSub;
            waitText.fontStyle = FontStyles.Italic;
        }
    }

    // ================================================================
    // ステータス・フィードバック系テキスト
    // ================================================================

    private void ApplyStatusTexts()
    {
        var status = UITheme.FindDeep("StatusText");
        if (status != null && status.TryGetComponent(out TMP_Text statusText))
        {
            statusText.color = UITheme.Gold;
            statusText.fontStyle = FontStyles.Bold;
        }

        var error = UITheme.FindDeep("ErrorText");
        if (error != null && error.TryGetComponent(out TMP_Text errorText))
        {
            errorText.color = UITheme.Red;
            errorText.fontStyle = FontStyles.Bold;
        }

        var copied = UITheme.FindDeep("CopyFeedbackText");
        if (copied != null && copied.TryGetComponent(out TMP_Text copiedText))
        {
            copiedText.color = UITheme.Green;
            copiedText.fontStyle = FontStyles.Bold;
        }
    }

    // ================================================================
    // 共通
    // ================================================================

    private static void StyleButtonByName(string name, Color bg, Color fg, float maxFontSize)
    {
        var go = UITheme.FindDeep(name);
        if (go == null) return;

        var button = go.GetComponent<Button>();
        if (button != null) UITheme.StyleButton(button, bg, fg, maxFontSize);
    }

    private static void StyleCardByName(string name)
    {
        foreach (var go in UITheme.FindAllDeep(name))
        {
            if (go.TryGetComponent(out Image img))
            {
                img.sprite = UITheme.RoundedSprite;
                img.type   = Image.Type.Sliced;
                img.color  = UITheme.Panel;
                UITheme.AddShadow(go);
            }
        }
    }

    private void Update()
    {
        // 「Waiting for host...」をゆっくり明滅させて生存感を出す
        if (waitingText != null && waitingText.gameObject.activeInHierarchy)
        {
            pulseTime += Time.deltaTime;
            var c = waitingText.color;
            c.a = 0.55f + 0.45f * Mathf.PingPong(pulseTime * 0.9f, 1f);
            waitingText.color = c;
        }
    }
}
