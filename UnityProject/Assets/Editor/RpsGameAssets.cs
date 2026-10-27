using System;
using System.IO;
using System.Linq;
using CrazyMinnow.SALSA;
using CrazyMinnow.SALSA.OneClicks;
using RpsArena.Game;
using RpsArena.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RpsSalsaSetup : OneClickBase
{
    public static void Configure(GameObject actor)
    {
        Init(); NewConfiguration(OneClickConfiguration.ConfigType.Emoter); AddSmrSearch("^.*$");
        useRandomEmotes=false; isChancePerEmote=true; numRandomEmotesPerCycle=0;
        foreach(var name in new[]{"Smile","Frown","Surprise","Blink"})
        {
            NewExpression(name); AddEmoteFlags(false,false,false,1);
            AddShapeComponent(new[]{name},name=="Blink"?.06f:.22f,.1f,name=="Blink"?.09f:.3f,name,1,false);
        }
        DoOneClickiness(actor); AddQueueProcessor(actor);
        actor.AddComponent<RpsExpressions>();
    }
}

public static class RpsGameAssets
{
    const string Generated="Assets/GameArt";
    [MenuItem("RPS/Build Playable Game Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Generated); Directory.CreateDirectory("Assets/Resources"); AssetDatabase.Refresh();
        var catalog=AssetDatabase.LoadAssetAtPath<RpsCharacterCatalog>("Assets/Resources/RpsCharacterCatalog.asset");
        if(!catalog) { catalog=ScriptableObject.CreateInstance<RpsCharacterCatalog>(); AssetDatabase.CreateAsset(catalog,"Assets/Resources/RpsCharacterCatalog.asset"); }
        catalog.male=Character("Male"); catalog.female=Character("Female");
        catalog.stageMaterial=Material("Stage",new Color(.055f,.09f,.145f),false);
        catalog.darkMaterial=Material("Podium",new Color(.025f,.04f,.065f),false);
        catalog.cyanMaterial=Material("Cyan",new Color(.12f,.83f,.72f),true);
        catalog.coralMaterial=Material("Coral",new Color(.94f,.25f,.2f),true);
        EditorUtility.SetDirty(catalog);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
        PlayerSettings.companyName="Dingous"; PlayerSettings.productName="Duel Arena";
        PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=850;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.runInBackground=true;
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
        AssetDatabase.SaveAssets(); Validate(); Debug.Log("RPS_GAME_ASSETS_READY");
    }
    private static GameObject Character(string sex)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/{sex}/Rps{sex}.prefab");
        var actor=(GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=UnityEngine.Object.Instantiate(skin.sharedMesh); mesh.name="Rps"+sex+"Face";
                var verts=mesh.vertices;
                var head=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
                Debug.Log($"RPS_FACE {sex} head={head.position:F4} bounds={skin.bounds}");
                // Coordinates are in character space, independent of the FBX's centimetre units.
                var weights=mesh.boneWeights; var binds=mesh.bindposes;
                var boneMatrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*binds[i]).ToArray();
                var matrices=new Matrix4x4[verts.Length]; var world=new Vector3[verts.Length];
                for(int i=0;i<verts.Length;i++)
                {
                    var w=weights[i]; var m=new Matrix4x4();
                    for(int k=0;k<16;k++) m[k]=boneMatrices[w.boneIndex0][k]*w.weight0+boneMatrices[w.boneIndex1][k]*w.weight1+boneMatrices[w.boneIndex2][k]*w.weight2+boneMatrices[w.boneIndex3][k]*w.weight3;
                    matrices[i]=m.inverse; world[i]=m.MultiplyPoint3x4(verts[i]);
                }
                float top=world.Max(v=>v.y), headY=head.position.y;
                Debug.Log($"RPS_FACE {sex} top={top:F4} headY={headY:F4}");
                float eyeY=sex=="Male"?1.557f:1.517f;
                float mouthY=sex=="Male"?1.495f:1.448f;
                float faceFront=world.Where(v=>Mathf.Abs(v.x)<.065f && Mathf.Abs(v.y-eyeY)<.03f).Max(v=>v.z);
                foreach(var shape in new[]{"Smile","Frown","Surprise","Blink"})
                {
                    var delta=new Vector3[verts.Length]; int moved=0;
                    for(int i=0;i<verts.Length;i++)
                    {
                        var p=world[i];
                        float front=Mathf.SmoothStep(0,1,Mathf.InverseLerp(faceFront-.09f,faceFront-.035f,p.z));
                        float x=p.x+.0036f;
                        float mouth=Gaussian(x/.07f)*Gaussian((p.y-mouthY)/.016f)*front;
                        float corners=Gaussian((Mathf.Abs(x)-.036f)/.02f);
                        Vector3 d=Vector3.zero;
                        if(shape=="Smile") { d.y=.009f*mouth*corners; d.x=Mathf.Sign(x)*.005f*mouth*corners; }
                        if(shape=="Frown") { d.y=-.017f*mouth*corners; }
                        if(shape=="Surprise")
                        {
                            d.y=(p.y<mouthY?-.004f:.001f)*mouth; d.x=-x*.12f*mouth;
                            d.y+=.006f*Gaussian((Mathf.Abs(x)-.04f)/.035f)*Gaussian((p.y-eyeY-.037f)/.017f)*front;
                        }
                        if(shape=="Blink")
                        {
                            float eye=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.023f,.039f,Mathf.Abs(Mathf.Abs(x)-.039f))))
                                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.018f,.031f,Mathf.Abs(p.y-eyeY))))*front;
                            d.y=-(p.y-eyeY)*.98f*eye;
                        }
                        if(d.sqrMagnitude>1e-10) moved++;
                        delta[i]=matrices[i].MultiplyVector(d);
                    }
                    mesh.AddBlendShapeFrame(shape,100,delta,null,null);
                    Debug.Log($"RPS_FACE {sex} {shape}: {moved} vertices");
                }
                string path=$"{Generated}/Rps{sex}Face.asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
                else AssetDatabase.CreateAsset(mesh,path);
                skin.sharedMesh=mesh;
            }
            RpsSalsaSetup.Configure(actor);
            var prefab=PrefabUtility.SaveAsPrefabAsset(actor,$"{Generated}/Rps{sex}Game.prefab");
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); }
    }
    static float Gaussian(float v)=>Mathf.Exp(-v*v*2);
    private static Material Material(string name,Color color,bool emissive)
    {
        string path=$"{Generated}/{name}.mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) { m=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m,path); }
        m.color=color; m.SetFloat("_Glossiness",.28f);
        if(emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*.7f); }
        EditorUtility.SetDirty(m); return m;
    }
    [MenuItem("RPS/Validate Game")]
    public static void Validate()
    {
        int[,] expected={{0,-1,1},{1,0,-1},{-1,1,0}};
        for(int a=1;a<=3;a++) for(int b=1;b<=3;b++)
            if(RpsRules.Compare((RpsChoice)a,(RpsChoice)b)!=expected[a-1,b-1]) throw new Exception("Rules failed");
        if(RpsRules.RatingDelta(1000,1000,1)!=16 || RpsRules.RatingDelta(1000,1000,0)!=-16) throw new Exception("ELO failed");
        var c=Resources.Load<RpsCharacterCatalog>("RpsCharacterCatalog");
        foreach(var prefab in new[]{c.male,c.female})
        {
            if(!prefab.GetComponent<Emoter>() || !prefab.GetComponent<QueueProcessor>()) throw new Exception("SALSA missing");
            if(prefab.GetComponent<Emoter>().emotes.Count!=4) throw new Exception("SALSA expressions missing");
            if(prefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.blendShapeCount!=4) throw new Exception("Face shapes missing");
        }
        Debug.Log("RPS_VALIDATION_PASS: 9 outcomes, ELO, 2 SALSA rigs, 8 facial shapes.");
    }
    public static void BuildWindows()
    {
        Build(); BuildPlayer(BuildTarget.StandaloneWindows64,"../Builds/Windows/DuelArena.exe");
    }
    public static void RenderFaces()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var key=new GameObject("Key").AddComponent<Light>(); key.type=LightType.Directional; key.intensity=1.1f; key.transform.rotation=Quaternion.Euler(20,170,0);
        var catalog=Resources.Load<RpsCharacterCatalog>("RpsCharacterCatalog");
        for(int row=0;row<2;row++) for(int col=0;col<5;col++)
        {
            var actor=UnityEngine.Object.Instantiate(row==0?catalog.male:catalog.female);
            actor.transform.position=new Vector3((2-col)*.36f,-row*.38f,0);
            if(col>0) actor.GetComponentInChildren<SkinnedMeshRenderer>().SetBlendShapeWeight(col-1,100);
        }
        var camera=new GameObject("Camera").AddComponent<Camera>(); camera.transform.position=new Vector3(0,1.375f,3); camera.transform.rotation=Quaternion.Euler(0,180,0);
        camera.orthographic=true; camera.orthographicSize=.35f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.04f,.06f,.1f);
        var rt=new RenderTexture(2200,900,24); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var tex=new Texture2D(2200,900,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,2200,900),0,0); tex.Apply();
        Directory.CreateDirectory("../Builds/Validation"); File.WriteAllBytes("../Builds/Validation/facial-shapes.png",tex.EncodeToPNG());
        RenderTexture.active=null; camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log("RPS_FACE_RENDERED");
    }
    public static void BuildWeb()
    {
        PlayerSettings.WebGL.template="PROJECT:Duel";
        Validate(); BuildPlayer(BuildTarget.WebGL,"../Builds/WebGL");
    }
    private static void BuildPlayer(BuildTarget target,string path)
    {
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName=path,target=target,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("RPS_BUILD_SUCCESS "+target+" "+report.summary.totalSize);
    }
}
