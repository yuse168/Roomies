using Unity.Netcode;
using UnityEngine;

public partial class NetworkFurniture
{
    private readonly NetworkVariable<bool> activityOn=new(false);
    private readonly NetworkVariable<int> bestScore=new(0);
    private float nextUse,pulseUntil;
    private Light partyLight;
    private Transform activityVisual;
    private Vector3 activityBaseScale;
    public string InteractionLabel
    {
        get
        {
            var item=FurnitureCatalog.Get(CatalogIndex);if(item==null)return "家具を見る";
            if(item.id=="arcade_machine")return $"タイミングゲーム {TimingScore()}点 / BEST {bestScore.Value}";
            if(item.id=="trampoline")return "ぴょんと跳ぶ";
            if(item.id=="disco_ball")return activityOn.Value?"パーティーを止める":"パーティーを始める";
            return DisplayName+"を使う・効果を見る";
        }
    }
    private int TimingScore()=>NetworkManager==null?0:Mathf.RoundToInt(Mathf.PingPong((float)NetworkManager.ServerTime.Time*.8f,1)*100);

    [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]
    public void UseFurnitureServerRpc(ulong playerId,RpcParams rpcParams=default)
    {
        if(DayManager.GameplayStopped||IsHeld||Time.time<nextUse||
            !TryGetRequestingPlayer(playerId,rpcParams.Receive.SenderClientId,out var player)||!player.IsPlayerObject)return;
        if((GetVisualBounds().ClosestPoint(player.transform.position)-player.transform.position).sqrMagnitude>16)return;
        var job=player.GetComponent<SmugglingPlayer>();if(job!=null&&(job.IsJailed||job.IsControlLocked))return;
        nextUse=Time.time+.7f;
        var item=FurnitureCatalog.Get(CatalogIndex);if(item==null)return;
        if(placed!=null)placed.RecalculateOnRoom();
        string message=FurnitureCatalog.EffectDescription(item.effect,item.effectValue)+
            (placed!=null&&placed.IsEffectActive?" / 部屋に設置して発動中":" / 部屋に置くと翌朝から発動");
        bool jump=false;
        if(item.id=="arcade_machine")
        {
            int score=TimingScore();bestScore.Value=Mathf.Max(bestScore.Value,score);
            message=$"{score}点！ この部屋の最高記録 {bestScore.Value}点";
        }
        else if(item.id=="disco_ball") {activityOn.Value=!activityOn.Value;message=activityOn.Value?"リビングでパーティー！":"パーティー終了";}
        else if(item.id=="trampoline") {jump=true;message="ぴょーん！";}
        else if(item.id=="wall_clock"&&DayManager.Instance!=null)message+=$" / 残り {Mathf.CeilToInt(DayManager.Instance.RemainingSeconds)}秒";
        if(NightEventManager.Instance!=null)NightEventManager.Instance.ServerAnnounceTo(player.OwnerClientId,DisplayName,message,NightEventManager.StyleFun);
        ActivityRpc(playerId,jump);
    }
    [Rpc(SendTo.Everyone,InvokePermission=RpcInvokePermission.Server)]
    private void ActivityRpc(ulong playerId,bool jump)
    {
        pulseUntil=Time.time+.5f;
        if(jump&&NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerId,out var player)&&player.IsOwner)
            player.GetComponent<PlayerMovement>()?.ApplyMiningImpact(Vector3.up*7.5f);
    }
    private void UpdateActivityVisual()
    {
        if(!IsSpawned||!built)return;
        var visual=transform.Find("Visual");if(visual==null)return;
        if(activityVisual!=visual){activityVisual=visual;activityBaseScale=visual.localScale;}
        float pulse=Mathf.Max(0,pulseUntil-Time.time);
        visual.localScale=activityBaseScale*(1+Mathf.Sin(pulse*Mathf.PI*2)*.035f);
        var item=FurnitureCatalog.Get(CatalogIndex);
        if(item==null||item.id!="disco_ball")return;
        if(activityOn.Value)
        {
            visual.localRotation=Quaternion.Euler(0,(float)NetworkManager.ServerTime.Time*55,0);
            if(partyLight==null){var go=new GameObject("Disco glow");go.transform.SetParent(transform,false);partyLight=go.AddComponent<Light>();partyLight.type=LightType.Point;partyLight.range=7;partyLight.shadows=LightShadows.None;}
            partyLight.color=Color.HSVToRGB(Mathf.Repeat((float)NetworkManager.ServerTime.Time*.15f,1),.65f,1);partyLight.intensity=2;
        }
        if(partyLight!=null)partyLight.enabled=activityOn.Value&&!DayManager.GameplayStopped;
    }
}
