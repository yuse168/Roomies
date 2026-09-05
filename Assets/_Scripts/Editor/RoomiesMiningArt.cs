#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

internal static partial class RoomiesArtLibraryBuilder
{
    internal static void BuildMiningArt()
    {
        Directory.CreateDirectory(Root + "/Mining");
        foreach (string key in new[] { "cream", "teal", "coral", "mustard", "ink", "wood", "pink", "green", "silver", "blue", "paper", "screen" })
            Palette[key] = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/{key}.mat");
        Bake("pickaxe", "Mining", () => {
            Cylinder("Worn wooden handle", V(0,0,0), .038f,.85f,"wood");
            for(int i=0;i<4;i++)Cylinder("Grip binding",V(0,-.22f+i*.045f,0),.045f,.026f,"teal");
            Box("Steel eye",V(0,.31f,0),V(.18f,.14f,.14f),"silver");
            Line("Pick",V(.06f,.34f,0),V(.38f,.23f,0),.07f,"silver");
            Line("Adze",V(-.06f,.34f,0),V(-.32f,.26f,0),.09f,"silver");
        },V(.8f,1,.16f),true);
        string[] colors={"cream","ink","coral","silver","blue","mustard","screen","mustard"};
        for(int kind=0;kind<8;kind++)
        {
            int k=kind;
            Bake("ore_"+k,"Mining",()=> {
                Rock("Host stone",V(0,0,0),V(.65f,.5f,.55f),k==1?"ink":"wood",13+k);
                for(int i=0;i<5;i++)
                {
                    float a=i*2.4f;
                    Rock("Mineral crystal",V(Mathf.Cos(a)*.22f,.16f,Mathf.Sin(a)*.18f),V(.23f,.28f,.2f),colors[k],i+4);
                }
            }, k==7?V(1.65f,1.4f,1.55f):k==6?V(.4f,.5f,.4f):V(.6f,.5f,.55f),true);
        }
        Bake("ore_8","Mining",()=>{Can();Box("Ancient dirt",V(.1f,.08f,0),V(.08f,.07f,.07f),"wood");},V(.24f,.4f,.24f),true);
        Bake("ore_9","Mining",()=>{
            Box("Forgotten console",V(0,0,0),V(.4f,.15f,.3f),"cream",.035f);
            Box("Cartridge",V(0,.13f,0),V(.23f,.2f,.06f),"coral");
            Box("Power button",V(-.1f,.08f,.1f),V(.06f,.025f,.04f),"teal");
            Cylinder("Controller pad",V(.15f,.09f,0),.065f,.04f,"ink");
        },V(.48f,.35f,.36f),true);
        Bake("ore_10","Mining",()=>{
            Ellipsoid("Suspicious pottery",V(0,0,0),V(.5f,.56f,.5f),"pink");
            Cylinder("Mouth",V(0,.27f,0),.13f,.12f,"teal");Cylinder("Hollow opening",V(0,.334f,0),.09f,.006f,"ink");
            for(int i=-1;i<=1;i++)Box("Painted face",V(i*.1f,.04f,.247f),V(.04f,.05f,.025f),"ink");
        },V(.5f,.7f,.5f),true);
        for(int tier=1;tier<=3;tier++)
        {
            int t=tier;
            Bake("vein_"+t,"Mining",()=>{
                Rock("Rounded bedrock",V(0,.55f,0),V(2.5f,1.7f,1.6f),t==1?"wood":t==2?"blue":"pink",20+t);
                for(int i=0;i<7;i++)Rock("Mineral seams",V(-.85f+i*.28f,.5f+(i%3)*.22f,-.62f),V(.24f,.35f,.22f),t==1?"coral":t==2?"silver":"mustard",i+7);
            },V(2.5f,1.9f,1.7f));
        }
        Bake("cave_boulder","Mining",()=>Rock("Strata",V(0,0,0),V(3,3,2),"blue",37),V(3,3,2),true);
        Bake("pit_cart","Mining",()=>{
            Box("Bed",V(0,.6f,0),V(1.5f,.16f,2),"ink");
            foreach(int side in new[]{-1,1}){
                Box("Tub side",V(side*.7f,1,0),V(.12f,.75f,2),"teal",.04f);
                Box("Tub end",V(0,1,side*.94f),V(1.5f,.75f,.12f),"teal");
                foreach(float z in new[]{-.65f,.65f})Cylinder("Rail wheel",V(side*.78f,.32f,z),.3f,.12f,"ink",V(0,0,90));
            }
            for(int i=0;i<3;i++)Rock("Old rubble",V(-.35f+i*.33f,.83f,0),V(.55f,.4f,.65f),"wood",i+1);
        });
        Bake("pit_support","Mining",()=>{
            foreach(int side in new[]{-1,1}){
                Box("Timber post",V(side*2.65f,1.85f,0),V(.3f,3.7f,.38f),"wood");
                Box("Steel collar",V(side*2.65f,.65f,0),V(.34f,.18f,.42f),"teal");
                Line("Brace",V(side*2.6f,2.8f,0),V(side*1.8f,3.55f,0),.2f,"wood");
            }
            Box("Lintel",V(0,3.65f,0),V(5.8f,.35f,.4f),"wood");
            Box("Lamp shade",V(0,3.38f,0),V(.7f,.18f,.4f),"teal");
            Box("Lamp glass",V(0,3.28f,0),V(.55f,.03f,.25f),"screen");
        });
        Bake("mine_cashier","Mining",()=>{
            Box("Cabinet",V(0,.7f,0),V(1.8f,1.4f,.8f),"teal",.09f);
            Box("Opening",V(0,1,-.416f),V(1.4f,.48f,.05f),"ink");
            Box("Steel tray",V(0,.73f,-.65f),V(1.5f,.08f,.65f),"silver");
            Box("Price display",V(0,1.46f,-.05f),V(1.4f,.5f,.2f),"ink");
            Box("Screen",V(0,1.47f,-.16f),V(1.2f,.34f,.03f),"screen");
            Cylinder("Big sell button",V(.65f,1.25f,-.45f),.12f,.06f,"mustard",V(90,0,0));
        });
        AssetDatabase.SaveAssets();
    }
    static void Rock(string name, Vector3 p, Vector3 size, string color, int seed)
    {
        const int segments=9,rings=5;
        var random=new System.Random(seed);
        var vertices=new System.Collections.Generic.List<Vector3>();
        var triangles=new System.Collections.Generic.List<int>();
        for(int j=0;j<=rings;j++)for(int i=0;i<segments;i++)
        {
            float phi=Mathf.PI*j/rings, a=2*Mathf.PI*i/segments;
            float r=.85f+(float)random.NextDouble()*.18f;
            vertices.Add(Vector3.Scale(V(Mathf.Sin(phi)*Mathf.Cos(a),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(a)),size*.5f)*r);
        }
        for(int j=0;j<rings;j++)for(int i=0;i<segments;i++)
        {int a=j*segments+i,b=j*segments+(i+1)%segments;triangles.AddRange(new[]{a,b,b+segments,a,b+segments,a+segments});}
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        Part(name,mesh,p,Vector3.zero,color);
    }
}
#endif
