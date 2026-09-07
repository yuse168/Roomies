using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A short-lived operation on the existing NetworkManager; keeps the Steam lobby/transport.</summary>
public sealed class SessionRetryFlow : MonoBehaviour
{
    public static bool IsRestarting { get; private set; }
    public static string LastError { get; private set; }
    private NetworkManager network;
    private double deadline;
    private NetworkObject[] previousObjects;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { IsRestarting=false; LastError=null; }

    public static void Begin()
    {
        var nm=NetworkManager.Singleton;
        if(IsRestarting || nm==null || !nm.IsHost || DayManager.Instance==null || !DayManager.Instance.IsGameOver) return;
        if(nm.NetworkConfig.PlayerPrefab==null||!nm.NetworkConfig.EnableSceneManagement||nm.SceneManager==null) {LastError="再開に必要なネットワーク設定が不足しています";return;}
        var flow=nm.GetComponent<SessionRetryFlow>();
        if(flow==null)flow=nm.gameObject.AddComponent<SessionRetryFlow>();
        flow.network=nm;flow.deadline=Time.realtimeSinceStartupAsDouble+nm.NetworkConfig.LoadSceneTimeOut+15;
        flow.previousObjects=nm.SpawnManager.SpawnedObjectsList.Where(o=>!o.IsPlayerObject && o.gameObject.scene.name=="GameRoom").ToArray();
        IsRestarting=true;LastError=null;
        nm.SceneManager.OnLoadEventCompleted+=flow.Loaded;
        var result=nm.SceneManager.LoadScene("GameRoom",LoadSceneMode.Single);
        if(result!=SceneEventProgressStatus.Started)flow.Finish("再開できませんでした: "+result);
    }
    private void Loaded(string sceneName,LoadSceneMode mode,List<ulong> completed,List<ulong> timedOut)
    {
        if(sceneName!="GameRoom")return;
        // Destroy any dynamically spawned gameplay objects NGO carried into the new scene.
        foreach(var obj in previousObjects)if(obj!=null&&obj.IsSpawned)obj.Despawn();
        previousObjects=null;
        foreach(var id in timedOut)if(id!=network.LocalClientId)network.DisconnectClient(id);
        // NGO may preserve player objects during scene replacement. Recreate them to reset all job states.
        PlayerSpawnSync.ResetIndex();
        foreach(var client in network.ConnectedClientsList.ToArray())
        {
            if(timedOut.Contains(client.ClientId))continue;
            if(client.PlayerObject!=null&&client.PlayerObject.IsSpawned)client.PlayerObject.Despawn();
            var player=Instantiate(network.NetworkConfig.PlayerPrefab);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(client.ClientId);
        }
        DayManager.Instance?.SnapshotDayStartEarnings();
        Finish(null);
    }
    private void Update()
    {
        if(!IsRestarting)return;
        if(network==null||!network.IsListening)Finish(null);
        else if(Time.realtimeSinceStartupAsDouble>deadline)Finish("再読み込みがタイムアウトしました。メニューへ戻って再接続してください。");
    }
    private void Finish(string error)
    {
        if(network!=null&&network.SceneManager!=null)network.SceneManager.OnLoadEventCompleted-=Loaded;
        previousObjects=null;IsRestarting=false;LastError=error;
    }
    private void OnDestroy()=>Finish(null);
}
