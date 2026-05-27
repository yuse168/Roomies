using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnSync : NetworkBehaviour
{
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