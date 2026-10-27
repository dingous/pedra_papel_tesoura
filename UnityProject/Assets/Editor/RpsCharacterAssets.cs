using System;
using System.IO;
using System.Linq;
using RpsArena.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RpsCharacterImporter : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Characters/") || !assetPath.EndsWith(".fbx")) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.optimizeGameObjects = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
    }

    private void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith("Assets/Characters/") || !assetPath.EndsWith(".fbx")) return;
        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = clip.takeName.Split('|').Last();
            clip.loopTime = clip.name == "Idle";
            clip.lockRootPositionXZ = true;
            clip.lockRootHeightY = true;
            clip.lockRootRotation = true;
        }
        importer.clipAnimations = clips;
    }
}

public static class RpsCharacterAssets
{
    private static readonly string[] Required = { "Idle", "CountIn", "Rock", "Paper", "Scissors", "LeftCountIn", "LeftRock", "LeftPaper", "LeftScissors" };

    [MenuItem("RPS/Rebuild Character Assets")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach (string sex in new[] { "Male", "Female" }) BuildOne(sex);
        BuildPreview();
        AssetDatabase.SaveAssets();
        Debug.Log("RPS_CHARACTERS_VALIDATED: 2 FBX, 18 clips, 2 prefabs, 30 finger joints per character.");
    }

    private static void BuildOne(string sex)
    {
        string folder = "Assets/Characters/" + sex;
        string modelPath = folder + "/Rps" + sex + ".fbx";
        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (!model) throw new Exception("Missing FBX: " + modelPath);
        var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        Debug.Log("RPS clips " + sex + ": " + string.Join(", ", clips.Select(c => c.name + "=" + c.length)));
        foreach (string name in Required)
            if (!clips.Any(c => c.name == name && c.length > 0)) throw new Exception("Missing animation: " + sex + "/" + name);
        var transforms = model.GetComponentsInChildren<Transform>(true);
        foreach (string side in new[] { "Left", "Right" })
        foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
        for (int i = 1; i <= 3; ++i)
            if (!transforms.Any(t => t.name == side + "Hand" + finger + i)) throw new Exception("Missing finger bone.");
        var probe = UnityEngine.Object.Instantiate(model);
        foreach (string side in new[] { "Right", "Left" })
        {
            string prefix = side == "Right" ? "" : "Left";
            var index = probe.GetComponentsInChildren<Transform>().Single(t => t.name == side + "HandIndex2");
            var ring = probe.GetComponentsInChildren<Transform>().Single(t => t.name == side + "HandRing2");
            clips.Single(c => c.name == prefix + "Paper").SampleAnimation(probe, 1f);
            var openIndex = index.localRotation; var openRing = ring.localRotation;
            clips.Single(c => c.name == prefix + "Rock").SampleAnimation(probe, 1f);
            if (Quaternion.Angle(openIndex, index.localRotation) < 35 || Quaternion.Angle(openRing, ring.localRotation) < 35)
                throw new Exception("Rock does not close the fingers: " + sex + side);
            clips.Single(c => c.name == prefix + "Scissors").SampleAnimation(probe, 1f);
            if (Quaternion.Angle(openIndex, index.localRotation) > 5 || Quaternion.Angle(openRing, ring.localRotation) < 35)
                throw new Exception("Scissors finger pose failed: " + sex + side);
        }
        UnityEngine.Object.DestroyImmediate(probe);

        string materialPath = folder + "/Rps" + sex + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/texture_0.png");
        material.SetFloat("_Glossiness", 0.18f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        string handMaterialPath = folder + "/Rps" + sex + "Hands.mat";
        var handMaterial = AssetDatabase.LoadAssetAtPath<Material>(handMaterialPath);
        if (!handMaterial)
        {
            handMaterial = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(handMaterial, handMaterialPath);
        }
        handMaterial.color = JsonUtility.FromJson<Color>(File.ReadAllText(folder + "/HandColor.json")).gamma;
        handMaterial.SetFloat("_Glossiness", .18f);
        EditorUtility.SetDirty(handMaterial);

        string controllerPath = folder + "/Rps" + sex + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var old in machine.states) machine.RemoveState(old.state);
        foreach (string name in Required)
        {
            var state = machine.AddState(name);
            state.motion = clips.Single(c => c.name == name);
            state.writeDefaultValues = true;
            if (name == "Idle") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Rps" + sex;
        foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.sharedMaterials = Enumerable.Range(0, renderer.sharedMaterials.Length).Select(i => i == 0 ? material : handMaterial).ToArray();
            renderer.updateWhenOffscreen = true;
        }
        var animator = instance.GetComponent<Animator>();
        if (!animator) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        instance.AddComponent<RpsCharacterAnimator>();
        PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Rps" + sex + ".prefab");
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static void BuildPreview()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, .95f, 4f);
        camera.transform.rotation = Quaternion.Euler(0,180,0);
        camera.orthographic = true; camera.orthographicSize = 1.05f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f,.13f,.19f);
        var light = new GameObject("KeyLight").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(35,150,0);
        var cycle = new GameObject("GesturePreviewCycle").AddComponent<RpsCharacterPreviewCycle>();
        cycle.characters = new RpsCharacterAnimator[2];
        for (int i=0; i<2; i++)
        {
            string sex = i==0 ? "Male" : "Female";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/"+sex+"/Rps"+sex+".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(i==0 ? -.55f : .55f,0,0);
            cycle.characters[i] = instance.GetComponent<RpsCharacterAnimator>();
        }
        EditorSceneManager.SaveScene(scene,"Assets/Characters/RpsCharactersPreview.unity");
    }

    public static void RenderValidation()
    {
        Build();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(.6f,.6f,.6f);
        var camera = new GameObject("ValidationCamera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 2.05f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.14f,.20f);
        var light = new GameObject("ValidationLight").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1f;
        bool frontPositive = false;
        for (int row = 0; row < 2; row++)
        for (int col = 0; col < 3; col++)
        {
            string sex = row == 0 ? "Male" : "Female";
            string folder = "Assets/Characters/" + sex;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Rps"+sex+".prefab"));
            string name = new[] { "Rock", "Paper", "Scissors" }[col];
            var clip = AssetDatabase.LoadAllAssetsAtPath(folder+"/Rps"+sex+".fbx").OfType<AnimationClip>().Single(c => c.name == name);
            instance.GetComponent<Animator>().enabled = false;
            clip.SampleAnimation(instance,1f);
            instance.transform.position = new Vector3((1-col)*1.15f, row==0 ? .15f : -1.8f, 0);
            if (row==0 && col==0)
            {
                var all = instance.GetComponentsInChildren<Transform>();
                frontPositive = all.Single(t=>t.name=="headfront").position.z > all.Single(t=>t.name=="Head").position.z;
            }
        }
        camera.transform.position = new Vector3(0,0,frontPositive ? 5 : -5);
        camera.transform.LookAt(Vector3.zero);
        light.transform.rotation = Quaternion.Euler(25,frontPositive ? -155 : -25,0);
        var target = new RenderTexture(1440,1600,24);
        camera.targetTexture = target; camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1440,1600,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1440,1600),0,0);image.Apply();
        string output = Path.GetFullPath("../ArtSource/Characters/Unity_Characters_Validation.png");
        File.WriteAllBytes(output,image.EncodeToPNG());
        RenderTexture.active = null;camera.targetTexture=null;target.Release();
        UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
        Debug.Log("RPS_RENDER_SAVED " + output);
    }
}
