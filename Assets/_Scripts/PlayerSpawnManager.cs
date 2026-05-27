using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField]
    private Transform[] spawnPoints;

    private int currentSpawnIndex = 0;

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager がありません");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback +=
            OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -=
                OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        StartCoroutine(
            MovePlayerToSpawnPoint(clientId)
        );
    }

    private IEnumerator MovePlayerToSpawnPoint(
        ulong clientId
    )
    {
        // Player生成待ち
        yield return null;
        yield return null;

        if (!NetworkManager.Singleton.ConnectedClients
            .ContainsKey(clientId))
        {
            yield break;
        }

        NetworkObject player =
            NetworkManager.Singleton
            .ConnectedClients[clientId]
            .PlayerObject;

        if (player == null)
        {
            Debug.LogWarning(
                "PlayerObject がまだありません"
            );

            yield break;
        }

        if (spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            Debug.LogError(
                "SpawnPoint が設定されていません"
            );

            yield break;
        }

        Transform spawnPoint =
            spawnPoints[currentSpawnIndex];

        CharacterController controller =
            player.GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        // サーバー側移動
        player.transform.position =
            spawnPoint.position;

        player.transform.rotation =
            spawnPoint.rotation;

        if (controller != null)
        {
            controller.enabled = true;
        }

        // クライアント側強制同期
        PlayerSpawnSync sync =
            player.GetComponent<PlayerSpawnSync>();

        if (sync != null)
        {
            sync.TeleportClientRpc(
                spawnPoint.position,
                spawnPoint.rotation
            );
        }

        currentSpawnIndex++;

        if (currentSpawnIndex >= spawnPoints.Length)
        {
            currentSpawnIndex = 0;
        }

        Debug.Log(
            "PlayerをSpawnPointへ移動: " +
            spawnPoint.name
        );
    }
}