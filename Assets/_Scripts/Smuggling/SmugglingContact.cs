using UnityEngine;

/// <summary>紙袋を渡す「渡し人」と、受け取る「売人」の共通コンポーネント。</summary>
public class SmugglingContact : SmugglingInteractable
{
    public enum ContactRole : byte
    {
        Giver,
        Dealer,
    }

    [SerializeField] private ContactRole role;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private GameObject visibleRoot;

    public ContactRole Role => role;
    private bool? lastNightState;
    private GameObject runtimeGuaranteedVisual;

    private void Awake()
    {
        if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        DayManager.OnNightArrived += RefreshAvailability;
        DayManager.OnMorningArrived += RefreshAvailability;
        RefreshAvailability();
    }

    private void OnDisable()
    {
        DayManager.OnNightArrived -= RefreshAvailability;
        DayManager.OnMorningArrived -= RefreshAvailability;
    }

    private void Start()
    {
        RefreshAvailability();
    }

    private void Update()
    {
        // シーン読み込み順やネットワーク参加タイミングによって昼夜イベントを
        // 取り逃しても、現在値から必ず表示状態を復元する。
        bool isNight = DayManager.Instance != null && DayManager.Instance.IsNight;
        if (!lastNightState.HasValue || lastNightState.Value != isNight)
            RefreshAvailability();
    }

    private void RefreshAvailability()
    {
        bool active = DayManager.Instance != null && DayManager.Instance.IsNight;
        ApplyNightState(active);
    }

    /// <summary>DayManagerから現在の時間帯を直接適用する。</summary>
    public void ApplyNightState(bool active)
    {
        lastNightState = active;
        if (interactionCollider != null) interactionCollider.enabled = active;
        if (visibleRoot != null) visibleRoot.SetActive(active);

        SmugglingAppearance appearance = GetComponent<SmugglingAppearance>();
        if (active && (appearance == null || !appearance.HasReplacementModel))
            EnsureRuntimeGuaranteedVisual();
        if (runtimeGuaranteedVisual != null) runtimeGuaranteedVisual.SetActive(active);

        if (active)
        {
            int rendererCount = 0;
            if (visibleRoot != null)
            {
                foreach (Renderer renderer in visibleRoot.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = true;
                    rendererCount++;
                }
            }

            Debug.Log(
                $"[Smuggling] {name} を夜用NPCとして表示しました。" +
                $"role={role}, renderers={rendererCount}, position={transform.position}");
        }
    }

    /// <summary>
    /// Editor生成Prefabの描画状態に依存しない最終フォールバック。
    /// 本番モデルが未設定のときだけ、ルート直下へ明るい仮人型を生成する。
    /// </summary>
    private void EnsureRuntimeGuaranteedVisual()
    {
        if (runtimeGuaranteedVisual != null) return;

        runtimeGuaranteedVisual = new GameObject("RuntimeGuaranteedVisual");
        runtimeGuaranteedVisual.transform.SetParent(transform, false);
        runtimeGuaranteedVisual.layer = gameObject.layer;

        Color bodyColor = role == ContactRole.Giver
            ? new Color(0.1f, 0.85f, 1f)
            : new Color(1f, 0.35f, 0.08f);

        CreateRuntimePart(
            PrimitiveType.Capsule,
            "Body",
            new Vector3(0f, 1f, 0f),
            new Vector3(0.72f, 1f, 0.72f),
            bodyColor);
        CreateRuntimePart(
            PrimitiveType.Sphere,
            "Head",
            new Vector3(0f, 1.9f, 0f),
            Vector3.one * 0.58f,
            new Color(1f, 0.78f, 0.52f));
        CreateRuntimePart(
            PrimitiveType.Cube,
            role == ContactRole.Giver ? "GiverMarker" : "DealerMarker",
            new Vector3(0f, 2.55f, 0f),
            new Vector3(0.55f, 0.12f, 0.55f),
            bodyColor);

        GameObject lightObject = new GameObject("VisibilityLight");
        lightObject.transform.SetParent(runtimeGuaranteedVisual.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 2f, 0.4f);
        Light visibilityLight = lightObject.AddComponent<Light>();
        visibilityLight.type = LightType.Point;
        visibilityLight.range = 5f;
        visibilityLight.intensity = 4f;
        visibilityLight.color = Color.Lerp(bodyColor, Color.white, 0.55f);
        visibilityLight.shadows = LightShadows.None;
    }

    private void CreateRuntimePart(
        PrimitiveType primitiveType,
        string partName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.layer = gameObject.layer;
        part.transform.SetParent(runtimeGuaranteedVisual.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null) Destroy(partCollider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            // CreatePrimitiveが持つ、現在のRender Pipelineで確実に描画可能な
            // 標準材質を複製して色だけ変更する。
            Material material = renderer.material;
            material.name = "SmugglingGuaranteedVisual_Runtime";
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }
    }

    /// <summary>シーン内の渡し人・売人すべてへ昼夜状態を適用する。</summary>
    public static int ApplyNightStateToAll(bool active)
    {
        SmugglingContact[] contacts = FindObjectsByType<SmugglingContact>(FindObjectsInactive.Include);
        foreach (SmugglingContact contact in contacts)
            contact.ApplyNightState(active);
        return contacts.Length;
    }

    public override bool CanInteract(SmugglingPlayer player)
    {
        if (player == null || DayManager.Instance == null || !DayManager.Instance.IsNight) return false;
        return role == ContactRole.Giver ? player.CanTakeBag : player.CanDeliverBag;
    }

    public override string GetInteractionLabel(SmugglingPlayer player)
    {
        return role == ContactRole.Giver ? "紙袋を受け取る" : "紙袋を渡す";
    }

    public override void Interact(SmugglingPlayer player)
    {
        if (player == null) return;
        player.RequestContactInteraction(role);
    }
}
