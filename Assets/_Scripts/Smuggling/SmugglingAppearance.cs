using UnityEngine;

/// <summary>
/// 仮の見た目（カプセルなど）を、あとから本番モデル（Prefab / FBX）へ差し替えるための共通コンポーネント。
///
/// 使い方（どちらでもOK）：
///  A) Inspector の「本番モデル」に FBX か Prefab を入れる
///     → 実行時に仮モデルが消えて、そちらが生成される。位置/回転/スケールもここで微調整できる。
///  B) プレハブを開いて Placeholder の子を直接差し替える
///     → このコンポーネントは何もしない（本番モデル未設定なら仮モデルをそのまま使う）
/// </summary>
public class SmugglingAppearance : MonoBehaviour
{
    [Header("本番モデル（FBX / Prefab をここに入れると仮モデルと差し替わる）")]
    [SerializeField] private GameObject modelPrefab;

    [Header("差し替えたモデルの微調整")]
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 modelLocalEuler = Vector3.zero;
    [SerializeField] private float modelScale = 1f;

    [Header("参照（プレハブ生成時に自動設定済み）")]
    [Tooltip("モデルをぶら下げる親。未設定なら自分自身。")]
    [SerializeField] private Transform modelRoot;
    [Tooltip("仮モデル。本番モデルを入れると非表示になる。")]
    [SerializeField] private GameObject placeholder;

    private GameObject spawnedModel;
    private bool placeholderPrepared;

    /// <summary>差し替え後（または仮）の見た目ルート。向きを合わせたいときに使う。</summary>
    public Transform VisualRoot => modelRoot != null ? modelRoot : transform;
    public bool HasReplacementModel => modelPrefab != null;

    /// <summary>
    /// 仮モデルだけを夜でも視認できるUnlit材質へ切り替える。
    /// 本番モデルが設定されている場合は何もしない。
    /// </summary>
    public void PreparePlaceholderForNight()
    {
        if (placeholderPrepared || modelPrefab != null || placeholder == null) return;
        placeholderPrepared = true;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Color");
        if (unlit == null) return;

        foreach (Renderer renderer in placeholder.GetComponentsInChildren<Renderer>(true))
        {
            Color color = Color.white;
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                color = renderer.sharedMaterial.GetColor("_BaseColor");
            else if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                color = renderer.sharedMaterial.color;

            Material material = new Material(unlit)
            {
                name = "SmugglingPlaceholder_Unlit_Runtime",
                color = color,
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            renderer.material = material;
        }
    }

    private void Awake()
    {
        Apply();
    }

    private void Apply()
    {
        if (modelPrefab == null) return;

        Transform parent = modelRoot != null ? modelRoot : transform;

        if (placeholder != null)
        {
            placeholder.SetActive(false);
        }

        spawnedModel = Instantiate(modelPrefab, parent);
        spawnedModel.name = "Model";
        spawnedModel.transform.localPosition = modelLocalPosition;
        spawnedModel.transform.localRotation = Quaternion.Euler(modelLocalEuler);
        spawnedModel.transform.localScale = Vector3.one * Mathf.Max(0.0001f, modelScale);

        // FBXをそのまま入れるとColliderが付いてきて誤爆することがあるので当たり判定は無効化。
        // （当たり判定は仮プレハブ側の CapsuleCollider / CharacterController に任せる）
        foreach (var col in spawnedModel.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }
    }
}
