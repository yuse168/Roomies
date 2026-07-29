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

        // 背景
        var bgGo = new GameObject("BG", typeof(RectTransform));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bgGo.AddComponent<Image>().color = new Color(
            UITheme.WarmBottom.r, UITheme.WarmBottom.g, UITheme.WarmBottom.b, 0.97f);

        // タイトル
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(canvasGo.transform, false);
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0, -80);
        trt.sizeDelta = new Vector2(1200, 120);
        title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "今日の収支";
        title.fontSize = 80;
        title.fontStyle = FontStyles.Bold;
        title.color = UITheme.Accent;
        title.alignment = TextAlignmentOptions.Center;

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
        float rowH = 86f;
        float startY = (lines.Length - 1) * rowH * 0.5f;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int sep = line.LastIndexOf('|');
            string name = sep >= 0 ? line.Substring(0, sep) : line;
            string amount = sep >= 0 ? line.Substring(sep + 1) : "0";
            int rank = i + 1;

            var rowGo = new GameObject($"Row{i}", typeof(RectTransform));
            rowGo.transform.SetParent(listRoot, false);
            var rrt = rowGo.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0, startY - i * rowH);
            rrt.sizeDelta = new Vector2(900, rowH - 10);

            // 背景（1位は金色）
            var bg = rowGo.AddComponent<Image>();
            bg.color = RankColor(rank);

            // テキスト
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(rowGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(28, 0); txtRt.offsetMax = new Vector2(-28, 0);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            // 例:  1位  Alice                ¥1,200
            int amt; int.TryParse(amount, out amt);
            txt.text = $"<b>{rank}位</b>   {name}<pos=70%>¥{amt:N0}";
            txt.fontSize = 44;
            txt.color = rank == 1 ? new Color(0.15f, 0.1f, 0.02f) : Color.white;
            txt.alignment = TextAlignmentOptions.Left;
            txt.textWrappingMode = TextWrappingModes.NoWrap;

            rowGo.SetActive(false);
            rows.Add(rowGo);
        }
    }

    private static Color RankColor(int rank)
    {
        switch (rank)
        {
            case 1:  return new Color(1.0f, 0.80f, 0.20f, 0.98f); // 金
            case 2:  return new Color(0.75f, 0.78f, 0.85f, 0.95f); // 銀
            case 3:  return new Color(0.80f, 0.52f, 0.30f, 0.95f); // 銅
            default: return UITheme.PanelSoft;
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
