using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 夜終わりに「今日の収支」を全画面で上位からドン！ドン！と表示する演出。
/// シーン配置不要：DayManagerがランタイムで生成する。
/// データは "名前|金額" を改行で並べた文字列（金額の降順で渡す）。
/// </summary>
public class DayResultUI : MonoBehaviour
{
    private CanvasGroup     group;
    private RectTransform   listRoot;
    private TextMeshProUGUI title;
    private readonly List<GameObject> rows = new List<GameObject>();

    // タイミング
    private const float FadeIn      = 0.4f;
    private const float RevealGap   = 0.55f; // 1行ごとの「ドン！」間隔
    private const float HoldAfter   = 1.6f;
    private const float FadeOut     = 0.5f;

    /// <summary>表示にかかるおおよその合計秒数（DayManagerが待ち時間に使う）。</summary>
    public static float EstimatedDuration(int playerCount)
        => FadeIn + Mathf.Max(1, playerCount) * RevealGap + HoldAfter + FadeOut + 0.3f;

    private void Awake()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("DayResultCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // 朝演出(10000)より下、通常UIより上
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // 背景（部屋がうっすら残る遮光。他の全画面演出と同じ型）
        UITheme.StageBackdrop(canvasGo.transform, UITheme.Lime, 0.9f);

        // タイトル
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(canvasGo.transform, false);
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0, -84);
        trt.sizeDelta = new Vector2(1200, 110);
        title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "今日の収支";
        title.fontSize = 76;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        title.characterSpacing = -1f;
        title.enableAutoSizing = true;
        title.fontSizeMin = 44f;
        title.fontSizeMax = 76f;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        // 行を並べる親
        var listGo = new GameObject("List", typeof(RectTransform));
        listGo.transform.SetParent(canvasGo.transform, false);
        listRoot = listGo.GetComponent<RectTransform>();
        listRoot.anchorMin = listRoot.anchorMax = new Vector2(0.5f, 0.5f);
        listRoot.pivot = new Vector2(0.5f, 0.5f);
        listRoot.anchoredPosition = new Vector2(0, 20);
        listRoot.sizeDelta = new Vector2(1000, 700);

        canvasGo.SetActive(true);
    }

    public void Play(string data)
    {
        StopAllCoroutines();
        StartCoroutine(PlayRoutine(data));
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (group != null) group.alpha = 0f;
        ClearRows();
    }

    private IEnumerator PlayRoutine(string data)
    {
        BuildRows(data);

        // フェードイン
        yield return Fade(0f, 1f, FadeIn);

        // 上位から1つずつドン！
        for (int i = 0; i < rows.Count; i++)
        {
            yield return RevealRow(rows[i].GetComponent<RectTransform>());
            yield return new WaitForSeconds(RevealGap);
        }

        yield return new WaitForSeconds(HoldAfter);
        yield return Fade(1f, 0f, FadeOut);
        ClearRows();
    }

    private void BuildRows(string data)
    {
        ClearRows();
        if (string.IsNullOrWhiteSpace(data)) return;

        string[] lines = data.Replace("\r", "").Split('\n');
        const float rowH = 96f;
        const float rowWidth = 900f;
        const float plateHeight = 86f;
        float startY = (lines.Length - 1) * rowH * 0.5f;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int sep = line.LastIndexOf('|');
            string name = sep >= 0 ? line.Substring(0, sep) : line;
            string amount = sep >= 0 ? line.Substring(sep + 1) : "0";
            int rank = i + 1;
            int.TryParse(amount, out int amt);

            Color medal = RankColor(rank);

            // --- 名札（枠＝メダル色、中は濃色。ロビーの名札と同じ語彙） ---
            var rowGo = new GameObject($"Row{i}", typeof(RectTransform));
            rowGo.transform.SetParent(listRoot, false);
            var rrt = rowGo.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0, startY - i * rowH);
            rrt.sizeDelta = new Vector2(rowWidth, plateHeight);

            var plate = rowGo.AddComponent<Image>();
            plate.sprite = UITheme.PillSprite;
            plate.color = medal;
            UITheme.SetCornerRadius(plate, plateHeight * 0.5f);
            UITheme.AddShadow(rowGo, 0.45f, 8f);

            var fill = UITheme.Chip(rowGo.transform, "Fill",
                UITheme.MenuInk, plateHeight * 0.5f - 5f);
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(5f, 5f);
            fillRt.offsetMax = new Vector2(-5f, -5f);

            // --- 順位メダル（丸） ---
            var medalImage = UITheme.Chip(rowGo.transform, "Medal", medal, 33f);
            var medalRt = medalImage.rectTransform;
            medalRt.anchorMin = medalRt.anchorMax = new Vector2(0f, 0.5f);
            medalRt.pivot = new Vector2(0f, 0.5f);
            medalRt.anchoredPosition = new Vector2(12f, 0f);
            medalRt.sizeDelta = new Vector2(66f, 66f);

            // メダル色が明るい1〜3位は濃い文字、4位以降の紫は白文字にする
            var medalLabel = UITheme.Label(medalImage.transform, "Num", rank.ToString(),
                36f, rank <= 3 ? new Color(0.14f, 0.08f, 0.02f) : Color.white,
                TextAlignmentOptions.Center, bold: true);
            StretchFull(medalLabel.rectTransform);

            // --- 名前（左） ---
            var nameLabel = UITheme.Label(rowGo.transform, "Name", name,
                40f, Color.white, TextAlignmentOptions.Left, bold: true);
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            var nameRt = nameLabel.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(0f, 1f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(96f, 0f);
            nameRt.sizeDelta = new Vector2(480f, 0f);

            // --- 金額（右） ---
            var amountLabel = UITheme.Label(rowGo.transform, "Amount", $"¥{amt:N0}",
                44f, amt < 0 ? UITheme.Red : UITheme.Lime,
                TextAlignmentOptions.Right, bold: true);
            amountLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var amountRt = amountLabel.rectTransform;
            amountRt.anchorMin = new Vector2(1f, 0f);
            amountRt.anchorMax = new Vector2(1f, 1f);
            amountRt.pivot = new Vector2(1f, 0.5f);
            amountRt.anchoredPosition = new Vector2(-32f, 0f);
            amountRt.sizeDelta = new Vector2(300f, 0f);

            rowGo.SetActive(false);
            rows.Add(rowGo);
        }
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Color RankColor(int rank)
    {
        switch (rank)
        {
            case 1:  return new Color(1.00f, 0.80f, 0.20f); // 金
            case 2:  return new Color(0.80f, 0.84f, 0.92f); // 銀
            case 3:  return new Color(0.84f, 0.56f, 0.32f); // 銅
            default: return UITheme.Grape;
        }
    }

    private IEnumerator RevealRow(RectTransform row)
    {
        row.gameObject.SetActive(true);
        // ドン！＝大きめから等倍へ一気に
        float t = 0f, dur = 0.22f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1.6f, 1f, t / dur);
            row.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        row.localScale = Vector3.one;
    }

    private void ClearRows()
    {
        foreach (var r in rows) if (r != null) Destroy(r);
        rows.Clear();
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        if (group == null) yield break;
        float t = 0f;
        group.alpha = from;
        while (t < dur)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        group.alpha = to;
    }
}
