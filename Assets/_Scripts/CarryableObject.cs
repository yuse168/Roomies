using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class CarryableObject : NetworkBehaviour
{
    private const ulong NoHolder = ulong.MaxValue;

    [Header("重さ設定")]
    [Range(1, 5)]
    public int weightLevel = 1;

    [Header("投げる力設定")]
    public float baseForwardPower = 6f;
    public float baseUpPower = 1.5f;
    public float baseDownPower = 0f;

    [Header("重さごとの補正")]
    public float weightForwardReduction = 1f;
    public float weightUpReduction = 0.3f;
    public float weightDownIncrease = 1f;

    [Header("持てる物の設定")]
    public Rigidbody rb;
    public Collider objectCollider;

    [Header("プレイヤーとの当たり判定")]
    public float restoreHolderCollisionDelay = 0.5f;

    [Header("Server検証")]
    [SerializeField, Min(0.1f)] private float serverPickupDistance = 4f;
    [SerializeField, Min(0.1f)] private float serverMaxHeldDistance = 4.5f;
    [SerializeField, Min(0.1f)] private float holderDisconnectCheckInterval = 0.5f;

    private NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false);
    private NetworkVariable<ulong> holderClientId = new NetworkVariable<ulong>(NoHolder);
    private NetworkVariable<ulong> holderNetworkObjectId = new NetworkVariable<ulong>(NoHolder);

    private Collider[] holderColliders;
    private Coroutine restoreCollisionCoroutine;
    private float disconnectCheckTimer;
    private float relocationUntil;

    public void ServerRelocate(Vector3 position)
    {
        if (!IsServer) return;
        transform.position = position;
        relocationUntil = Time.time + .75f;
    }

    public void ServerRelease() => ReleaseHolder();

    public bool IsHeld => isHeld.Value;

    [Header("落ちてぶつかった音")]
    [Tooltip("この速さ以上でぶつかったときだけ鳴らす（m/s）")]
    [SerializeField, Min(0.1f)] private float impactMinSpeed = 1.6f;

    [Tooltip("この速さでぶつかると音量が最大になる（m/s）")]
    [SerializeField, Min(1f)] private float impactLoudSpeed = 7f;

    [Tooltip("続けて鳴らさないための間隔（秒）。転がって何度も鳴るのを防ぐ")]
    [SerializeField, Min(0f)] private float impactCooldown = 0.18f;

    private float nextImpactTime;

    /// <summary>
    /// 落ちた先でぶつかったときの音。手を離した瞬間ではなく、着地した場所で鳴る。
    /// 物理はサーバーが持っているので、サーバーだけが判定して全員へ配る。
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsSpawned || !IsServer) return;
        if (isHeld.Value) return;                       // 持っている間は鳴らさない
        if (Time.time < nextImpactTime) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < impactMinSpeed) return;

        nextImpactTime = Time.time + impactCooldown;

        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        float strength = Mathf.InverseLerp(impactMinSpeed, impactLoudSpeed, speed);
        ImpactSoundRpc(point, strength);
    }

    /// <summary>ぶつかった音（全員に聞こえる。ぶつかった場所から鳴る）。</summary>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ImpactSoundRpc(Vector3 position, float strength)
    {
        // 重さで音を変える。軽い物はカタッ、重い物はドスン
        string key = weightLevel <= 1 ? "drop/light"
            : weightLevel <= 3 ? "drop/medium"
            : "drop/heavy";

        // 重いものほど低く、勢いが強いほど大きく鳴らす
        float pitch = Mathf.Clamp(1.12f - (weightLevel - 1) * 0.06f, 0.7f, 1.2f);
        float volume = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(strength));

        GameAudio.PlayExclusiveAt(
            key, "drop#" + NetworkObjectId, position, volume, pitch, 26f);
    }
    public bool IsHeldBy(ulong clientId)
    {
        return isHeld.Value && holderClientId.Value == clientId;
    }

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (objectCollider == null) objectCollider = GetComponent<Collider>();

        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.mass = weightLevel;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PickupServerRpc(
        ulong clientId,
        ulong playerNetworkObjectId,
        RpcParams rpcParams = default)
    {
        if (isHeld.Value) return;
        if (clientId != rpcParams.Receive.SenderClientId) return;
        if (!TryGetPlayer(playerNetworkObjectId, clientId, out NetworkObject playerObject))
            return;
        if (!playerObject.IsPlayerObject) return;
        var interaction = playerObject.GetComponent<PlayerInteract>();
        if (interaction == null || HasOtherHeldObject(clientId)) return;

        if (Vector3.Distance(playerObject.transform.position, transform.position) >
            serverPickupDistance)
        {
            Debug.LogWarning("[Carryable] 遠すぎる持ち上げ要求を拒否しました。");
            return;
        }

        // Another player may grab a thrown ore before the delayed collision restore.
        // Restore the previous holder before replacing its network id/collider cache.
        IgnoreHolderCollision(false);
        isHeld.Value = true;
        holderClientId.Value = clientId;
        holderNetworkObjectId.Value = playerNetworkObjectId;

        if (restoreCollisionCoroutine != null)
        {
            StopCoroutine(restoreCollisionCoroutine);
            restoreCollisionCoroutine = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (objectCollider != null)
        {
            objectCollider.enabled = true;
        }

        IgnoreHolderCollision(true);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdateHeldPositionServerRpc(Vector3 position, Quaternion rotation, RpcParams rpcParams = default)
    {
        if (!isHeld.Value) return;
        if (rpcParams.Receive.SenderClientId != holderClientId.Value) return;
        if (Time.time < relocationUntil) return;
        if (!IsFinite(position) || !IsFinite(rotation)) return;
        if (!TryGetHolderPlayer(out NetworkObject playerObject))
        {
            ReleaseHolder();
            return;
        }

        Vector3 offset = position - playerObject.transform.position;
        if (offset.sqrMagnitude > serverMaxHeldDistance * serverMaxHeldDistance)
            position = playerObject.transform.position +
                       offset.normalized * serverMaxHeldDistance;

        transform.position = position;
        transform.rotation = rotation;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DropServerRpc(Vector3 dropPosition, Vector3 throwDirection, RpcParams rpcParams = default)
    {
        if (!isHeld.Value) return;
        if (rpcParams.Receive.SenderClientId != holderClientId.Value) return;
        if (!IsFinite(dropPosition) || !IsFinite(throwDirection)) return;
        if (!TryGetHolderPlayer(out NetworkObject playerObject))
        {
            ReleaseHolder();
            return;
        }

        Vector3 offset = dropPosition - playerObject.transform.position;
        if (offset.sqrMagnitude > serverMaxHeldDistance * serverMaxHeldDistance)
            dropPosition = playerObject.transform.position +
                           offset.normalized * serverMaxHeldDistance;

        isHeld.Value = false;
        holderClientId.Value = NoHolder;

        transform.position = dropPosition;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            float forwardPower = Mathf.Max(0f, baseForwardPower - ((weightLevel - 1) * weightForwardReduction));
            float upPower = Mathf.Max(0f, baseUpPower - ((weightLevel - 1) * weightUpReduction));
            float downPower = baseDownPower + ((weightLevel - 1) * weightDownIncrease);

            Vector3 force =
                throwDirection.normalized * forwardPower +
                Vector3.up * upPower +
                Vector3.down * downPower;

            rb.AddForce(force, ForceMode.Impulse);
        }

        restoreCollisionCoroutine = StartCoroutine(RestoreHolderCollisionAfterDelay());
    }

    void IgnoreHolderCollision(bool ignore)
    {
        if (objectCollider == null) return;
        if (!ignore)
        {
            if (holderColliders != null)
                foreach (var col in holderColliders)
                    if (col != null) Physics.IgnoreCollision(objectCollider, col, false);
            holderColliders = null;
            return;
        }
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(holderNetworkObjectId.Value, out NetworkObject holderObject))
        {
            holderColliders = holderObject.GetComponentsInChildren<Collider>();

            foreach (Collider col in holderColliders)
            {
                if (col != null)
                {
                    Physics.IgnoreCollision(objectCollider, col, ignore);
                }
            }
        }
    }

    IEnumerator RestoreHolderCollisionAfterDelay()
    {
        yield return new WaitForSeconds(restoreHolderCollisionDelay);

        if (!isHeld.Value)
        {
            IgnoreHolderCollision(false);
        }

        holderNetworkObjectId.Value = NoHolder;
        restoreCollisionCoroutine = null;
    }

    private void Update()
    {
        if (!IsServer || !isHeld.Value) return;

        disconnectCheckTimer += Time.deltaTime;
        if (disconnectCheckTimer < holderDisconnectCheckInterval) return;
        disconnectCheckTimer = 0f;

        if (!TryGetHolderPlayer(out _))
            ReleaseHolder();
    }

    private bool TryGetPlayer(
        ulong playerNetworkObjectId,
        ulong clientId,
        out NetworkObject playerObject)
    {
        playerObject = null;
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                   playerNetworkObjectId,
                   out playerObject) &&
               playerObject.OwnerClientId == clientId;
    }

    private bool TryGetHolderPlayer(out NetworkObject playerObject)
    {
        return TryGetPlayer(
            holderNetworkObjectId.Value,
            holderClientId.Value,
            out playerObject);
    }

    private void ReleaseHolder()
    {
        if (!IsServer) return;

        if (restoreCollisionCoroutine != null)
        {
            StopCoroutine(restoreCollisionCoroutine);
            restoreCollisionCoroutine = null;
        }

        IgnoreHolderCollision(false);
        isHeld.Value = false;
        holderClientId.Value = NoHolder;
        holderNetworkObjectId.Value = NoHolder;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    private bool HasOtherHeldObject(ulong clientId)
    {
        foreach (var item in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (item == NetworkObject) continue;
            var carry = item.GetComponent<CarryableObject>();
            if (carry != null && carry.IsHeldBy(clientId)) return true;
            var furniture = item.GetComponent<NetworkFurniture>();
            if (furniture != null && furniture.IsHeldBy(clientId)) return true;
        }
        return false;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z) &&
               float.IsFinite(value.w);
    }
}
