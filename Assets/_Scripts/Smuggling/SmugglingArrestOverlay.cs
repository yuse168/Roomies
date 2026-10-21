using System.Collections;
using UnityEngine;

/// <summary>仮の警察ドアップ→暗転演出。画像素材なしで動作する。</summary>
public class SmugglingArrestOverlay : MonoBehaviour
{
    private float alpha;
    private bool showFace;
    private bool isChasing;
    private GUIStyle titleStyle;
    private GUIStyle chaseTitleStyle;
    private GUIStyle chaseHintStyle;
    private int styledForHeight;

    public void SetChasing(bool chasing)
    {
        isChasing = chasing;
    }

    public void Play()
    {
        isChasing = false;
        StopAllCoroutines();
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        showFace = true;
        alpha = 1f;
        yield return new WaitForSecondsRealtime(1.15f);
        showFace = false;
        yield return new WaitForSecondsRealtime(0.65f);
        alpha = 0f;
    }

    private void OnGUI()
    {
        if (alpha <= 0f && !isChasing) return;

        Color old = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = -1000;

        if (isChasing) DrawChaseAlert();

        if (alpha > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            if (showFace)
            {
                float size = Mathf.Min(Screen.width, Screen.height) * 0.72f;
                Rect face = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);
                GUI.color = new Color(0.12f, 0.28f, 0.75f, 1f);
                GUI.DrawTexture(face, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (titleStyle == null)
                {
                    titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = Mathf.Max(28, Screen.height / 14),
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white },
                    };
                }

                GUI.Label(face, "警察だ！\n止まれ！", titleStyle);
            }
        }

        GUI.depth = oldDepth;
        GUI.color = old;
    }

    private void DrawChaseAlert()
    {
        EnsureChaseStyles();

        float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 8f) * 0.5f;
        float edge = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.018f, 12f, 26f);

        GUI.color = new Color(1f, 0.04f, 0.08f, 0.16f + pulse * 0.20f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, edge), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, Screen.height - edge, Screen.width, edge), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, 0f, edge, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Screen.width - edge, 0f, edge, Screen.height), Texture2D.whiteTexture);

        float width = Mathf.Min(Screen.width - 40f, 560f);
        float height = Mathf.Clamp(Screen.height * 0.105f, 76f, 112f);
        float grow = pulse * 4f;
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f - grow,
            Mathf.Max(24f, Screen.height * 0.055f) - grow,
            width + grow * 2f,
            height + grow * 2f);

        GUI.color = new Color(0.18f, 0.015f, 0.035f, 0.94f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.18f, 0.24f, 0.90f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, Mathf.Max(5f, edge * 0.35f)), Texture2D.whiteTexture);

        GUI.color = Color.white;
        Rect titleRect = new Rect(panel.x + 14f, panel.y + 7f, panel.width - 28f, panel.height * 0.58f);
        Rect hintRect = new Rect(panel.x + 14f, panel.y + panel.height * 0.54f, panel.width - 28f, panel.height * 0.34f);
        GUI.Label(titleRect, "警察に追われている！", chaseTitleStyle);
        GUI.Label(hintRect, "走って距離を離せ", chaseHintStyle);
    }

    private void EnsureChaseStyles()
    {
        if (chaseTitleStyle == null)
        {
            chaseTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            chaseHintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.82f, 0.32f) },
            };
        }

        if (styledForHeight == Screen.height) return;
        styledForHeight = Screen.height;
        chaseTitleStyle.fontSize = Mathf.Clamp(Screen.height / 25, 28, 48);
        chaseHintStyle.fontSize = Mathf.Clamp(Screen.height / 46, 18, 28);
    }
}
