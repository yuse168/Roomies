using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnSync : NetworkBehaviour
{
    private static int spawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(TeleportAfterSpawn());
        }
    }

    private IEnumerator TeleportAfterSpawn()
    {
        yield return null;

        SpawnPoint[] spawnPoints = FindObjectsByType<SpawnPoint>();

        Debug.Log("[PlayerSpawnSync] spawnPoints.Length=" + spawnPoints.Length);

        if (spawnPoints.Length == 0) yield break;

        int index = spawnIndex % spawnPoints.Length;
        spawnIndex++;

        Transform spawnPoint = spawnPoints[index].transform;

        Debug.Log("[PlayerSpawnSync] Teleport to " + spawnPoint.name + " pos=" + spawnPoint.position);

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null && controller.enabled)
        {
            controller.enabled = false;
        }

        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;

        if (controller != null)
        {
            controller.enabled = true;
        }

        TeleportClientRpc(spawnPoint.position, spawnPoint.rotation);
    }

    [ClientRpc]
    public void TeleportClientRpc(
        Vector3 position,
        Quaternion rotation
    )
    {
        CharacterController controller =
            GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = position;
        transform.rotation = rotation;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}