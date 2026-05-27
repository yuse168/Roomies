using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameDisplay : NetworkBehaviour
{
    [Header("名前表示UI")]
    public Transform nameCanvasTransform;
    public TextMeshProUGUI nameText;

    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;

        if (IsServer)
        {
            playerName.Value = "Player " + OwnerClientId;
        }

        UpdateNameText(playerName.Value.ToString());
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        UpdateNameText(newName.ToString());
    }

    private void UpdateNameText(string newName)
    {
        if (nameText != null)
        {
            nameText.text = newName;
        }
    }

    public string GetPlayerName()
    {
        return playerName.Value.ToString();
    }

    private void LateUpdate()
    {
        if (nameCanvasTransform != null && Camera.main != null)
        {
            nameCanvasTransform.LookAt(Camera.main.transform);
            nameCanvasTransform.Rotate(0, 180f, 0);
        }
    }
}