using System.Collections;
using UnityEngine;

/// <summary>仮の警察ドアップ→暗転演出。画像素材なしで動作する。</summary>
public class SmugglingArrestOverlay : MonoBehaviour
{
    private float alpha;
    private bool showFace;
    private GUIStyle titleStyle;

    public void Play()
    {
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
        if (alpha <= 0f) return;

        Color old = GUI.color;
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

        GUI.color = old;
    }
}
