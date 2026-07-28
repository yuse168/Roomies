#if UNITY_EDITOR
using Unity.AI.MCP.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Roomies_saveを開いたとき、Unity AI AssistantのMCP Bridgeを自動起動する。
/// Codex側は .codex/config.toml からUnity Relayへ接続する。
/// </summary>
[InitializeOnLoad]
public static class UnityMcpAutoStart
{
    static UnityMcpAutoStart()
    {
        EditorApplication.delayCall += EnsureBridgeRunning;
    }

    [MenuItem("Tools/Codex/Start Unity MCP _F12")]
    public static void EnsureBridgeRunning()
    {
        if (UnityMCPBridge.IsRunning) return;

        // パッケージ初期化順によって、Enabled=trueでも内部Bridgeが未生成の
        // 状態になることがある。一度無効化して確実にインスタンスを作り直す。
        UnityMCPBridge.Enabled = false;
        UnityMCPBridge.Enabled = true;
        UnityMCPBridge.Start();
        EditorApplication.delayCall += LogBridgeStatus;
    }

    private static void LogBridgeStatus()
    {
        Debug.Log($"[Unity MCP] Bridge自動起動: running={UnityMCPBridge.IsRunning}");
    }
}
#endif
