using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class CarryableObject : NetworkBehaviour
{
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

    private NetworkVariable<bool> isHeld = new NetworkVariable<bool>(false);
    private NetworkVariable<ulong> holderClientId = new NetworkVariable<ulong>(9999);
    private NetworkVariable<ulong> holderNetworkObjectId = new NetworkVariable<ulong>(9999);

    private Collider[] holderColliders;
    private Coroutine restoreCollisionCoroutine;

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
    public void PickupServerRpc(ulong clientId, ulong playerNetworkObjectId)
    {
        if (isHeld.Value) return;

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

        transform.position = position;
        transform.rotation = rotation;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DropServerRpc(Vector3 dropPosition, Vector3 throwDirection, RpcParams rpcParams = default)
    {
        if (!isHeld.Value) return;
        if (rpcParams.Receive.SenderClientId != holderClientId.Value) return;

        isHeld.Value = false;
        holderClientId.Value = 9999;

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

        holderNetworkObjectId.Value = 9999;
        restoreCollisionCoroutine = null;
    }
}
