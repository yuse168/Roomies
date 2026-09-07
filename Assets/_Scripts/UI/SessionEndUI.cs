using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SessionEndUI : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text summary,status;
    [SerializeField] private Button retry;
    [SerializeField] private Button menu;
    private bool leaving;
#if UNITY_EDITOR
    public Canvas BuildPreview()
    {
        BuildUI();canvas.enabled=true;
        summary.text="DAY 6 まで生活した\n共同口座 ¥420 / 必要な家賃 ¥650\nあなたの稼ぎ記録 ¥1,280";
        status.text="再挑戦すると資金・家具・仕事の状態をリセットします";
        return canvas;
    }
#endif
    private void Start()
    {
        BuildUI();
        retry.onClick.AddListener(Retry);
        menu.onClick.AddListener(ReturnToMenu);
        canvas.enabled=false;
    }
    void Retry() => SessionRetryFlow.Begin();
    void ReturnToMenu() { if (!leaving) StartCoroutine(Leave()); }
    [ContextMenu("未配置のUIを作成")]
    public void BuildUI()
    {
        if (canvas != null) return;
        canvas=UITheme.CreateCanvas(transform,"Session result",32600);
        canvas.GetComponent<CanvasScaler>().screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        var dim=new GameObject("Dim",typeof(RectTransform),typeof(Image));dim.transform.SetParent(canvas.transform,false);
        var dr=(RectTransform)dim.transform;dr.anchorMin=Vector2.zero;dr.anchorMax=Vector2.one;dr.offsetMin=dr.offsetMax=Vector2.zero;
        dim.GetComponent<Image>().color=new Color(.025f,.05f,.07f,.9f);
        var panel=UITheme.Surface(canvas.transform,"Result card",24,2);panel.rectTransform.sizeDelta=new Vector2(900,650);
        Label(panel.transform,"家賃が払えなかった！",42,230,new Vector2(800,70));
        summary=Label(panel.transform,"",27,85,new Vector2(780,200));
        retry=Button(panel.transform,"みんなで DAY 1 から再挑戦",-100,null);
        menu=Button(panel.transform,"メインメニューへ",-190,null);
        status=Label(panel.transform,"",19,-270,new Vector2(800,54));
        canvas.enabled=false;
    }
    private void LateUpdate()
    {
        if(canvas==null)return;
        var day=DayManager.Instance;
        bool show=day!=null&&day.IsGameOver;
        canvas.enabled=show;
        if(!show)return;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        var nm=NetworkManager.Singleton;
        int money=SharedMoneyManager.Instance!=null?SharedMoneyManager.Instance.CurrentMoney:0;
        int mine=nm?.LocalClient?.PlayerObject?.GetComponent<PlayerEarning>()?.GetEarning()??0;
        summary.text=$"DAY {day.DebugDay} まで生活した\n共同口座 ¥{money:N0} / 必要な家賃 ¥{day.CurrentRentTotal:N0}\nあなたの稼ぎ記録 ¥{mine:N0}";
        retry.interactable=nm!=null&&nm.IsHost&&!SessionRetryFlow.IsRestarting&&!leaving;
        status.text=SessionRetryFlow.LastError??(SessionRetryFlow.IsRestarting?"全員の準備を待っています…":nm!=null&&nm.IsHost?"再挑戦すると資金・家具・仕事の状態をリセットします":"ホストが再挑戦を選ぶと、全員で再開します");
    }
    private IEnumerator Leave()
    {
        leaving=true;
        var lobby=FindAnyObjectByType<SteamLobby>();if(lobby!=null)lobby.LeaveLobby();
        var nm=NetworkManager.Singleton;if(nm!=null&&nm.IsListening)nm.Shutdown();
        while(nm!=null&&(nm.IsListening||nm.ShutdownInProgress))yield return null;
        SceneManager.LoadScene("MainMenuSteam");
    }
    static TMP_Text Label(Transform parent,string text,float size,float y,Vector2 dimensions)
    {var l=UITheme.Label(parent,"Label",text,size,Color.white,TextAlignmentOptions.Center,true);l.rectTransform.sizeDelta=dimensions;l.rectTransform.anchoredPosition=new Vector2(0,y);return l;}
    static Button Button(Transform parent,string text,float y,UnityEngine.Events.UnityAction action)
    {var image=UITheme.Surface(parent,text,16,1);image.rectTransform.sizeDelta=new Vector2(660,70);image.rectTransform.anchoredPosition=new Vector2(0,y);image.color=UITheme.MenuInkSoft;var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;if(action!=null)b.onClick.AddListener(action);Label(image.transform,text,25,0,new Vector2(630,60));return b;}
}
