using Unity.Netcode;
using UnityEngine;

public class DeliveryZone : NetworkBehaviour
{
    [Header("箱Prefab")]
    [SerializeField] private NetworkObject deliveryBoxPrefab;

    [Header("スポーン位置")]
    [SerializeField] private Transform boxSpawnPoint;

    [Header("レア確率")]
    [SerializeField, Range(0, 100)]
    private int rarePercent = 20;

    public NetworkObject CurrentBox { get; private set; }

    private void Start()
    {
        if (!IsServer) return;

        SpawnNewBox();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("DeliveryBox")) return;

        NetworkObject boxNetObj =
            other.GetComponent<NetworkObject>();

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

        NetworkObject boxNetObj =
            other.GetComponent<NetworkObject>();

        if (boxNetObj != null &&
            CurrentBox == boxNetObj)
        {
            CurrentBox = null;

            Debug.Log("箱が納品エリアから出ました");
        }
    }

    public bool HasBox()
    {
        return CurrentBox != null;
    }

    public DeliveryItem GetCurrentItem()
    {
        if (CurrentBox == null) return null;

        return CurrentBox
            .GetComponentInChildren<DeliveryItem>();
    }

    public void RemoveBox()
    {
        if (!IsServer) return;

        if (CurrentBox != null)
        {
            Debug.Log("箱を削除");

            CurrentBox.Despawn(true);

            CurrentBox = null;
        }

        SpawnNewBox();
    }

    private void SpawnNewBox()
    {
        if (deliveryBoxPrefab == null)
        {
            Debug.LogError(
                "deliveryBoxPrefab が設定されていません"
            );
            return;
        }

        if (boxSpawnPoint == null)
        {
            Debug.LogError(
                "boxSpawnPoint が設定されていません"
            );
            return;
        }

        NetworkObject newBox = Instantiate(
            deliveryBoxPrefab,
            boxSpawnPoint.position,
            boxSpawnPoint.rotation
        );

        newBox.Spawn();

        DeliveryItem deliveryItem =
            newBox.GetComponentInChildren<DeliveryItem>();

        if (deliveryItem != null)
        {
            bool isRare =
                Random.Range(0, 100) < rarePercent;

            deliveryItem.SetRare(isRare);

            Debug.Log("レア判定: " + isRare);
        }
        else
        {
            Debug.LogError(
                "DeliveryItem が見つかりません"
            );
        }

        Debug.Log("新しい箱をスポーンしました");
    }
}