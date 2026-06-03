using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DeliveryZone : NetworkBehaviour
{
    [Header("箱Prefab")]
    [SerializeField] private NetworkObject deliveryBoxPrefab;

    [Header("スポーン位置")]
    [SerializeField] private Transform boxSpawnPoint;

    [Header("箱の最大数")]
    [SerializeField] private int maxBoxCount = 3;

    [Header("レア確率")]
    [SerializeField, Range(0, 100)]
    private int rarePercent = 20;

    private List<NetworkObject> boxesInZone = new List<NetworkObject>();

    private void Start()
    {
        if (!IsServer) return;

        for (int i = 0; i < maxBoxCount; i++)
        {
            SpawnNewBox();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("DeliveryBox")) return;

        NetworkObject boxNetObj =
            other.GetComponent<NetworkObject>();

        if (boxNetObj != null && !boxesInZone.Contains(boxNetObj))
        {
            boxesInZone.Add(boxNetObj);

            Debug.Log("箱が納品エリアに入りました エリア内: " + boxesInZone.Count);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("DeliveryBox")) return;

        NetworkObject boxNetObj =
            other.GetComponent<NetworkObject>();

        if (boxNetObj != null && boxesInZone.Contains(boxNetObj))
        {
            boxesInZone.Remove(boxNetObj);

            Debug.Log("箱が納品エリアから出ました エリア内: " + boxesInZone.Count);
        }
    }

    public bool HasBox()
    {
        return boxesInZone.Count > 0;
    }

    public DeliveryItem GetCurrentItem()
    {
        if (boxesInZone.Count == 0) return null;

        return boxesInZone[0]
            .GetComponentInChildren<DeliveryItem>();
    }

    public void RemoveBox()
    {
        if (!IsServer) return;

        if (boxesInZone.Count > 0)
        {
            NetworkObject box = boxesInZone[0];
            boxesInZone.RemoveAt(0);

            box.Despawn(true);

            Debug.Log("箱を削除 エリア内残り: " + boxesInZone.Count);
        }

        SpawnNewBox();
    }

    public void RespawnBox()
    {
        if (!IsServer) return;

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
            deliveryItem.SetOwnerZone(this);

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