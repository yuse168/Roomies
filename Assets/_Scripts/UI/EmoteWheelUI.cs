using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmoteWheelUI : MonoBehaviour
{
    private static EmoteWheelUI instance;
    public static EmoteWheelUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<EmoteWheelUI>();
                if (instance == null)
                {
                    GameObject go = new GameObject("EmoteWheelUI_Manager");
                    instance = go.AddComponent<EmoteWheelUI>();
                }
            }
            return instance;
        }
    }

    [Header("ホイール設定")]
    [SerializeField] private float wheelRadius = 130f;
    [SerializeField] private float deadzoneRadius = 20f;

    private GameObject rootCanvasObj;
    private GameObject wheelPanelObj;
    private RectTransform wheelPanelRect;
    private TextMeshProUGUI centerText;

    private struct SlotData
    {
        public int index;
        public RectTransform rectTransform;
        public Image bgImage;
        public TextMeshProUGUI text;
        public Vector2 direction;
    }

    private List<SlotData> slots = new List<SlotData>();
    private Vector2 accumulatedMouseDelta = Vector2.zero;
    private int currentSelectedSlot = 0;
    private bool isOpen = false;

    public bool IsOpen => isOpen;
    public int CurrentSelectedSlot => currentSelectedSlot;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        Hide();
    }

    private void BuildUI()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Transform targetParent = parentCanvas != null ? parentCanvas.transform : null;

        if (targetParent == null)
        {
            rootCanvasObj = new GameObject("EmoteWheelCanvas");
            rootCanvasObj.transform.SetParent(transform, false);
            Canvas canvas = rootCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = rootCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            rootCanvasObj.AddComponent<GraphicRaycaster>();
            targetParent = rootCanvasObj.transform;
        }

        // ホイール背景パネル
        wheelPanelObj = new GameObject("WheelPanel");
        wheelPanelRect = wheelPanelObj.AddComponent<RectTransform>();
        wheelPanelRect.SetParent(targetParent, false);
        wheelPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        wheelPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        wheelPanelRect.pivot = new Vector2(0.5f, 0.5f);
        wheelPanelRect.sizeDelta = new Vector2(420, 420);
        wheelPanelRect.anchoredPosition = Vector2.zero;

        Image bgRim = wheelPanelObj.AddComponent<Image>();
        Texture2D circleTex = CreateCircleTexture(256, new Color(0.05f, 0.05f, 0.08f, 0.65f));
        bgRim.sprite = Sprite.Create(circleTex, new Rect(0, 0, circleTex.width, circleTex.height), new Vector2(0.5f, 0.5f));

        // 中央のラベル
        GameObject centerObj = new GameObject("CenterText");
        RectTransform centerRect = centerObj.AddComponent<RectTransform>();
        centerRect.SetParent(wheelPanelRect, false);
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.sizeDelta = new Vector2(220, 60);
        centerRect.anchoredPosition = Vector2.zero;

        centerText = centerObj.AddComponent<TextMeshProUGUI>();
        centerText.alignment = TextAlignmentOptions.Center;
        centerText.fontSize = 24;
        centerText.fontStyle = FontStyles.Bold;
        centerText.color = Color.white;
        centerText.text = "EMOTE";

        // 6分割のスロット生成
        float[] angles = new float[] { 90f, 30f, 330f, 270f, 210f, 150f };
        Texture2D slotTex = CreateCircleTexture(128, Color.white);
        Sprite slotSprite = Sprite.Create(slotTex, new Rect(0, 0, slotTex.width, slotTex.height), new Vector2(0.5f, 0.5f));

        for (int i = 0; i < 6; i++)
        {
            int slotNumber = i + 1;
            float angleRad = angles[i] * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            GameObject slotObj = new GameObject($"Slot_{slotNumber}");
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.SetParent(wheelPanelRect, false);
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(72, 72);
            slotRect.anchoredPosition = dir * wheelRadius;

            Image img = slotObj.AddComponent<Image>();
            img.sprite = slotSprite;
            img.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);

            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(slotRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 30;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            tmp.text = slotNumber.ToString();

            SlotData data;
            data.index = slotNumber;
            data.rectTransform = slotRect;
            data.bgImage = img;
            data.text = tmp;
            data.direction = dir;
            slots.Add(data);
        }
    }

    public void Show()
    {
        isOpen = true;
        accumulatedMouseDelta = Vector2.zero;
        currentSelectedSlot = 0;
        if (wheelPanelObj != null) wheelPanelObj.SetActive(true);
        UpdateHighlight();
    }

    public void Hide()
    {
        isOpen = false;
        if (wheelPanelObj != null) wheelPanelObj.SetActive(false);
    }

    public void AddMouseInput(Vector2 delta)
    {
        if (!isOpen) return;

        accumulatedMouseDelta += delta;
        float dist = accumulatedMouseDelta.magnitude;

        if (dist < deadzoneRadius)
        {
            currentSelectedSlot = 0;
        }
        else
        {
            float angle = Mathf.Atan2(accumulatedMouseDelta.y, accumulatedMouseDelta.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if (angle >= 60f && angle < 120f) currentSelectedSlot = 1;
            else if (angle >= 0f && angle < 60f) currentSelectedSlot = 2;
            else if (angle >= 300f || angle < 0f) currentSelectedSlot = 3;
            else if (angle >= 240f && angle < 300f) currentSelectedSlot = 4;
            else if (angle >= 180f && angle < 240f) currentSelectedSlot = 5;
            else if (angle >= 120f && angle < 180f) currentSelectedSlot = 6;
            else currentSelectedSlot = 3;
        }

        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        Color normalBg = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        Color activeBg = new Color(1f, 0.64f, 0.12f, 0.98f);

        Color normalText = new Color(0.9f, 0.9f, 0.9f, 1f);
        Color activeText = Color.black;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            bool isSelected = (slot.index == currentSelectedSlot);

            slot.bgImage.color = isSelected ? activeBg : normalBg;
            slot.text.color = isSelected ? activeText : normalText;
            slot.rectTransform.localScale = isSelected ? Vector3.one * 1.25f : Vector3.one;
        }

        if (centerText != null)
        {
            if (currentSelectedSlot > 0)
            {
                centerText.text = $"<color=#FFA31F>- {currentSelectedSlot} -</color>";
            }
            else
            {
                centerText.text = "<color=#AAAAAA>EMOTE</color>";
            }
        }
    }

    private Texture2D CreateCircleTexture(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return tex;
    }
}
