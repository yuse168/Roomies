using UnityEngine;
using Unity.Netcode;

public class DebugAutoHost : MonoBehaviour
{
    [Header("デバッグ用")]
    public bool autoStartHost = true;

    void Start()
    {
        if (!autoStartHost) return;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManagerがシーンにありません");
            return;
        }

        if (NetworkManager.Singleton.IsListening) return;

        NetworkManager.Singleton.StartHost();

        Debug.Log("Debug Auto Host Started");
    }
}