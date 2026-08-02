using UnityEngine;
using Unity.Netcode;

// DoorInteract
// Eキーで開閉できるドア。
// 開閉状態を全プレイヤーに同期し、なめらかに回転させる。
public class DoorInteract : NetworkBehaviour
{
    [Header("ドア設定")]
    public Transform doorPivot;
    public float openAngle = 90f;
    public float rotateSpeed = 8f;

    [Header("Server検証")]
    [SerializeField, Min(0.1f)] private float serverInteractDistance = 4f;

    // ドアが開いているか
    private NetworkVariable<bool> isOpen = new NetworkVariable<bool>(false);

    void Update()
    {
        if (doorPivot == null) return;

        float targetAngle = isOpen.Value ? openAngle : 0f;

        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        doorPivot.localRotation = Quaternion.Slerp(
            doorPivot.localRotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    // ドア開閉
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ToggleDoorServerRpc(RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                rpcParams.Receive.SenderClientId,
                out NetworkClient client) ||
            client.PlayerObject == null)
            return;

        Transform target = doorPivot != null ? doorPivot : transform;
        if (Vector3.Distance(client.PlayerObject.transform.position, target.position) >
            serverInteractDistance)
        {
            Debug.LogWarning("[Door] 遠すぎるドア操作要求を拒否しました。");
            return;
        }

        isOpen.Value = !isOpen.Value;
    }
}
