using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DeliveryZone : NetworkBehaviour
{
    [Header("箱Prefab")]
    [SerializeField] private NetworkObject normalBoxPrefab;
    [SerializeField] private NetworkObject rareBoxPrefab;

    [Header("スポーン位置")]
    [SerializeField] private Transform boxSpawnPoint;
    [SerializeField] private Vector2 spawnJitter = new Vector2(0.6f, 0.6f);
    [SerializeField] private float spawnHeight = 0.5f;

    [Header("配達トラック")]
    [Tooltip("荷物を積むトラック。荷台にある箱は「町に出ている数」に数えない")]
    [SerializeField] private DeliveryTruck truck;

    // 出ている数の上限は GameBalance（deliveryLooseBoxes / deliveryMaxBoxes）で決める

    [Header("レア確率")]
    [SerializeField, Range(0, 100)]
    private int rarePercent = 20;

    private List<NetworkObject> boxesInZone = new List<NetworkObject>();
    private readonly List<NetworkObject> spawnedBoxes = new List<NetworkObject>();
    private readonly Dictionary<NetworkObject, HashSet<Collider>> collidersInZone = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        ServerRefill();
    }

    /// <summary>
    /// 町に出ている箱（荷台の外・割れていない）が GameBalance の数になるまで集荷場に出す（サーバーのみ）。
    /// 荷台に積んだときと、割れたときに呼ぶ。全体の上限（deliveryMaxBoxes）は超えない。
    /// </summary>
    public void ServerRefill()
    {
        if (!IsServer) return;

        spawnedBoxes.RemoveAll(b => b == null || !b.IsSpawned);

        GameBalance balance = GameBalance.Current;
        int loose = 0;
        foreach (var box in spawnedBoxes)
            if (truck == null || !truck.ServerContains(box)) loose++;

        int room = Mathf.Min(balance.deliveryLooseBoxes - loose, balance.deliveryMaxBoxes - spawnedBoxes.Count);
        for (int i = 0; i < room; i++) SpawnNewBox();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        NetworkObject boxNetObj =
            other.GetComponentInParent<NetworkObject>();

        if (boxNetObj == null || !boxNetObj.IsSpawned ||
            (!boxNetObj.CompareTag("DeliveryBox") && !other.CompareTag("DeliveryBox"))) return;

        if (!collidersInZone.TryGetValue(boxNetObj, out var colliders))
        {
            colliders = new HashSet<Collider>();
            collidersInZone.Add(boxNetObj, colliders);
            boxesInZone.Add(boxNetObj);
        }
        colliders.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        NetworkObject boxNetObj =
            other.GetComponentInParent<NetworkObject>();

        if (boxNetObj != null && collidersInZone.TryGetValue(boxNetObj, out var colliders))
        {
            colliders.Remove(other);
            if (colliders.Count == 0)
            {
                collidersInZone.Remove(boxNetObj);
                boxesInZone.Remove(boxNetObj);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        boxesInZone.Clear();
        collidersInZone.Clear();
        spawnedBoxes.Clear();
    }

    public bool HasBox()
    {
        PruneInvalidBoxes();
        return boxesInZone.Count > 0;
    }

    public DeliveryItem GetCurrentItem()
    {
        PruneInvalidBoxes();
        if (boxesInZone.Count == 0) return null;

        return boxesInZone[0]
            .GetComponentInChildren<DeliveryItem>();
    }

    public void RemoveBox()
    {
        if (!IsServer) return;
        PruneInvalidBoxes();

        if (boxesInZone.Count > 0)
        {
            NetworkObject box = boxesInZone[0];
            boxesInZone.RemoveAt(0);
            collidersInZone.Remove(box);

            box.Despawn(true);

            Debug.Log("箱を削除 エリア内残り: " + boxesInZone.Count);
        }

        ServerRefill();
    }

    public void RespawnBox()
    {
        if (!IsServer) return;

        ServerRefill();
    }

    private void SpawnNewBox()
    {
        if (normalBoxPrefab == null)
        {
            Debug.LogError(
                "normalBoxPrefab が設定されていません"
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

        bool isRare =
            Random.Range(0, 100) < rarePercent;

        NetworkObject prefab = isRare && rareBoxPrefab != null
            ? rareBoxPrefab
            : normalBoxPrefab;

        Vector3 spawnPos = boxSpawnPoint.position
            + new Vector3(
                Random.Range(-Mathf.Abs(spawnJitter.x), Mathf.Abs(spawnJitter.x)),
                spawnHeight,
                Random.Range(-Mathf.Abs(spawnJitter.y), Mathf.Abs(spawnJitter.y))
            );

        NetworkObject newBox = Instantiate(
            prefab,
            spawnPos,
            boxSpawnPoint.rotation
        );

        newBox.Spawn();
        spawnedBoxes.Add(newBox);

        Rigidbody boxRb = newBox.GetComponent<Rigidbody>();
        if (boxRb != null)
        {
            boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            boxRb.useGravity = true;
        }

        DeliveryItem deliveryItem =
            newBox.GetComponentInChildren<DeliveryItem>();

        if (deliveryItem != null)
        {
            deliveryItem.SetOwnerZone(this);
            deliveryItem.SetRare(isRare);
        }
        else if (isRare)
        {
            Debug.LogWarning(
                "レアPrefab に DeliveryItem が設定されていません。ペナルティ等は機能しません。"
            );
        }

    }

    private void PruneInvalidBoxes()
    {
        for (int i = boxesInZone.Count - 1; i >= 0; i--)
        {
            var box = boxesInZone[i];
            bool valid = box != null && box.IsSpawned;
            if (valid && collidersInZone.TryGetValue(box, out var colliders))
            {
                colliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
                valid = colliders.Count > 0;
            }
            if (valid) continue;
            collidersInZone.Remove(box);
            boxesInZone.RemoveAt(i);
        }
    }
}
