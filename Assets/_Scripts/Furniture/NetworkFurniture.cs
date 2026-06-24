using Unity.Netcode;
using UnityEngine;

/// <summary>
/// マルチ同期される家具。サーバーが Spawn し、catalogIndex を同期する。
/// 各クライアントはインデックスから共有カタログを引いて同じ見た目・効果を復元する。
/// 効果の有効/無効（翌朝から）も NetworkVariable で同期する。
///
/// このコンポーネントは「NetworkFurniture プレハブ」に付ける：
///   空GameObject + NetworkObject + NetworkFurniture
/// 見た目（仮ブロック）は実行時に子として生成する。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkFurniture : NetworkBehaviour
{
    private readonly NetworkVariable<int> catalogIndex =
        new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> effectActive =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool built;
    private PlacedFurniture placed;

    public override void OnNetworkSpawn()
    {
        catalogIndex.OnValueChanged += OnIndexChanged;
        effectActive.OnValueChanged += OnActiveChanged;

        if (catalogIndex.Value >= 0) BuildVisual(catalogIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        catalogIndex.OnValueChanged -= OnIndexChanged;
        effectActive.OnValueChanged -= OnActiveChanged;
    }

    // ---- サーバー操作 ----

    /// <summary>サーバーがSpawn直後に呼ぶ。カタログindexを同期。</summary>
    public void ServerSetIndex(int index)
    {
        if (!IsServer) return;
        catalogIndex.Value = index;
    }

    /// <summary>サーバーが朝に呼ぶ。効果を有効化（全員に同期）。</summary>
    public void ServerActivateEffect()
    {
        if (!IsServer) return;
        effectActive.Value = true;
    }

    // ---- 同期コールバック ----

    private void OnIndexChanged(int oldValue, int newValue)
    {
        if (newValue >= 0) BuildVisual(newValue);
    }

    private void OnActiveChanged(bool oldValue, bool newValue)
    {
        if (placed != null) placed.SetEffectActive(newValue);
    }

    // ---- 見た目・効果の構築 ----

    private void BuildVisual(int index)
    {
        if (built) return;
        var item = FurnitureCatalog.Get(index);
        if (item == null) return;
        built = true;

        // 見た目（仮ブロック）を子として生成
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Visual";
        cube.transform.SetParent(transform, false);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localScale = item.placeholderSize;
        SetColor(cube.GetComponent<Renderer>(), item.placeholderColor);

        // 効果ホルダ（Room判定・FurnitureEffectManager登録はPlacedFurnitureが行う）
        placed = GetComponent<PlacedFurniture>();
        if (placed == null) placed = gameObject.AddComponent<PlacedFurniture>();
        placed.itemId      = item.id;
        placed.displayName = item.displayName;
        placed.effect      = item.effect;
        placed.effectValue = item.effectValue;
        placed.SetEffectActive(effectActive.Value);
    }

    private static void SetColor(Renderer r, Color c)
    {
        if (r == null) return;
        var m = r.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        m.color = c;
    }
}
