using Steamworks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameDisplay : NetworkBehaviour
{
    private const string ColorPreferenceKey = "Roomies.PlayerColor";

    public static readonly Color[] CharacterColors =
    {
        new Color(0.32f, 0.76f, 0.40f), new Color(0.95f, 0.34f, 0.38f),
        new Color(0.49f, 0.32f, 0.86f), new Color(0.98f, 0.76f, 0.18f),
        new Color(0.18f, 0.48f, 0.92f), new Color(0.95f, 0.48f, 0.18f),
        new Color(0.20f, 0.78f, 0.76f), new Color(0.92f, 0.34f, 0.68f),
        new Color(0.58f, 0.78f, 0.22f), new Color(0.56f, 0.30f, 0.18f),
        new Color(0.82f, 0.84f, 0.88f), new Color(0.18f, 0.20f, 0.25f),
        new Color(0.50f, 0.64f, 0.94f), new Color(0.74f, 0.46f, 0.92f),
        new Color(0.98f, 0.62f, 0.70f), new Color(0.95f, 0.88f, 0.60f)
    };

    [Header("名前表示UI")]
    public Transform nameCanvasTransform;
    public TextMeshProUGUI nameText;

    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<int> characterColorIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;
        characterColorIndex.OnValueChanged += OnColorChanged;

        // 頭上の名前はゲーム画面では表示しない。名前データ自体はランキング等で利用する。
        if (nameCanvasTransform != null)
            nameCanvasTransform.gameObject.SetActive(false);

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

            int savedColor = Mathf.Clamp(
                PlayerPrefs.GetInt(ColorPreferenceKey, (int)(OwnerClientId % (ulong)CharacterColors.Length)),
                0,
                CharacterColors.Length - 1);
            SubmitColorServerRpc(savedColor);
        }

        UpdateNameText(playerName.Value.ToString());
        ApplyCharacterColor(characterColorIndex.Value);
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
        characterColorIndex.OnValueChanged -= OnColorChanged;
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

    public int CurrentColorIndex => characterColorIndex.Value;

    public void SetLocalCharacterColor(int colorIndex)
    {
        if (!IsOwner) return;

        colorIndex = Mathf.Clamp(colorIndex, 0, CharacterColors.Length - 1);
        PlayerPrefs.SetInt(ColorPreferenceKey, colorIndex);
        PlayerPrefs.Save();
        ApplyCharacterColor(colorIndex);
        SubmitColorServerRpc(colorIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitColorServerRpc(int colorIndex)
    {
        characterColorIndex.Value = Mathf.Clamp(colorIndex, 0, CharacterColors.Length - 1);
    }

    private void OnColorChanged(int previous, int current)
    {
        ApplyCharacterColor(current);
    }

    private void ApplyCharacterColor(int colorIndex)
    {
        Color color = CharacterColors[Mathf.Clamp(colorIndex, 0, CharacterColors.Length - 1)];
        var block = new MaterialPropertyBlock();
        Transform bobBody = null;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "BobBody") continue;
            bobBody = child;
            break;
        }

        if (bobBody == null)
        {
            Debug.LogWarning("[PlayerNameDisplay] BobBodyが見つからないため、カラー変更をスキップしました。", this);
            return;
        }

        foreach (Renderer rendererComponent in bobBody.GetComponentsInChildren<Renderer>(true))
        {
            rendererComponent.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            rendererComponent.SetPropertyBlock(block);
        }
    }

    private void LateUpdate()
    {
        if (nameCanvasTransform != null && nameCanvasTransform.gameObject.activeSelf)
            nameCanvasTransform.gameObject.SetActive(false);
    }
}
