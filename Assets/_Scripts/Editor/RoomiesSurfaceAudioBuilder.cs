#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

internal static partial class RoomiesSurfaceAudioBuilder
{
    const string Root="Assets/Resources/RoomiesArt";
    static readonly Dictionary<string,Texture2D> textures=new();
    static readonly HashSet<Material> touched=new();
    static readonly HashSet<Mesh> mapped=new();
    static int converted;
    static readonly string[] Patterns={"paint","plaster","woodgrain","cloth","metal","asphalt","tile","rock","leaves","paper","screen","rubber","grass"};
    [MenuItem("Roomies/Art/Build Textures and Soundtrack")]
    static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits before authoring.");
        Directory.CreateDirectory(Root+"/Textures");Directory.CreateDirectory(Root+"/SurfaceMaterials");Directory.CreateDirectory(Root+"/SurfaceMeshes");
        touched.Clear();mapped.Clear();textures.Clear();converted=0;
        foreach(string pattern in Patterns)CreateTexture(pattern);
        // Shared palette also covers runtime-created props and purchased furniture.
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(Eligible(mat))TextureMaterial(mat,Pattern(mat.name));
        }
        var prefabPaths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Prefabs",Root})
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
        foreach(string path in prefabPaths)
        {
            var go=PrefabUtility.LoadPrefabContents(path);
            try{if(Process(go))PrefabUtility.SaveAsPrefabAsset(go,path);}
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        foreach(string path in new[]{"Assets/_Scenes/GameRoom.unity","Assets/_Scenes/MainMenuSteam.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                foreach(var go in scene.GetRootGameObjects())Process(go);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        BuildAudio();AssignAudio();AssetDatabase.SaveAssets();
        Directory.CreateDirectory("ArtReview/2026-09-06");
        File.WriteAllText("ArtReview/2026-09-06/surface-audio-build.txt",$"Patterns: {Patterns.Length} albedo + {Patterns.Length} normal maps\nTextured materials: {touched.Count}\nUV meshes: {mapped.Count}\nRenderer material assignments: {converted}\nOriginal audio tracks generated and register/mining clips wired.\n");
        Debug.Log($"[Surfaces] {touched.Count} materials textured, {mapped.Count} meshes mapped, {converted} renderer assignments.");
    }
    static bool Eligible(Material m)=>m!=null&&(m.shader.name.Contains("/Lit")||m.shader.name.Contains("Simple Lit")||m.shader.name.Contains("SimpleLit")||m.shader.name=="Standard");
    static string Pattern(string name)
    {
        name=name.ToLowerInvariant();
        if(name.Contains("wood")||name.Contains("oak"))return "woodgrain";
        if(name.Contains("silver")||name.Contains("metal"))return "metal";
        if(name.Contains("green"))return "leaves";
        if(name.Contains("paper"))return "paper";
        if(name.Contains("screen"))return "screen";
        if(name.Contains("cream")||name.Contains("wall"))return "plaster";
        if(name.Contains("pink")||name.Contains("cloth")||name.Contains("body"))return "cloth";
        return "paint";
    }
    static string ContextPattern(Renderer renderer,string fallback)
    {
        string n=renderer.name.ToLowerInvariant();
        if(n.Contains("vein")||renderer.transform.position.y< -5&&(n=="solid floor"||n=="end wall"))return "rock";
        if(n=="pplane1"||n.Contains("ground")||n.Contains("embankment"))return "grass";
        if(n.Contains("road")||n.Contains("pCube8".ToLowerInvariant()))return "asphalt";
        if(n.Contains("tiled")||n.Contains("paving")||n=="sidewalk")return "tile";
        if(n.Contains("rock")||n.Contains("boulder")||n.Contains("ore")||n.Contains("cave"))return "rock";
        if(n.Contains("sofa")||n.Contains("bed")||n.Contains("rug")||n.Contains("body"))return "cloth";
        return fallback;
    }
    static bool Process(GameObject root)
    {
        bool changed=false;
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if(renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)continue;
            var mats=renderer.sharedMaterials;bool materialChanged=false;
            for(int i=0;i<mats.Length;i++)
            {
                var mat=mats[i];if(!Eligible(mat))continue;
                string property=mat.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                var oldTexture=mat.GetTexture(property);
                if(oldTexture!=null&&!AssetDatabase.GetAssetPath(oldTexture).StartsWith(Root+"/Textures"))continue;
                string pattern=ContextPattern(renderer,Pattern(mat.name));
                string path=AssetDatabase.GetAssetPath(mat);
                bool variant=!path.EndsWith(".mat")||pattern!=Pattern(mat.name);
                if(variant&&!path.StartsWith(Root+"/SurfaceMaterials"))
                {
                    string key=Hash128.Compute(path+mat.name+pattern).ToString();
                    string target=$"{Root}/SurfaceMaterials/{pattern}_{key}.mat";
                    var copy=AssetDatabase.LoadAssetAtPath<Material>(target);
                    if(copy==null){copy=new Material(mat);copy.name=pattern+" "+mat.name;AssetDatabase.CreateAsset(copy,target);}
                    mats[i]=mat=copy;materialChanged=true;
                }
                TextureMaterial(mat,pattern);
            }
            if(materialChanged){renderer.sharedMaterials=mats;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);changed=true;converted++;}
            var filter=renderer.GetComponent<MeshFilter>();
            Mesh mesh=filter!=null?filter.sharedMesh:(renderer as SkinnedMeshRenderer)?.sharedMesh;
            bool worldGround=filter!=null&&(renderer.name.StartsWith("pCube")||renderer.name=="pPlane1");
            if(mesh==null||!mesh.isReadable||!worldGround&&mesh.uv.Length==mesh.vertexCount&&mesh.uv.Any(u=>u.sqrMagnitude>.0001f))continue;
            string meshPath=AssetDatabase.GetAssetPath(mesh);
            if(!meshPath.EndsWith(".asset"))
            {
                string target=$"{Root}/SurfaceMeshes/{Hash128.Compute(meshPath+mesh.name)}.asset";
                var copy=AssetDatabase.LoadAssetAtPath<Mesh>(target);
                if(copy==null){copy=Object.Instantiate(mesh);AssetDatabase.CreateAsset(copy,target);}
                mesh=copy;
                if(filter!=null)filter.sharedMesh=mesh;else ((SkinnedMeshRenderer)renderer).sharedMesh=mesh;
                changed=true;
            }
            if(mapped.Add(mesh))
            {
                var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
                Vector3 s=worldGround||meshPath.Contains("ArchitectureMeshes")?renderer.transform.lossyScale:Vector3.one;
                for(int i=0;i<uv.Length;i++)
                {
                    var p=Vector3.Scale(vertices[i],s);var n=normals.Length==vertices.Length?normals[i]:Vector3.up;
                    var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
                    uv[i]=(a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.8f;
                }
                mesh.uv=uv;mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
            }
        }
        return changed;
    }
    static void TextureMaterial(Material mat,string pattern)
    {
        string prop=mat.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
        mat.SetTexture(prop,textures[pattern]);
        if(mat.HasProperty("_BumpMap")){mat.SetTexture("_BumpMap",textures[pattern+"_normal"]);mat.SetFloat("_BumpScale",.25f);mat.EnableKeyword("_NORMALMAP");}
        EditorUtility.SetDirty(mat);touched.Add(mat);
    }
    static float Hash(int x,int y){unchecked{uint h=(uint)(x*374761393+y*668265263+17);h=(h^(h>>13))*1274126177;h^=h>>16;return (h&65535)/65535f;}}
    static float Wave(float t)=>.5f+.5f*Mathf.Sin(t*Mathf.PI*2);
    static void CreateTexture(string pattern)
    {
        const int N=512;var image=new Texture2D(N,N,TextureFormat.RGB24,false);var normal=new Texture2D(N,N,TextureFormat.RGB24,false,true);
        var colors=new Color[N*N];var height=new float[N*N];
        for(int y=0;y<N;y++)for(int x=0;x<N;x++)
        {
            float u=x/(float)N,v=y/(float)N,noise=Hash(x,y),value=.91f+(noise-.5f)*.06f;
            switch(pattern)
            {
                case "woodgrain": value=.79f+.14f*Wave(u*22+2*Wave(v*2)+.4f*Wave(v*7))+.045f*noise;if(x%128<3||y%256<2)value=.48f;break;
                case "cloth": value=.81f+.06f*(x%4<2?1:0)+.06f*(y%4<2?1:0)+.035f*noise;break;
                case "metal": value=.86f+.08f*Hash(y,7)+.03f*noise;break;
                case "asphalt": value=.7f+.2f*noise;if(noise>.97f)value=1;break;
                case "tile": value=x%128<4||y%128<4?.54f:.9f+.025f*noise;break;
                case "rock": value=.74f+.1f*Wave(u*7+Wave(v*3))+.07f*Wave(v*9+Wave(u*4))+.09f*noise;break;
                case "leaves": value=.78f+.12f*Wave(u*17+Wave(v*11))+.08f*noise;break;
                case "grass": value=.76f+.13f*noise+.055f*Wave(u*31+Wave(v*19));break;
                case "paper": value=.93f+.05f*noise;break;
                case "screen": value=y%8<2?.68f:.95f;break;
                case "rubber": value=.78f+.09f*noise;break;
                case "plaster": value=.84f+.1f*noise+.03f*Wave(u*8+Wave(v*5));break;
            }
            colors[y*N+x]=new Color(value,value,value);height[y*N+x]=value;
        }
        image.SetPixels(colors);image.Apply();
        for(int y=0;y<N;y++)for(int x=0;x<N;x++)
        {
            float dx=height[y*N+(x+1)%N]-height[y*N+(x+N-1)%N];
            float dy=height[((y+1)%N)*N+x]-height[((y+N-1)%N)*N+x];
            var n=new Vector3(-dx*1.5f,-dy*1.5f,1).normalized;colors[y*N+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
        }
        normal.SetPixels(colors);normal.Apply();
        foreach(var pair in new[]{(pattern,image),(pattern+"_normal",normal)})
        {
            string path=$"{Root}/Textures/{pair.Item1}.png";File.WriteAllBytes(path,pair.Item2.EncodeToPNG());AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.maxTextureSize=512;
            if(pair.Item1.EndsWith("_normal")){importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;}
            importer.SaveAndReimport();textures[pair.Item1]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        Object.DestroyImmediate(image);Object.DestroyImmediate(normal);
    }
}
#endif
