using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// TABキー長押しで表示する「みんなの稼ぎ」ランキング。
/// UIはランタイム生成（UITheme）で、シーン側の旧パネルは使わず非表示にする。
/// 稼ぎ降順に並べ、1〜3位はメダル色のランクバッジ、自分の行はハイライト。
/// </summary>
public class PlayerEarningListUI : MonoBehaviour
{
    [Header("旧UI（使用しない・自動で非表示にする）")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI earningListText;

    // ランタイム生成UI
    private CanvasGroup group;
    private RectTransform cardRt;
    private Transform rowsParent;

    private bool visible;
    private float refreshTimer;
    private const float RefreshInterval = 0.25f;

    private const float CardWidth    = 580f;
    private const float HeaderHeight = 84f;
    private const float RowHeight    = 62f;
    private const float RowSpacing   = 10f;

    // ランクバッジの色（1位:金 / 2位:銀 / 3位:銅 / それ以下:グレー）
    private static readonly Color[] RankColors =
    {
        new Color(1.00f, 0.80f, 0.22f),
        new Color(0.78f, 0.82f, 0.88f),
        new Color(0.80f, 0.52f, 0.28f),
    };
    private static readonly Color RankDefault = UITheme.Grape;

    private void Start()
    {
        // 旧UIは完全に置き換え
        if (panel != null) panel.SetActive(false);

        BuildUI();
    }

    private void BuildUI()
    {
        var canvas = UITheme.CreateCanvas(transform, "EarningListCanvas", 8000);

        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // 中央のパネル（白いカードではなく濃色＋太い白フチ）
        var card = UITheme.Surface(canvas.transform, "EarningCard", 30f, 5f);
        cardRt = card.rectTransform;
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot     = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(CardWidth, 200); // 高さは行数に応じて更新

        // ヘッダー（¥バッジ＋タイトル）
        Image badge = UITheme.Chip(card.transform, "Badge", UITheme.Sun, 25f);
        var badgeRt = badge.rectTransform;
        badgeRt.anchorMin = new Vector2(0f, 1f);
        badgeRt.anchorMax = new Vector2(0f, 1f);
        badgeRt.pivot     = new Vector2(0f, 1f);
        badgeRt.anchoredPosition = new Vector2(24, -20);
        badgeRt.sizeDelta = new Vector2(50, 50);

        var badgeGlyph = UITheme.Label(badge.transform, "Glyph", "¥",
            30f, new Color(0.16f, 0.09f, 0.02f), TextAlignmentOptions.Center, bold: true);
        StretchFull(badgeGlyph.rectTransform);

        var title = UITheme.Label(card.transform, "Title", "みんなの稼ぎ",
            30f, Color.white, TextAlignmentOptions.Left, bold: true);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot     = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(88, -24);
        titleRt.sizeDelta = new Vector2(-120, 40);
        title.textWrappingMode = TextWrappingModes.NoWrap;

        // 行コンテナ
        var rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(card.transform, false);
        var rowsRt = rows.GetComponent<RectTransform>();
        rowsRt.anchorMin = new Vector2(0f, 1f);
        rowsRt.anchorMax = new Vector2(1f, 1f);
        rowsRt.pivot     = new Vector2(0.5f, 1f);
        rowsRt.anchoredPosition = new Vector2(0, -HeaderHeight);
        rowsRt.sizeDelta = new Vector2(-44, 0);

        var vlg = rows.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        rowsParent = rows.transform;
    }

    private void Update()
    {
        if (Keyboard.current == null || group == null) return;

        bool tabPressed = Keyboard.current.tabKey.isPressed;

        // 開いた瞬間＋開いている間は定期更新
        if (tabPressed)
        {
            refreshTimer -= Time.deltaTime;
            if (!visible || refreshTimer <= 0f)
            {
                RefreshRows();
                refreshTimer = RefreshInterval;
            }
        }
        visible = tabPressed;

        // ふわっと開閉（フェード＋スケール）
        float targetAlpha = visible ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime * 8f);

        if (cardRt != null)
        {
            float targetScale = visible ? 1f : 0.94f;
            cardRt.localScale = Vector3.Lerp(
                cardRt.localScale, Vector3.one * targetScale, Time.deltaTime * 14f);
        }
    }

    // ================================================================
    // 行の更新
    // ================================================================

    private void RefreshRows()
    {
        if (rowsParent == null) return;

        foreach (Transform child in rowsParent)
            Destroy(child.gameObject);

        // 稼ぎ降順で並べる
        var players = new List<PlayerEarning>(
            FindObjectsByType<PlayerEarning>());
        players.Sort((a, b) => b.GetEarning().CompareTo(a.GetEarning()));

        for (int i = 0; i < players.Count; i++)
        {
            CreateRow(i + 1, players[i]);
        }

        // パネルの高さを行数に合わせる
        if (cardRt != null)
        {
            float height = HeaderHeight + players.Count * (RowHeight + RowSpacing) + 18f;
            cardRt.sizeDelta = new Vector2(CardWidth, height);
        }
    }

    private void CreateRow(int rank, PlayerEarning player)
    {
        bool isSelf = player.IsOwner;

        string playerName = "Player";
        var nameDisplay = player.GetComponent<PlayerNameDisplay>();
        if (nameDisplay != null) playerName = nameDisplay.GetPlayerName();

        // --- 行（角丸の淡いカード。自分の行はアクセントで強調） ---
        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(rowsParent, false);

        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = rowLe.preferredHeight = RowHeight;

        var rowBg = row.AddComponent<Image>();
        rowBg.sprite = UITheme.PillSprite;
        rowBg.color  = isSelf
            ? Color.Lerp(UITheme.MenuInkSoft, UITheme.Sun, 0.26f)
            : UITheme.MenuInkSoft;
        UITheme.SetCornerRadius(rowBg, RowHeight * 0.5f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.spacing                = 14f;
        hlg.padding                = new RectOffset(8, 20, 6, 6);
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // --- ランクバッジ（丸） ---
        var rankGo = new GameObject("Rank", typeof(RectTransform), typeof(Image));
        rankGo.transform.SetParent(row.transform, false);
        var rankLe = rankGo.AddComponent<LayoutElement>();
        rankLe.minWidth = rankLe.preferredWidth = 46f;
        rankLe.minHeight = rankLe.preferredHeight = 46f;

        var rankImg = rankGo.GetComponent<Image>();
        rankImg.sprite = UITheme.PillSprite;
        rankImg.color  = rank <= RankColors.Length ? RankColors[rank - 1] : RankDefault;
        rankImg.raycastTarget = false;
        UITheme.SetCornerRadius(rankImg, 22f);

        // メダル色が明るい1〜3位は濃い文字、4位以降は白文字にする
        var rankLabel = UITheme.Label(rankGo.transform, "Num", rank.ToString(),
            24f,
            rank <= RankColors.Length ? new Color(0.14f, 0.08f, 0.02f) : Color.white,
            TextAlignmentOptions.Center, bold: true);
        StretchFull(rankLabel.rectTransform);

        // --- 名前 ---
        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(row.transform, false);
        var nameLe = nameGo.AddComponent<LayoutElement>();
        nameLe.flexibleWidth = 1f;

        var nameLabel = nameGo.AddComponent<TextMeshProUGUI>();
        nameLabel.text = isSelf ? $"{playerName}（あなた）" : playerName;
        nameLabel.fontSize  = 26f;
        nameLabel.fontStyle = FontStyles.Bold;
        nameLabel.color     = isSelf ? UITheme.Sun : Color.white;
        nameLabel.alignment = TextAlignmentOptions.Left;
        nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        nameLabel.overflowMode = TextOverflowModes.Ellipsis;

        // --- 金額 ---
        var amountGo = new GameObject("Amount", typeof(RectTransform));
        amountGo.transform.SetParent(row.transform, false);
        var amountLe = amountGo.AddComponent<LayoutElement>();
        amountLe.minWidth = amountLe.preferredWidth = 160f;

        int earning = player.GetEarning();
        var amountLabel = amountGo.AddComponent<TextMeshProUGUI>();
        amountLabel.text = "¥" + earning.ToString("N0");
        amountLabel.fontSize  = 27f;
        amountLabel.fontStyle = FontStyles.Bold;
        amountLabel.color     = earning < 0 ? UITheme.Red : UITheme.Lime;
        amountLabel.alignment = TextAlignmentOptions.Right;
        amountLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
