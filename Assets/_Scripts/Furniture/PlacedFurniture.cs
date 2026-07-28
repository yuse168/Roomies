using UnityEngine;

/// <summary>
/// 設置済みの家具に付くマーカー。
/// 「Room」タグのオブジェクトの上（接触）にある間だけ効果が有効になる。
/// </summary>
public class PlacedFurniture : MonoBehaviour
{
    [Tooltip("Roomと判定するタグ")]
    public const string RoomTag = "Room";

    public string itemId;
    public string displayName;
    public FurnitureEffect effect = FurnitureEffect.None;
    public float effectValue;

    /// <summary>現在 Room の上に乗っているか（FurnitureEffectManagerが更新）。</summary>
    public bool IsOnRoom { get; private set; }

    /// <summary>効果が有効か（購入直後はfalse、翌朝にtrueになる）。</summary>
    public bool EffectActive { get; private set; }

    /// <summary>プレイヤーが移動中か。移動中は家具効果を停止する。</summary>
    public bool IsMoving { get; private set; }

    public void SetEffectActive(bool active) => EffectActive = active;
    public void SetMoving(bool moving) => IsMoving = moving;

    /// <summary>効果が実際に発動中か（翌朝以降 かつ Roomの上）。</summary>
    public bool IsEffectActive => EffectActive && IsOnRoom && !IsMoving;

    private Collider cachedCollider;

    private void Awake()
    {
        cachedCollider = GetComponentInChildren<Collider>();
    }

    private void OnEnable()
    {
        FurnitureEffectManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (FurnitureEffectManager.InstanceOrNull != null)
            FurnitureEffectManager.InstanceOrNull.Unregister(this);
    }

    /// <summary>
    /// 真下に「Room」タグのコライダーが接しているかを判定して IsOnRoom を更新する。
    /// FurnitureEffectManager から定期的に呼ばれる。
    /// </summary>
    public void RecalculateOnRoom()
    {
        IsOnRoom = ComputeOnRoom();
    }

    private bool ComputeOnRoom()
    {
        if (cachedCollider == null)
            cachedCollider = GetComponentInChildren<Collider>();

        if (cachedCollider == null) return false;

        Bounds b = cachedCollider.bounds;

        // 家具の底面のすぐ下に薄い箱を作り、そこに重なる Room タグのコライダーを探す
        Vector3 center = new Vector3(b.center.x, b.min.y - 0.03f, b.center.z);
        Vector3 half   = new Vector3(b.extents.x * 0.9f, 0.06f, b.extents.z * 0.9f);

        var cols = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            if (c == null) continue;
            if (c.transform.IsChildOf(transform)) continue; // 自分自身は無視
            if (c.CompareTag(RoomTag)) return true;
        }
        return false;
    }
}
