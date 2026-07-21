using Steamworks;
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
            // Steam名が届くまでの仮名
            playerName.Value = "Player " + OwnerClientId;
        }

        // 自分のSteam表示名をサーバーへ送って全員に同期する
        if (IsOwner)
        {
            string steamName = GetLocalSteamName();
            if (!string.IsNullOrWhiteSpace(steamName))
            {
                SubmitNameServerRpc(TruncateForFixedString(steamName));
            }
        }

        UpdateNameText(playerName.Value.ToString());
    }

    /// <summary>自分のSteam表示名（Steam未初期化ならnull）。</summary>
    private static string GetLocalSteamName()
    {
        try
        {
            return SteamManager.Initialized ? SteamFriends.GetPersonaName() : null;
        }
        catch
        {
            return null;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitNameServerRpc(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        playerName.Value = TruncateForFixedString(name);
    }

    // FixedString32Bytes（UTF-8で最大29バイト）に収まるよう安全に切り詰める。
    // 日本語名はマルチバイトなので文字数ではなくバイト数で判定する。
    private static string TruncateForFixedString(string s)
    {
        const int maxBytes = 29;
        var sb = new System.Text.StringBuilder();
        int bytes = 0;
        foreach (char c in s)
        {
            int b = System.Text.Encoding.UTF8.GetByteCount(c.ToString());
            if (bytes + b > maxBytes) break;
            sb.Append(c);
            bytes += b;
        }
        return sb.ToString();
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