using Unity.Netcode;
using Unity.Netcode.Components;
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
[RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
public class NetworkFurniture : NetworkBehaviour
{
    private readonly NetworkVariable<int> catalogIndex =
        new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> effectActive =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isHeld =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> holderClientId =
        new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> holderPlayerObjectId =
        new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool built;
    private PlacedFurniture placed;
    private Collider[] furnitureColliders;
    private float disconnectCheckTimer;

    public bool IsHeld => isHeld.Value;
    public int CatalogIndex => catalogIndex.Value;
    public string DisplayName
    {
        get
        {
            FurnitureItem item = FurnitureCatalog.Get(catalogIndex.Value);
            return item != null ? item.displayName : "家具";
        }
    }

    public bool IsHeldBy(ulong clientId)
    {
        return isHeld.Value && holderClientId.Value == clientId;
    }

    public float BoundingRadius
    {
        get
        {
            Bounds bounds = GetVisualBounds();
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }
    }

    public override void OnNetworkSpawn()
    {
        catalogIndex.OnValueChanged += OnIndexChanged;
        effectActive.OnValueChanged += OnActiveChanged;
        isHeld.OnValueChanged += OnHeldChanged;

        if (catalogIndex.Value >= 0) BuildVisual(catalogIndex.Value);
        ApplyHeldState(isHeld.Value);
    }

    public override void OnNetworkDespawn()
    {
        catalogIndex.OnValueChanged -= OnIndexChanged;
        effectActive.OnValueChanged -= OnActiveChanged;
        isHeld.OnValueChanged -= OnHeldChanged;
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PickupServerRpc(
        ulong playerObjectId,
        RpcParams rpcParams = default)
    {
        if (isHeld.Value || !TryGetRequestingPlayer(
                playerObjectId,
                rpcParams.Receive.SenderClientId,
                out NetworkObject playerObject))
            return;

        if (Vector3.Distance(playerObject.transform.position, transform.position) > 4f)
        {
            Debug.LogWarning("[Furniture] 遠すぎる家具の持ち上げ要求を拒否しました。");
            return;
        }

        holderClientId.Value = rpcParams.Receive.SenderClientId;
        holderPlayerObjectId.Value = playerObjectId;
        isHeld.Value = true;
        ApplyHeldState(true);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdateHeldTransformServerRpc(
        Vector3 requestedPosition,
        float requestedYaw,
        RpcParams rpcParams = default)
    {
        if (!IsValidHolder(rpcParams.Receive.SenderClientId)) return;
        if (!IsFinite(requestedPosition) || !float.IsFinite(requestedYaw)) return;
        if (!TryGetHolderPlayer(out NetworkObject playerObject)) return;

        Vector3 offset = requestedPosition - playerObject.transform.position;
        if (offset.sqrMagnitude > 20.25f)
            requestedPosition = playerObject.transform.position + offset.normalized * 4.5f;

        transform.SetPositionAndRotation(
            requestedPosition,
            Quaternion.Euler(0f, requestedYaw, 0f));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DropServerRpc(
        Vector3 requestedPosition,
        float requestedYaw,
        RpcParams rpcParams = default)
    {
        if (!IsValidHolder(rpcParams.Receive.SenderClientId)) return;
        if (!IsFinite(requestedPosition) || !float.IsFinite(requestedYaw)) return;
        if (!TryGetHolderPlayer(out NetworkObject playerObject)) return;

        Vector3 offset = requestedPosition - playerObject.transform.position;
        if (offset.sqrMagnitude > 20.25f)
            requestedPosition = playerObject.transform.position + offset.normalized * 4.5f;

        Vector3 placedPosition = SnapPositionToGround(requestedPosition, playerObject);
        transform.SetPositionAndRotation(
            placedPosition,
            Quaternion.Euler(0f, requestedYaw, 0f));

        isHeld.Value = false;
        holderClientId.Value = ulong.MaxValue;
        holderPlayerObjectId.Value = ulong.MaxValue;
        ApplyHeldState(false);
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

    private void OnHeldChanged(bool oldValue, bool newValue)
    {
        ApplyHeldState(newValue);
    }

    // ---- 見た目・効果の構築 ----

    private void BuildVisual(int index)
    {
        if (built) return;
        var item = FurnitureCatalog.Get(index);
        if (item == null) return;
        built = true;

        GameObject visual;
        if (item.prefab != null)
        {
            visual = Instantiate(item.prefab, transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = item.placeholderSize;
            SetColor(visual.GetComponent<Renderer>(), item.placeholderColor);
        }

        // 効果ホルダ（Room判定・FurnitureEffectManager登録はPlacedFurnitureが行う）
        placed = GetComponent<PlacedFurniture>();
        if (placed == null) placed = gameObject.AddComponent<PlacedFurniture>();
        placed.itemId      = item.id;
        placed.displayName = item.displayName;
        placed.effect      = item.effect;
        placed.effectValue = item.effectValue;
        placed.SetEffectActive(effectActive.Value);
        placed.SetMoving(isHeld.Value);

        furnitureColliders = GetComponentsInChildren<Collider>(true);
        ApplyHeldState(isHeld.Value);
        gameObject.name = "Furniture_" + item.id;
    }

    private void Update()
    {
        if (!IsServer || !isHeld.Value) return;

        disconnectCheckTimer += Time.deltaTime;
        if (disconnectCheckTimer < 0.5f) return;
        disconnectCheckTimer = 0f;

        if (!TryGetHolderPlayer(out _))
        {
            isHeld.Value = false;
            holderClientId.Value = ulong.MaxValue;
            holderPlayerObjectId.Value = ulong.MaxValue;
            ApplyHeldState(false);
        }
    }

    private void ApplyHeldState(bool held)
    {
        if (furnitureColliders == null || furnitureColliders.Length == 0)
            furnitureColliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider furnitureCollider in furnitureColliders)
        {
            if (furnitureCollider != null)
                furnitureCollider.enabled = !held;
        }

        if (placed != null) placed.SetMoving(held);
    }

    private bool TryGetRequestingPlayer(
        ulong playerObjectId,
        ulong senderClientId,
        out NetworkObject playerObject)
    {
        playerObject = null;
        if (NetworkManager.Singleton == null) return false;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                playerObjectId,
                out playerObject))
            return false;

        return playerObject.OwnerClientId == senderClientId;
    }

    private bool TryGetHolderPlayer(out NetworkObject playerObject)
    {
        playerObject = null;
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                   holderPlayerObjectId.Value,
                   out playerObject) &&
               playerObject.OwnerClientId == holderClientId.Value;
    }

    private bool IsValidHolder(ulong senderClientId)
    {
        return isHeld.Value && holderClientId.Value == senderClientId;
    }

    private Vector3 SnapPositionToGround(
        Vector3 requestedPosition,
        NetworkObject playerObject)
    {
        float bottomOffset = GetBottomOffset();
        Vector3 origin = requestedPosition + Vector3.up * 2.5f;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            8f,
            ~0,
            QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (playerObject != null &&
                hit.collider.transform.IsChildOf(playerObject.transform))
                continue;
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.45f) continue;

            return new Vector3(
                requestedPosition.x,
                hit.point.y + bottomOffset + 0.01f,
                requestedPosition.z);
        }

        return requestedPosition;
    }

    private float GetBottomOffset()
    {
        Bounds bounds = GetVisualBounds();
        return Mathf.Max(0.05f, transform.position.y - bounds.min.y);
    }

    private Bounds GetVisualBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.one * 0.5f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
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
