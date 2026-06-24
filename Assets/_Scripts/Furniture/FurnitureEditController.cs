using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 夜の家具編集モード（フェーズ1〜2）。
/// ・夜ターンのみ P キーで開閉
/// ・カメラ中央のレイで床を指し、ゴースト（半透明プレビュー）を表示
/// ・左クリックで設置 / 右クリックで撤去 / R で回転 / 数字キーで選択
/// ・家具は <see cref="FurnitureItem"/> のPrefabで見た目を差し替え可能（未設定は仮ブロック）
///
/// ※ 現状は「ローカル設置」のみ（フェーズ2）。マルチでの同期はフェーズ3で対応。
/// </summary>
public class FurnitureEditController : MonoBehaviour
{
    [Header("家具カタログ（空なら仮ブロックを自動生成）")]
    [SerializeField] private List<FurnitureItem> catalog = new List<FurnitureItem>();

    [Header("撤去レイキャスト設定")]
    [Tooltip("撤去対象を探すレイヤー。既定は全レイヤー。")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float maxPlaceDistance = 8f;

    // ゴーストの色味（CreateInstanceのプレビュー用・現状未使用）
    private static readonly Color GhostTint = new Color(0.3f, 0.9f, 1.0f, 1f);

    private Camera cam;
    private bool   editMode;
    private int    selected;
    private int    deliveredCount; // 配達した数（配達先で重ならないようにずらす用）

    // ---- UI ----
    private GameObject uiRoot;     // キャンバス（常時有効）
    private GameObject shopRoot;   // カタログバー（編集モード中だけ表示）
    private TextMeshProUGUI hintText;

    // 効果ステータス（Tabで表示／非表示）
    private GameObject effectPanel;
    private TextMeshProUGUI effectText;
    private bool effectPanelVisible;
    private float effectUpdateTimer;

    /// <summary>Inspectorでカタログを編集したいとき用：デフォルト12種を流し込む。</summary>
    [ContextMenu("デフォルトカタログを生成")]
    private void EditorFillDefaultCatalog()
    {
        BuildDefaultCatalog();
    }
    private readonly List<Image>          slotBgs   = new List<Image>();
    private readonly List<TextMeshProUGUI> slotTexts = new List<TextMeshProUGUI>();
    private Coroutine hintCoroutine;

    private static readonly Key[] DigitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
    };

    // =========================================================
    private void Start()
    {
        if (catalog == null || catalog.Count == 0)
            BuildDefaultCatalog();

        BuildUI();
        SetEditMode(false);
        Debug.Log("[Furniture] FurnitureEditController 起動（夜にPで家具ショップ）");
    }

    private void BuildDefaultCatalog()
    {
        // マルチ同期と整合させるため、共有カタログ（FurnitureCatalog）をそのまま使う。
        // ※ index がそのまま同期キーになるので、並び順は FurnitureCatalog 側で管理する。
        catalog = new List<FurnitureItem>(FurnitureCatalog.Items);
    }

    // =========================================================
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // P で開閉（開けるのは夜のみ）
        if (kb.pKey.wasPressedThisFrame)
        {
            var allDm = FindObjectsByType<DayManager>(FindObjectsSortMode.None);
            string info = "";
            foreach (var d in allDm)
                info += $" [id{d.GetInstanceID()} time={d.DebugTime} day={d.DebugDay} spawned={d.DebugSpawned} isInstance={(d == DayManager.Instance)}]";
            Debug.Log($"[Furniture] P押下: DayManager数={allDm.Length}{info}");
            if (editMode)            SetEditMode(false);
            else if (IsNight())      SetEditMode(true);
            else                     ShowHint("家具ショップは「夜」だけ開けます（Nで夜にできます）", 2.2f);
        }

        // Tab: 効果ステータスの表示切替（いつでも・確認用）
        if (kb.tabKey.wasPressedThisFrame)
        {
            ToggleEffectPanel();
        }
        if (effectPanelVisible)
        {
            effectUpdateTimer += Time.deltaTime;
            if (effectUpdateTimer >= 0.25f) { effectUpdateTimer = 0f; UpdateEffectText(); }
        }

        if (!editMode) return;

        // 夜が終わったら強制終了
        if (!IsNight()) { SetEditMode(false); return; }

        // 家具の選択（数字キー1-9）
        int n = Mathf.Min(catalog.Count, DigitKeys.Length);
        for (int i = 0; i < n; i++)
        {
            if (kb[DigitKeys[i]].wasPressedThisFrame) Select(i);
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        // マウスホイールで選択を送る（数字キーで届かない家具も選べる）
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f && catalog.Count > 0)
        {
            int dir = scroll > 0 ? 1 : -1;
            Select(((selected + dir) % catalog.Count + catalog.Count) % catalog.Count);
        }

        // 左クリック: 購入（即・配達先にスポーン。効果は翌朝から）
        if (mouse.leftButton.wasPressedThisFrame)
        {
            BuyFurniture();
        }

        // 右クリック: 見ている家具を撤去（配達済みのみ。返金なし）
        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (cam == null) cam = ResolveCamera();
            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, placementMask, QueryTriggerInteraction.Ignore))
                {
                    var placed = hit.collider.GetComponentInParent<PlacedFurniture>();
                    if (placed != null) Destroy(placed.gameObject);
                }
            }
        }
    }

    // =========================================================
    // モード切替
    // =========================================================
    private void SetEditMode(bool on)
    {
        editMode = on;
        if (shopRoot != null) shopRoot.SetActive(on); // バーだけ開閉（キャンバスは常時有効）

        if (on)
        {
            cam = ResolveCamera();
            UpdateSlotHighlight();
            if (hintText != null) hintText.text = DefaultHint;
        }
        else
        {
            if (hintText != null) hintText.text = "";
        }
    }

    private DayManager dayMgr;

    private DayManager GetDayManager()
    {
        // spawn済み（＝ネットワーク状態が同期されている本物）を優先して使う
        if (dayMgr != null && dayMgr.DebugSpawned) return dayMgr;

        var all = FindObjectsByType<DayManager>(FindObjectsSortMode.None);
        foreach (var d in all)
        {
            if (d != null && d.DebugSpawned) { dayMgr = d; return dayMgr; }
        }

        // spawn済みが無ければ従来どおり
        if (dayMgr == null)
            dayMgr = DayManager.Instance != null ? DayManager.Instance
                   : (all.Length > 0 ? all[0] : null);
        return dayMgr;
    }

    private bool IsNight()
    {
        var dm = GetDayManager();
        return dm != null && dm.IsNight;
    }

    // =========================================================
    // 効果ステータス表示（Tab）
    // =========================================================
    private void ToggleEffectPanel()
    {
        effectPanelVisible = !effectPanelVisible;
        if (effectPanel != null) effectPanel.SetActive(effectPanelVisible);
        if (effectPanelVisible) UpdateEffectText();
    }

    private void UpdateEffectText()
    {
        if (effectText == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>効果ステータス</b>  <size=15>(Tabで閉じる)</size>");

        var fem = FurnitureEffectManager.InstanceOrNull;
        if (fem == null || fem.Registered.Count == 0)
        {
            sb.AppendLine("");
            sb.AppendLine("家具がまだありません。");
            sb.AppendLine("夜に P → 購入してみてください。");
            effectText.text = sb.ToString();
            return;
        }

        sb.AppendLine($"移動速度: <color=#7CF>x{fem.MoveSpeedMultiplier:0.00}</color>");
        sb.AppendLine($"毎朝の収入: <color=#7F7>¥{fem.PassiveIncomeTotal}</color>");
        sb.AppendLine("―――――――――");

        foreach (var f in fem.Registered)
        {
            if (f == null) continue;
            string name = string.IsNullOrEmpty(f.displayName) ? f.itemId : f.displayName;
            string eff  = EffectLabel(f.effect, f.effectValue);
            string room = f.IsOnRoom ? "<color=#7F7>部屋○</color>" : "<color=#F88>部屋×</color>";
            string act  = f.EffectActive ? "<color=#7F7>有効</color>" : "<color=#FC6>翌朝から</color>";
            sb.AppendLine($"・{name}：{eff}　[{room} {act}]");
        }

        effectText.text = sb.ToString();
    }

    private static string EffectLabel(FurnitureEffect e, float v)
    {
        switch (e)
        {
            case FurnitureEffect.MoveSpeed:     return $"速度+{v}%";
            case FurnitureEffect.PassiveIncome: return $"収入+{v}/朝";
            default:                            return "効果なし";
        }
    }

    /// <summary>描画中のカメラを取得（MainCamera優先、無ければ有効なカメラ）。</summary>
    private Camera ResolveCamera()
    {
        if (Camera.main != null) return Camera.main;
        foreach (var c in Camera.allCameras)
            if (c != null && c.isActiveAndEnabled) return c;
        return null;
    }

    private void Select(int i)
    {
        selected = Mathf.Clamp(i, 0, catalog.Count - 1);
        UpdateSlotHighlight();
    }

    // =========================================================
    // 購入・配達
    // =========================================================
    /// <summary>
    /// 家具を購入する。代金を支払い、すぐに配達先へスポーンする。
    /// 効果は「翌朝」から有効（FurnitureEffectManagerが朝にActiveにする）。
    /// </summary>
    private void BuyFurniture()
    {
        if (selected < 0 || selected >= catalog.Count) return;

        FurnitureItem item = catalog[selected];
        var money = SharedMoneyManager.Instance;

        if (money == null)
        {
            ShowHint("お金システムが見つかりません", 1.6f);
            return;
        }
        if (!money.CanPay(item.cost))
        {
            ShowHint($"お金が足りない（¥{item.cost} / 所持 ¥{money.CurrentMoney}）", 2f);
            return;
        }

        Vector3 point = GetDeliveryPoint();
        var dm = GetDayManager();

        if (dm != null && dm.CanSpawnNetworkFurniture)
        {
            // ---- マルチ同期スポーン（サーバーが代金も引く） ----
            dm.BuyFurnitureServerRpc(selected, point, 0f);
            deliveredCount++;
            ShowHint($"{item.displayName} を購入（¥{item.cost}）→ 配達（全員に同期・効果は翌朝）", 2.2f);
        }
        else
        {
            // ---- フォールバック: ローカル生成（同期なし。プレハブ未設定時） ----
            money.RequestPurchaseServerRpc(item.cost);

            GameObject obj = CreateInstance(item, ghostMode: false);
            PlaceOnGround(obj, point, 0f);

            var m = obj.GetComponent<PlacedFurniture>();
            if (m == null) m = obj.AddComponent<PlacedFurniture>();
            m.itemId      = item.id;
            m.displayName = item.displayName;
            m.effect      = item.effect;
            m.effectValue = item.effectValue;
            m.SetEffectActive(false);

            deliveredCount++;
            ShowHint($"{item.displayName} を購入（¥{item.cost}）→ 配達（ローカルのみ・効果は翌朝）", 2.2f);
        }
    }

    /// <summary>家具の配達先ワールド座標を返す。マーカーが無ければ仮の位置。</summary>
    private Vector3 GetDeliveryPoint()
    {
        Vector3 basePos;

        var marker = FindAnyObjectByType<FurnitureDeliveryPoint>();
        if (marker != null)
        {
            basePos = marker.transform.position;
        }
        else if (cam != null)
        {
            // 仮：カメラ前方2m（配達マーカーを作るまでの暫定）
            basePos = cam.transform.position + cam.transform.forward * 2f;
        }
        else
        {
            basePos = Vector3.zero;
        }

        // 重ならないよう少しずつずらす
        float angle = deliveredCount * 1.1f;
        float radius = 0.4f + 0.25f * deliveredCount;
        radius = Mathf.Min(radius, 1.6f);
        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        return basePos + offset;
    }

    /// <summary>家具インスタンスを生成。prefab優先、無ければ仮ブロック。</summary>
    private GameObject CreateInstance(FurnitureItem item, bool ghostMode)
    {
        GameObject obj;

        if (item.prefab != null)
        {
            obj = Instantiate(item.prefab);
        }
        else
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.localScale = item.placeholderSize;
            var rend = obj.GetComponent<Renderer>();
            SetRendererColor(rend, ghostMode ? GhostTint : item.placeholderColor);

            // 仮ブロックでも重さ/重力をテストできるように（本番はプレハブ側で設定）
            if (!ghostMode && item.placeholderUsePhysics)
            {
                var rb = obj.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(0.01f, item.placeholderMass);
                rb.useGravity = item.placeholderUseGravity;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        if (ghostMode)
        {
            // 当たり判定を無効化（レイがゴースト自身に当たらないように）
            foreach (var c in obj.GetComponentsInChildren<Collider>()) c.enabled = false;

            // Prefab使用時もプレビューと分かるよう軽く色味を付ける
            if (item.prefab != null)
            {
                foreach (var rend in obj.GetComponentsInChildren<Renderer>())
                    SetRendererColor(rend, GetRendererColor(rend) * 0.6f + GhostTint * 0.4f);
            }
        }

        return obj;
    }

    /// <summary>URP(_BaseColor)とビルトイン(_Color)の両方に対応した色設定。</summary>
    private static void SetRendererColor(Renderer r, Color c)
    {
        if (r == null) return;
        var m = r.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        m.color = c;
    }

    private static Color GetRendererColor(Renderer r)
    {
        if (r == null) return Color.white;
        var m = r.material;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color"))     return m.GetColor("_Color");
        return m.color;
    }

    /// <summary>オブジェクトの「底面」が groundPoint に乗るように配置する（ピボット非依存）。</summary>
    private void PlaceOnGround(GameObject obj, Vector3 groundPoint, float rotationY)
    {
        obj.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        obj.transform.position = groundPoint;

        var rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float bottomOffset = obj.transform.position.y - b.min.y;
            obj.transform.position = new Vector3(
                groundPoint.x, groundPoint.y + bottomOffset, groundPoint.z);
        }
    }

    // =========================================================
    // UI（ランタイム生成・シーン配置不要）
    // =========================================================
    private void BuildUI()
    {
        var canvasGo = new GameObject("FurnitureEditCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        uiRoot = canvasGo;

        // 上部の操作ヒント（キャンバス直下＝常時有効。案内が夜以外でも出る）
        hintText = MakeText(canvasGo.transform, "Hint", "",
            26, new Vector2(0, -40), new Vector2(1600, 50),
            new Color(0.97f, 0.98f, 1f), TextAlignmentOptions.Center,
            anchor: new Vector2(0.5f, 1f));

        // 効果ステータスパネル（右下・Tabで表示）
        effectPanel = new GameObject("EffectPanel", typeof(RectTransform));
        effectPanel.transform.SetParent(canvasGo.transform, false);
        var ert = effectPanel.GetComponent<RectTransform>();
        ert.anchorMin = ert.anchorMax = new Vector2(1f, 0f);
        ert.pivot = new Vector2(1f, 0f);
        ert.anchoredPosition = new Vector2(-20f, 20f);
        ert.sizeDelta = new Vector2(460f, 380f);
        var ebg = effectPanel.AddComponent<Image>();
        ebg.color = new Color(0.10f, 0.11f, 0.18f, 0.92f);

        effectText = MakeText(effectPanel.transform, "Text", "",
            20, new Vector2(0, 0), new Vector2(430f, 360f),
            new Color(0.95f, 0.97f, 1f), TextAlignmentOptions.TopLeft,
            anchor: new Vector2(0.5f, 0.5f));
        effectText.textWrappingMode = TextWrappingModes.Normal;
        effectPanel.SetActive(false);

        // カタログバーの親（編集モード中だけ表示）
        shopRoot = new GameObject("ShopBar", typeof(RectTransform));
        shopRoot.transform.SetParent(canvasGo.transform, false);
        var shopRt = shopRoot.GetComponent<RectTransform>();
        shopRt.anchorMin = Vector2.zero; shopRt.anchorMax = Vector2.one;
        shopRt.offsetMin = Vector2.zero; shopRt.offsetMax = Vector2.zero;

        // 下部のカタログバー（多くてもはみ出さないよう自動で幅を縮める）
        int count = catalog.Count;
        float gap = 8f;
        float maxBarW = 1820f;
        float slotW = Mathf.Min(140f, (maxBarW - gap * (count - 1)) / Mathf.Max(1, count));
        float slotH = 70f;
        float totalW = count * slotW + (count - 1) * gap;
        float startX = -totalW / 2f + slotW / 2f;

        for (int i = 0; i < count; i++)
        {
            float x = startX + i * (slotW + gap);

            var slotGo = new GameObject($"Slot{i}", typeof(RectTransform));
            slotGo.transform.SetParent(shopRoot.transform, false);
            var srt = slotGo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(x, 72f);
            srt.sizeDelta = new Vector2(slotW, slotH);
            var bg = slotGo.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.17f, 0.26f, 0.92f);
            slotBgs.Add(bg);

            // 「1. 名前 / ¥価格」
            var txt = MakeText(slotGo.transform, "Label",
                $"{i + 1}. {catalog[i].displayName}\n<size=18>¥{catalog[i].cost}</size>",
                18, Vector2.zero, new Vector2(slotW - 8, slotH),
                Color.white, TextAlignmentOptions.Center, anchor: new Vector2(0.5f, 0.5f));
            txt.textWrappingMode = TextWrappingModes.Normal;
            slotTexts.Add(txt);
        }
    }

    private const string DefaultHint =
        "家具ショップ  [左]購入(効果は翌朝)  [右]撤去  [数字/ホイール]選択  [Tab]効果確認  [P]終了";

    private void UpdateSlotHighlight()
    {
        for (int i = 0; i < slotBgs.Count; i++)
        {
            bool sel = (i == selected);
            slotBgs[i].color = sel
                ? new Color(1f, 0.62f, 0.12f, 0.95f)      // 選択中: オレンジ
                : new Color(0.16f, 0.17f, 0.26f, 0.92f);  // 通常: ダーク
            if (i < slotTexts.Count)
                slotTexts[i].color = sel ? new Color(0.12f, 0.1f, 0.05f) : Color.white;
        }
    }

    private void ShowHint(string msg, float seconds)
    {
        if (hintText == null) return;
        if (hintCoroutine != null) StopCoroutine(hintCoroutine);
        hintCoroutine = StartCoroutine(HintRoutine(msg, seconds));
    }

    private IEnumerator HintRoutine(string msg, float seconds)
    {
        hintText.text = msg;
        yield return new WaitForSeconds(seconds);
        // 編集モード中はデフォルト案内に戻す。モード外は消す。
        hintText.text = editMode ? DefaultHint : "";
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
        Vector2 pos, Vector2 sd, Color color, TextAlignmentOptions align, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sd;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
