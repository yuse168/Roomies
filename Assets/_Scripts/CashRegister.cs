using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>One server-owned checkout, with a clerk lease and once-only wages per customer.</summary>
public sealed class CashRegister : NetworkBehaviour
{
    public TMP_Text display;
    public TMP_Text[] choiceLabels;
    public Transform customer;
    public Transform[] goods;
    public Transform clerkPoint;
    public AudioSource sound;
    public AudioClip scanSound,paymentSound;
    private readonly NetworkVariable<ulong> clerk = new(ulong.MaxValue);
    private readonly NetworkVariable<int> order = new(0), scanned = new(0), combo = new(0);
    private readonly NetworkVariable<int> phase = new(0); // idle, scanning, payment, next customer
    private readonly NetworkVariable<double> expires = new(0);
    private float nextAction;
    private float nextVisual;
    private static readonly string[] Names = { "おにぎり", "ミルク", "サンドイッチ", "ジュース", "お弁当", "おやつ" };
    private static readonly int[] Prices = {120,180,240,150,350,100};
    public int OrderNumber => order.Value;
    public bool IsOpen => DayManager.Instance != null && !DayManager.Instance.IsNight && !DayManager.GameplayStopped;
    public static int Product(int orderNumber,int index) => (int)(((long)orderNumber*7+index*5)%Prices.Length);
    public static int Total(int orderNumber) => Prices[Product(orderNumber,0)]+Prices[Product(orderNumber,1)]+Prices[Product(orderNumber,2)];
    public static int Change(int orderNumber,int choice) => 1000-Total(orderNumber)+(choice-orderNumber%3)*50;
    public override void OnNetworkSpawn(){scanned.OnValueChanged+=OnScanned;phase.OnValueChanged+=OnPhase;RefreshVisual();}
    public override void OnNetworkDespawn(){scanned.OnValueChanged-=OnScanned;phase.OnValueChanged-=OnPhase;}
    void OnScanned(int before,int after){if(after>before&&sound!=null&&scanSound!=null)sound.PlayOneShot(scanSound,.45f);}
    void OnPhase(int before,int after){if(before==2&&after==3&&sound!=null&&paymentSound!=null)sound.PlayOneShot(paymentSound,.6f);}
    public string ActionLabel(int action)
    {
        if(!IsOpen)return "レジ打ちは朝から日没まで";
        if(phase.Value==0)return "レジ打ちを始める / 離れると交代";
        if(clerk.Value!=NetworkManager.LocalClientId)return "ほかの人が接客中";
        if(phase.Value==3)return "次のお客さんを待っています";
        if(phase.Value==1)return action<0?$"{Names[Product(order.Value,scanned.Value)]} をスキャン":"先に商品をスキャン";
        return action<0?"お釣りの金額を選ぼう":$"お釣り ¥{Change(order.Value,action)} を渡す";
    }
    [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)]
    public void UseRpc(ulong playerId,int action,int expectedOrder,RpcParams rpc=default)
    {
        if(!IsOpen||action < -1||action>2||Time.time<nextAction||expectedOrder!=order.Value)return;
        if((phase.Value==1||phase.Value==2)&&NetworkManager.ServerTime.Time>=expires.Value)return;
        if(!NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId,out var client)||client.PlayerObject==null||client.PlayerObject.NetworkObjectId!=playerId)return;
        var player=client.PlayerObject;
        if(!Available(player)||Vector3.Distance(player.transform.position,clerkPoint.position)>3.3f)return;
        // Server held-state, not the owner's local interaction cache.
        foreach(var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if(obj.TryGetComponent<CarryableObject>(out var carry)&&carry.IsHeldBy(player.OwnerClientId))return;
            if(obj.TryGetComponent<NetworkFurniture>(out var furniture)&&furniture.IsHeldBy(player.OwnerClientId))return;
        }
        if(phase.Value==0)
        {
            // A player may work at only one checkout.
            foreach(var other in FindObjectsByType<CashRegister>())
                if(other!=this&&other.IsSpawned&&other.clerk.Value==client.ClientId)return;
            clerk.Value=client.ClientId;combo.Value=0;BeginOrder();nextAction=Time.time+.6f;return;
        }
        if(clerk.Value!=client.ClientId)return;
        nextAction=Time.time+.6f;
        if(phase.Value==1&&action==-1)
        {
            scanned.Value++;if(scanned.Value==3)phase.Value=2;
        }
        else if(phase.Value==2&&action>=0)
        {
            if(Change(order.Value,action)!=1000-Total(order.Value))
            {combo.Value=0;expires.Value-=4;Notice(client.ClientId,"お釣りが違うよ","¥1,000 − 合計金額 を計算しよう（残り時間 -4秒）");return;}
            int wage=FurnitureEffectManager.Instance.JobReward(35+Mathf.Min(combo.Value,5)*5);
            if(SharedMoneyManager.Instance==null||!SharedMoneyManager.Instance.TryAdd(wage,SharedMoneyReason.CashierWage,$"Checkout {NetworkObjectId}, order {order.Value}"))return;
            phase.Value=3;expires.Value=NetworkManager.ServerTime.Time+3;combo.Value++;
            player.GetComponent<PlayerEarning>()?.AddEarning(wage);
            Notice(client.ClientId,$"接客完了！ +¥{wage}",$"共同口座に入金 / {combo.Value}人連続で正解！");
        }
    }
    static bool Available(NetworkObject player)
    {var job=player.GetComponent<SmugglingPlayer>();return job==null||(!job.IsJailed&&!job.IsControlLocked);}
    void BeginOrder(){order.Value=order.Value==int.MaxValue?1:order.Value+1;scanned.Value=0;phase.Value=1;expires.Value=NetworkManager.ServerTime.Time+40;}
    static void Notice(ulong id,string title,string body)=>NightEventManager.Instance?.ServerAnnounceTo(id,title,body,NightEventManager.StyleFun);
    void Update()
    {
        if(!IsSpawned)return;
        if(IsServer&&phase.Value!=0)
        {
            if(!IsOpen||!NetworkManager.ConnectedClients.TryGetValue(clerk.Value,out var client)||client.PlayerObject==null||!Available(client.PlayerObject)||Vector3.Distance(client.PlayerObject.transform.position,clerkPoint.position)>4)
            {phase.Value=0;clerk.Value=ulong.MaxValue;combo.Value=0;}
            else if(NetworkManager.ServerTime.Time>=expires.Value)
            {
                if(phase.Value!=3){combo.Value=0;Notice(clerk.Value,"お客さんが帰ってしまった","次のお客さんで再挑戦！");}
                BeginOrder();
            }
        }
        if(Time.unscaledTime>=nextVisual){nextVisual=Time.unscaledTime+.1f;RefreshVisual();}
    }
    public void RefreshVisual()
    {
        if(display!=null)
        {
            string text=!IsSpawned||phase.Value==0?"レジ打ち / CASHIER\n商品3点 → お釣りを渡す\n報酬 ¥35〜 / 連続正解でUP\nスキャナーに E で勤務開始":phase.Value==3?$"ありがとうございました！\n連続 {combo.Value}人 正解\n次のお客様が来ます…":$"{(phase.Value==1?"商品をスキャン":"お釣りを選んでください")}  {scanned.Value}/3\n";
            if(IsSpawned&&phase.Value is 1 or 2)
            {
                for(int i=0;i<3;i++)text+=$"{(i<scanned.Value?"✓":"・")} {Names[Product(order.Value,i)]}  ¥{Prices[Product(order.Value,i)]}\n";
                text+=$"合計 ¥{Total(order.Value)}  / お預かり ¥1,000\n残り {Mathf.Max(0,Mathf.CeilToInt((float)(expires.Value-NetworkManager.ServerTime.Time)))}秒";
            }
            if(IsSpawned&&!IsOpen)text="CLOSED\n営業は朝から日没まで";
            display.text=text;
        }
        for(int i=0;i<(choiceLabels?.Length??0);i++)if(choiceLabels[i]!=null)choiceLabels[i].text=IsSpawned&&phase.Value==2?$"¥{Change(order.Value,i)}":"—";
        if(customer!=null)
        {
            customer.gameObject.SetActive(IsSpawned&&phase.Value!=0&&IsOpen);
            if(IsSpawned)customer.localPosition=new Vector3(0,0,phase.Value==3?Mathf.Clamp(3-(float)(expires.Value-NetworkManager.ServerTime.Time),0,3):0);
        }
        for(int i=0;i<(goods?.Length??0);i++)if(goods[i]!=null)goods[i].gameObject.SetActive(!IsSpawned||phase.Value==1&&i>=scanned.Value);
    }
}
