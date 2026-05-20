using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// PlayerNameDisplay
// プレイヤーの頭上に名前を表示するコード。
// 今は仮名を同期し、将来的にSteam名へ差し替える想定。
public class PlayerNameDisplay : NetworkBehaviour
{
    [Header("名前表示UI")]
    public Transform nameCanvasTransform;
    public TextMeshProUGUI nameText;

    // 全員に同期されるプレイヤー名
    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;

        // サーバー側で仮の名前を設定
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

    void LateUpdate()
    {
        // 名前UIだけをカメラの方へ向ける
        if (nameCanvasTransform != null && Camera.main != null)
        {
            nameCanvasTransform.LookAt(Camera.main.transform);
            nameCanvasTransform.Rotate(0, 180f, 0);
        }
    }
}