using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local presentation only. Original baked loops; no audio data is generated during gameplay.</summary>
public sealed class RoomiesAudio : MonoBehaviour
{
    static RoomiesAudio instance;
    readonly Dictionary<string,AudioClip> clips=new();
    [Header("Audio sources / swap clips below")]
    [SerializeField] AudioSource musicA,musicB,effects;
    [System.Serializable] public struct Sound { public string id; public AudioClip clip; }
    [SerializeField] Sound[] sounds;
    [SerializeField, Range(0,1)] float musicVolume=.16f;
    [SerializeField, Range(0,1)] float pauseMusicVolume=.09f;
    string currentTrack;
    bool useA;
    float blend=1,hoverAt;
    bool wasNight,wasOver;
    bool hadDay;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()=>instance=null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        if(instance!=null)return;
        var prefab=Resources.Load<GameObject>("RoomiesAudio/Audio rig");
        if(prefab!=null){Instantiate(prefab);return;}
        var go=new GameObject("Roomies Audio");DontDestroyOnLoad(go);instance=go.AddComponent<RoomiesAudio>();
    }
    void Awake()
    {
        if(instance!=null&&instance!=this){Destroy(gameObject);return;}
        instance=this;DontDestroyOnLoad(gameObject);
        if(musicA==null)musicA=gameObject.AddComponent<AudioSource>();
        if(musicB==null)musicB=gameObject.AddComponent<AudioSource>();
        if(effects==null)effects=gameObject.AddComponent<AudioSource>();
        if(sounds!=null)foreach(var sound in sounds)if(!string.IsNullOrEmpty(sound.id)&&sound.clip!=null)clips[sound.id]=sound.clip;
        foreach(var source in new[]{musicA,musicB,effects}){source.playOnAwake=false;source.spatialBlend=0;}
        musicA.loop=musicB.loop=true;
        SharedMoneyManager.SharedMoneyChanged+=MoneyChanged;
    }
    AudioClip Clip(string id)
    {
        if(!clips.TryGetValue(id,out var clip)){clip=Resources.Load<AudioClip>("RoomiesAudio/"+id);clips[id]=clip;}
        return clip;
    }
    public static void Play(string id,float volume=1)
    {
        if(!Application.isPlaying||instance==null)return;
        if(id=="ui_hover"){if(Time.unscaledTime<instance.hoverAt)return;instance.hoverAt=Time.unscaledTime+.075f;}
        var clip=instance.Clip(id);if(clip!=null)instance.effects.PlayOneShot(clip,volume);
    }
    public static void PlayAt(string id,Vector3 position,float volume=.35f)
    {
        if(!Application.isPlaying||instance==null)return;
        var clip=instance.Clip(id);if(clip==null)return;
        var go=new GameObject("Roomies spatial sound");go.transform.position=position;
        var source=go.AddComponent<AudioSource>();source.clip=clip;source.volume=volume;source.spatialBlend=1;
        source.minDistance=1;source.maxDistance=12;source.rolloffMode=AudioRolloffMode.Linear;source.Play();Destroy(go,clip.length+.1f);
    }
    static void MoneyChanged(int before,int after){if(after>before)Play("income",.3f);else if(after<before)Play("purchase",.3f);}
    void Update()
    {
        var day=DayManager.Instance;
        bool night=day!=null&&day.IsNight,over=day!=null&&day.IsGameOver;
        if(day!=null)
        {
            if(hadDay&&night!=wasNight)Play(night?"night_start":"morning",.6f);
            if(over&&!wasOver)Play("game_over",.6f);
        }
        hadDay=day!=null;wasNight=night;wasOver=over;
        string track=over?null:SceneManager.GetActiveScene().name=="GameRoom"?(night?"night_loop":"day_loop"):"menu_loop";
        if(track!=currentTrack)
        {
            currentTrack=track;useA=!useA;var next=useA?musicA:musicB;next.Stop();next.clip=track==null?null:Clip(track);next.volume=0;
            if(next.clip!=null)next.Play();blend=0;
        }
        blend=Mathf.MoveTowards(blend,1,Time.unscaledDeltaTime*.6f);
        float level=EscMenuUI.IsOpen ? pauseMusicVolume : musicVolume;
        var incoming=useA?musicA:musicB;var outgoing=useA?musicB:musicA;
        incoming.volume=blend*level;outgoing.volume=(1-blend)*level;
        if(blend==1&&outgoing.isPlaying)outgoing.Stop();
    }
    void OnDestroy(){SharedMoneyManager.SharedMoneyChanged-=MoneyChanged;if(instance==this)instance=null;}
}
