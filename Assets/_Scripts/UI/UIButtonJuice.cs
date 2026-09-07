using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    private const float Stiffness = 620f;
    private const float Damping   = 26f;

    [SerializeField] private RectTransform cap;
    [SerializeField, Min(0)] private float depth;

    private float scale = 1f;
    private float scaleVelocity;
    private float sink;
    private float sinkVelocity;

    private bool hovered;
    private bool pressed;
    private bool selected;

    private Outline border;
    private Color defaultBorder;

    /// <summary>厚みのあるボタンの天面と厚み量を登録する。</summary>
    public void SetCap(RectTransform capRect, float capDepth)
    {
        cap = capRect;
        depth = capDepth;
    }

    private void Awake()
    {
        border = GetComponent<Outline>();
        if (border != null) defaultBorder = border.effectColor;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        var button=GetComponent<Button>();if(button!=null&&!button.IsInteractable())return;
        hovered = true;RoomiesAudio.Play("ui_hover",.2f);
    }
    public void OnPointerExit(PointerEventData e)  { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e)
    {
        var button=GetComponent<Button>();if(e.button!=PointerEventData.InputButton.Left||button!=null&&!button.IsInteractable())return;
        pressed=true;RoomiesAudio.Play("ui_click",.45f);
    }
    public void OnPointerUp(PointerEventData e)    => pressed = false;

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        if (border != null && border.enabled)
            border.effectColor = new Color(UITheme.Sun.r, UITheme.Sun.g, UITheme.Sun.b, 0.95f);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        pressed = false;
        if (border != null) border.effectColor = defaultBorder;
    }

    private void OnDisable()
    {
        hovered = pressed = selected = false;
        scale = 1f;
        scaleVelocity = 0f;
        sink = 0f;
        sinkVelocity = 0f;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        ApplySink();
        if (border != null) border.effectColor = defaultBorder;
    }

    private void Update()
    {
        bool lifted = hovered || selected;

        float targetScale = pressed ? 0.975f : lifted ? 1.05f : 1f;
        float targetSink  = pressed ? depth : 0f;

        Spring(ref scale, ref scaleVelocity, targetScale);
        Spring(ref sink, ref sinkVelocity, targetSink);

        transform.localScale = new Vector3(scale, scale, 1f);
        ApplySink();
    }

    private static void Spring(ref float value, ref float velocity, float target)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        velocity += (target - value) * Stiffness * dt;
        velocity *= Mathf.Exp(-Damping * dt);
        value += velocity * dt;
    }

    private void ApplySink()
    {
        if (cap == null) return;
        cap.offsetMin = new Vector2(cap.offsetMin.x, depth - sink);
        cap.offsetMax = new Vector2(cap.offsetMax.x, -sink);
    }
}
