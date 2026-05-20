using Unity.Netcode;
using UnityEngine;

public class DeliveryZone : NetworkBehaviour
{
    [Header("再スポーン設定")]
    [SerializeField] private NetworkObject deliveryBoxPrefab;
    [SerializeField] private Transform boxSpawnPoint;

    public NetworkObject CurrentBox { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("DeliveryBox")) return;

        NetworkObject boxNetObj = other.GetComponent<NetworkObject>();

        if (boxNetObj != null)
        {
            CurrentBox = boxNetObj;
            Debug.Log("箱が納品エリアに入りました");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("DeliveryBox")) return;

        NetworkObject boxNetObj = other.GetComponent<NetworkObject>();

        if (boxNetObj != null && CurrentBox == boxNetObj)
        {
            CurrentBox = null;
            Debug.Log("箱が納品エリアから出ました");
        }
    }

    public bool HasBox()
    {
        return CurrentBox != null;
    }

    public void RemoveBox()
    {
        if (!IsServer) return;

        if (CurrentBox != null)
        {
            CurrentBox.Despawn(true);
            CurrentBox = null;
        }

        SpawnNewBox();
    }

    private void SpawnNewBox()
    {
        if (deliveryBoxPrefab == null || boxSpawnPoint == null)
        {
            Debug.Log("箱PrefabかSpawnPointが設定されていません");
            return;
        }

        NetworkObject newBox = Instantiate(
            deliveryBoxPrefab,
            boxSpawnPoint.position,
            boxSpawnPoint.rotation
        );

        newBox.Spawn();

        Debug.Log("新しい箱をスポーンしました");
    }
}