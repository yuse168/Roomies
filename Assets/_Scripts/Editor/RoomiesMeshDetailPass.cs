#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class RoomiesMeshDetailPass
{
    const string ScenePath = "Assets/_Scenes/GameRoom.unity";
    const string Folder = "Assets/Resources/RoomiesArt/ArchitectureMeshes";

    [MenuItem("Roomies/Art/Increase Mesh Detail")]
    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save GameRoom changes first.");
        var report = new StringBuilder("Mesh detail pass — baked geometry; no runtime subdivision\n");
        var paths = AssetDatabase.FindAssets("t:Mesh", new[]{"Assets/Resources/RoomiesArt/Meshes"})
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var before = paths.ToDictionary(p=>p,p=>Triangles(AssetDatabase.LoadAssetAtPath<Mesh>(p)));
        RoomiesArtLibraryBuilder.Build();
        RoomiesArtLibraryBuilder.BuildMiningArt();
        long oldTotal=0,newTotal=0;
        foreach(var path in paths)
        {
            long count=Triangles(AssetDatabase.LoadAssetAtPath<Mesh>(path));
            oldTotal+=before[path];newTotal+=count;
            report.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: {before[path]} -> {count} triangles");
            if(count<before[path])throw new InvalidOperationException("Unexpected reduction: "+path);
        }
        report.AppendLine($"Library total: {oldTotal} -> {newTotal} triangles");
        foreach(var path in paths)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            int changed=0;long oldScene=0,newScene=0;
            foreach(var root in scene.GetRootGameObjects())
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var source=mf.sharedMesh;
                if(source==null)continue;
                long count=Triangles(source);oldScene+=count;
                var renderer=mf.GetComponent<MeshRenderer>();
                if(renderer==null||renderer.sharedMaterials.Length!=1||!IsAxisAlignedBox(source)) {newScene+=count;continue;}
                var bounds=source.bounds;
                Vector3 scale=mf.transform.lossyScale;
                scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                Vector3 size=Vector3.Scale(bounds.size,scale);
                float min=Mathf.Min(size.x,Mathf.Min(size.y,size.z));
                if(min<.025f||Mathf.Max(size.x,Mathf.Max(size.y,size.z))>100) {newScene+=count;continue;}
                float bevel=Mathf.Min(.06f,min*.12f);
                string key=Hash128.Compute(bounds.center.ToString("R")+bounds.size.ToString("R")+scale.ToString("R")).ToString();
                string path=$"{Folder}/bevel_{key}.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null)
                {
                    mesh=RoomiesArtLibraryBuilder.RoundedBoxMesh(size,bevel);mesh.name="Roomies bevel";
                    var vertices=mesh.vertices;var normals=mesh.normals;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        vertices[i]=bounds.center+new Vector3(vertices[i].x/scale.x,vertices[i].y/scale.y,vertices[i].z/scale.z);
                        normals[i]=Vector3.Scale(normals[i],scale).normalized;
                    }
                    mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();
                    if((mesh.bounds.center-bounds.center).sqrMagnitude>.00001f||(mesh.bounds.size-bounds.size).sqrMagnitude>.00001f)
                        throw new InvalidOperationException("Bounds changed: "+mf.name);
                    AssetDatabase.CreateAsset(mesh,path);
                }
                Undo.RecordObject(mf,"Smooth building edges");mf.sharedMesh=mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
                changed++;newScene+=Triangles(mesh);
            }
            report.AppendLine($"Architecture box renderers bevelled: {changed}");
            report.AppendLine($"Scene mesh instances after library rebuild: {oldScene} -> {newScene} triangles");
            foreach(var skin in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkinnedMeshRenderer>(true)))
                if(skin.sharedMesh!=null)report.AppendLine($"Existing rig retained: {skin.name}, {Triangles(skin.sharedMesh)} triangles");
            var environment=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Roomies_EnvironmentArt");
            if(environment!=null)PrefabUtility.SaveAsPrefabAssetAndConnect(environment,"Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("ArtReview/2026-09-06");File.WriteAllText("ArtReview/2026-09-06/mesh-detail.txt",report.ToString());
            RoomiesArtAudit.Render(scene,new Vector3(25,4.5f,6),new Vector3(29,1.7f,14),"detail-entrance");
            RoomiesArtAudit.Render(scene,new Vector3(3.5f,-29.6f,68),new Vector3(8,-30.5f,71),"detail-ore");
            RoomiesArtAudit.Render(scene,new Vector3(10,11,8),new Vector3(18,2,18),"detail-house");
            Debug.Log($"[Mesh Detail] {paths.Length} library meshes rebuilt; {changed} architectural boxes bevelled.");
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    static long Triangles(Mesh mesh)
    {long count=0;if(mesh!=null)for(int i=0;i<mesh.subMeshCount;i++)count+=(long)mesh.GetIndexCount(i)/3;return count;}
    static bool IsAxisAlignedBox(Mesh mesh)
    {
        if(!mesh.isReadable||mesh.vertexCount>24||mesh.subMeshCount!=1||mesh.GetIndexCount(0)!=36)return false;
        var b=mesh.bounds;
        foreach(var v in mesh.vertices)
            for(int axis=0;axis<3;axis++)
                if(Mathf.Abs(v[axis]-b.min[axis])>.0001f&&Mathf.Abs(v[axis]-b.max[axis])>.0001f)return false;
        return true;
    }

    [MenuItem("Roomies/Art/Refresh Detail Mesh Previews")]
    static void RefreshPreviews()
    {
        RoomiesArtLibraryBuilder.Build();
        RoomiesArtLibraryBuilder.BuildMiningArt();
        AssetDatabase.SaveAssets();
        foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/Resources/RoomiesArt/Meshes"}))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            RoomiesArtAudit.Render(scene,new Vector3(25,4.5f,6),new Vector3(29,1.7f,14),"detail-entrance");
            RoomiesArtAudit.Render(scene,new Vector3(3.5f,-29.6f,68),new Vector3(8,-30.5f,71),"detail-ore");
            var audit=new StringBuilder();
            foreach(var name in new[]{"mine_cashier","vein_3","cave_boulder"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/RoomiesArt/Mining/{name}.prefab");
                audit.AppendLine(name+": "+string.Join(",",prefab.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>m.name)));
            }
            File.WriteAllText("Temp/RoomiesArt/detail-materials.txt",audit.ToString());
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
#endif
