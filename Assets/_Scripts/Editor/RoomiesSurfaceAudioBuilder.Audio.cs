#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static partial class RoomiesSurfaceAudioBuilder
{
    const int SampleRate=44100;
    const string AudioRoot="Assets/Resources/RoomiesAudio";
    static double Frequency(int midi)=>440*Math.Pow(2,(midi-69)/12d);
    static void Note(float[] samples,double at,double duration,int midi,float gain,int voice=0,float pan=0)
    {
        int frames=samples.Length/2;int start=(int)(at*SampleRate),length=(int)(duration*SampleRate);double f=Frequency(midi);
        for(int i=0;i<length;i++)
        {
            double t=i/(double)SampleRate,phase=t*f*Math.PI*2;
            double env=Math.Min(1,t/.008)*Math.Min(1,(duration-t)/.045);
            double wave;
            if(voice==0){env*=Math.Exp(-t*6);wave=Math.Sin(phase)+.27*Math.Sin(phase*2)+.09*Math.Sin(phase*3);}
            else if(voice==1){env*=Math.Min(1,t/.09)*Math.Min(1,(duration-t)/.25);wave=Math.Sin(phase)*.75+Math.Sin(phase*2.002)*.12;}
            else{env*=Math.Exp(-t*3.5);wave=Math.Sin(phase)*.9+Math.Sin(phase*2)*.15;}
            float value=(float)(gain*env*wave);int index=((start+i)%frames)*2;
            samples[index]+=value*(1-pan*.5f);samples[index+1]+=value*(1+pan*.5f);
        }
    }
    static void Drum(float[] samples,double at,float gain,bool hat=false)
    {
        int frames=samples.Length/2,start=(int)(at*SampleRate),length=(int)(SampleRate*(hat?.07:.23));
        float last=0;
        for(int i=0;i<length;i++)
        {
            float t=i/(float)SampleRate,n=Hash(i,start)*2-1;
            float wave=hat?(n-last)*Mathf.Exp(-t*85):Mathf.Sin(2*Mathf.PI*(48*t+.75f*(1-Mathf.Exp(-t*30))))*Mathf.Exp(-t*23);
            last=n;int index=((start+i)%frames)*2;samples[index]+=wave*gain;samples[index+1]+=wave*gain;
        }
    }
    static void BuildAudio()
    {
        Directory.CreateDirectory(AudioRoot);
        foreach(string id in new[]{"day_loop","night_loop","menu_loop"})
        {
            double beat=60d/(id=="night_loop"?80:id=="menu_loop"?104:96);int bars=16;
            var samples=new float[(int)Math.Round(beat*4*bars*SampleRate)*2];
            int[][] chords={new[]{48,52,55,59},new[]{45,48,52,55},new[]{50,53,57,60},new[]{43,47,50,53}};
            int[] melody={72,76,79,76,74,72,71,67,69,72,76,72,74,71,67,71};
            for(int bar=0;bar<bars;bar++)
            {
                var chord=chords[(bar/2)%4];double at=bar*4*beat;
                foreach(int note in chord)Note(samples,at,beat*3.95,note,.028f,1,(note%3-1)*.4f);
                Note(samples,at,beat*.85,chord[0]-12,.12f,2);Note(samples,at+beat*2,beat*.85,chord[0]-12,.09f,2);
                for(int step=0;step<4;step++)
                {
                    if(id!="night_loop"||step%2==0)Note(samples,at+beat*(step+.5),beat*.75,chord[(step+bar)%4]+12,.067f,0,step%2==0?-.35f:.35f);
                    if(id!="night_loop")Drum(samples,at+beat*step,.065f);
                    Drum(samples,at+beat*(step+.5),id=="night_loop"?.008f:.017f,true);
                }
                if(bar%4!=3)
                {
                    Note(samples,at+beat,beat*1.25,melody[bar],.1f,0,-.1f);
                    Note(samples,at+beat*2.75,beat*1.15,melody[(bar+3)%16],.074f,0,.15f);
                }
            }
            WriteAudio(id,samples,true);
        }
        foreach(string id in new[]{"ui_hover","ui_click","income","purchase","morning","night_start","game_over","step_concrete","step_wood","step_gravel","jump","scan","register","door","pickup"})
        {
            double duration=id=="game_over"?1.6:id is "morning" or "night_start"?1.2:id is "income" or "register"?.65:id=="door"?.4:id=="jump"?.3:.16;
            var samples=new float[(int)(duration*SampleRate)*2];
            if(id.StartsWith("step_"))
            {
                Drum(samples,0,id=="step_wood"?.45f:.28f);
                for(int i=0;i<samples.Length/2;i++)
                {float t=i/(float)SampleRate;float n=(Hash(i,93)*2-1)*Mathf.Exp(-t*40)*.18f;samples[i*2]+=n;samples[i*2+1]+=n;}
            }
            else if(id=="game_over"){Note(samples,0,.65,60,.3f);Note(samples,.4,.65,55,.26f);Note(samples,.8,.75,48,.22f);}
            else if(id is "morning" or "income" or "register")
            {Note(samples,0,.3,72,.2f);Note(samples,.09,.35,76,.16f);Note(samples,.18,duration-.18,79,.19f);}
            else if(id=="night_start"){Note(samples,0,.8,67,.2f);Note(samples,.3,.8,64,.17f);}
            else if(id=="door")
            {Note(samples,0,.3,41,.25f,2);Drum(samples,.27,.35f);}
            else if(id=="jump")
            {
                for(int i=0;i<samples.Length/2;i++)
                {double t=i/(double)SampleRate;float v=(float)(Math.Sin(2*Math.PI*(260*t+400*t*t))*Math.Sin(Math.PI*t/duration)*.22);samples[i*2]=samples[i*2+1]=v;}
            }
            else Note(samples,0,duration,id=="ui_hover"?81:id=="scan"?88:id=="ui_click"?69:id=="pickup"?55:64,.24f);
            WriteAudio(id,samples,false);
        }
    }
    static void WriteAudio(string name,float[] samples,bool loop)
    {
        float peak=samples.Max(s=>Mathf.Abs(s));float gain=loop&&peak>0?.65f/peak:peak>.8f?.8f/peak:1;
        string path=$"{AudioRoot}/{name}.wav";
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(SampleRate);writer.Write(SampleRate*4);writer.Write((short)4);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
            foreach(float sample in samples)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample*gain,-1,1)*32767));
        }
        AssetDatabase.ImportAsset(path);
        var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
        settings.loadType=loop?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
        // PCM keeps exact loop boundaries and avoids codec padding on these small original loops.
        settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
    }
    static void AssignAudio()
    {
        const string path="Assets/_Scenes/GameRoom.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CashRegister>()))
            {r.scanSound=AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot+"/scan.wav");r.paymentSound=AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot+"/register.wav");EditorUtility.SetDirty(r);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
#endif
