using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 家に自動設置されるキャラクターカラー変更用クローゼット。
/// 外見Prefabへ差し替えても、このコンポーネントとColliderを残せば機能する。
/// </summary>
public class CharacterCloset : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private CanvasGroup overlay;
    private PlayerNameDisplay targetPlayer;
    private TMP_Text selectedLabel;
    private readonly Button[] swatches = new Button[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        IsOpen = false;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsOpen = false;
        if (scene.name != "GameRoom" || FindAnyObjectByType<CharacterCloset>() != null) return;

        var closet = new GameObject("CharacterCloset");
        closet.transform.SetPositionAndRotation(
            new Vector3(3.25f, 1.05f, -7.35f),
            Quaternion.Euler(0f, -90f, 0f));
        closet.AddComponent<CharacterCloset>().BuildClosetVisual();
    }

    public bool CanInteract(PlayerNameDisplay player)
    {
        return player != null && player.IsOwner && !IsOpen;
    }

    public void Open(PlayerNameDisplay player)
    {
        if (!CanInteract(player)) return;
        targetPlayer = player;
        EnsureUI();
        IsOpen = true;
        overlay.alpha = 1f;
        overlay.interactable = true;
        overlay.blocksRaycasts = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshSelection(player.CurrentColorIndex);
    }

    public void Close()
    {
        if (overlay != null)
        {
            overlay.alpha = 0f;
            overlay.interactable = false;
            overlay.blocksRaycasts = false;
        }

        targetPlayer = null;
        IsOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (IsOpen && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Close();
    }

    private void BuildClosetVisual()
    {
        CreatePart("Body", new Vector3(1.8f, 2.1f, 0.72f), new Vector3(0f, 0f, 0f),
            new Color(0.24f, 0.15f, 0.10f), true);
        CreatePart("LeftDoor", new Vector3(0.84f, 1.84f, 0.08f), new Vector3(-0.45f, 0f, -0.40f),
            new Color(0.42f, 0.27f, 0.16f), false);
        CreatePart("RightDoor", new Vector3(0.84f, 1.84f, 0.08f), new Vector3(0.45f, 0f, -0.40f),
            new Color(0.42f, 0.27f, 0.16f), false);
        CreatePart("LeftHandle", new Vector3(0.06f, 0.24f, 0.07f), new Vector3(-0.10f, 0f, -0.47f),
            UITheme.Accent, false);
        CreatePart("RightHandle", new Vector3(0.06f, 0.24f, 0.07f), new Vector3(0.10f, 0f, -0.47f),
            UITheme.Accent, false);
    }

    private void CreatePart(string partName, Vector3 scale, Vector3 localPosition, Color color, bool collider)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        var rendererComponent = part.GetComponent<Renderer>();
        rendererComponent.material.color = color;

        Collider partCollider = part.GetComponent<Collider>();
        if (!collider && partCollider != null) Destroy(partCollider);
    }

    private void EnsureUI()
    {
        if (overlay != null) return;

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        Canvas canvas = UITheme.CreateCanvas(null, "CharacterClosetCanvas", 500);
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(canvas.transform, false);
        Stretch(shade.GetComponent<RectTransform>());
        shade.GetComponent<Image>().color = new Color(0.01f, 0.012f, 0.018f, 0.86f);
        overlay = shade.AddComponent<CanvasGroup>();

        Image card = UITheme.Card(shade.transform, "ClosetCard");
        RectTransform cardRect = card.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(760f, 590f);
        cardRect.anchoredPosition = Vector2.zero;

        TMP_Text title = UITheme.Label(card.transform, "Title", "キャラクターカラー",
            36f, UITheme.TextMain, TextAlignmentOptions.Center, true);
        SetBox(title.rectTransform, 40f, 30f, 680f, 56f);

        TMP_Text hint = UITheme.Label(card.transform, "Hint", "自分の色を選んでください",
            20f, UITheme.TextSub, TextAlignmentOptions.Center, false);
        SetBox(hint.rectTransform, 40f, 88f, 680f, 36f);

        for (int i = 0; i < swatches.Length; i++)
        {
            int index = i;
            var go = new GameObject($"Color_{i + 1:00}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(card.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            float x = 58f + (i % 4) * 166f;
            float y = 146f + (i / 4) * 82f;
            SetBox(rt, x, y, 146f, 64f);
            Image image = go.GetComponent<Image>();
            image.sprite = UITheme.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = PlayerNameDisplay.CharacterColors[i];
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SelectColor(index));
            swatches[i] = button;
        }

        selectedLabel = UITheme.Label(card.transform, "Selected", "",
            20f, UITheme.Accent, TextAlignmentOptions.Left, true);
        SetBox(selectedLabel.rectTransform, 58f, 492f, 380f, 42f);

        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(card.transform, false);
        SetBox(closeGo.GetComponent<RectTransform>(), 504f, 482f, 198f, 58f);
        Button closeButton = closeGo.GetComponent<Button>();
        TMP_Text closeLabel = UITheme.Label(
            closeGo.transform, "Label", "決定して戻る", 24f,
            UITheme.TextMain, TextAlignmentOptions.Center, true);
        Stretch(closeLabel.rectTransform);
        UITheme.StyleButton(closeButton, UITheme.Purple, UITheme.TextMain, 24f);
        closeButton.onClick.AddListener(Close);
    }

    private void SelectColor(int index)
    {
        targetPlayer?.SetLocalCharacterColor(index);
        RefreshSelection(index);
    }

    private void RefreshSelection(int selected)
    {
        for (int i = 0; i < swatches.Length; i++)
        {
            var outline = swatches[i].GetComponent<Outline>();
            if (i == selected)
            {
                if (outline == null) outline = swatches[i].gameObject.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(4f, -4f);
                outline.enabled = true;
            }
            else if (outline != null)
            {
                outline.enabled = false;
            }
        }

        if (selectedLabel != null)
            selectedLabel.text = $"選択中  {selected + 1:00} / 16";
    }

    private static void SetBox(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
