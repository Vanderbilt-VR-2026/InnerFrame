using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Player avatars, last step. For each character in Assets/Script/Network/Avatars/ named in PlayerAvatarBody.Order
/// (Avatar_BoyWorker.fbx: rigged in Mixamo, Art/Source/Avatars/make_mixamo_upload.py; Avatar_Girl.fbx: rigged with
/// Rigify in Blender, Art/Source/Avatars/Girl/scripts/export_unity.py): imports it as a Humanoid, gives it a URP Lit material on its 2048 base colour
/// (Avatars/Textures, from Art/Source/Avatars/make_textures.py) and an Animator with an IK pass, and saves
/// Resources/PlayerAvatars/&lt;name&gt;.prefab with PlayerAvatarRig. An "Idle" clip in the folder (a Mixamo animation,
/// "FBX for Unity, Without Skin", file name containing "Idle") plays under the IK if there is one.
/// Safe to run again: assets keep their GUIDs. Menu: InnerFrame > Multiplayer > Build Player Avatars.
/// </summary>
public static class PlayerAvatarBuilder
{
    const string Dir = "Assets/Script/Network/Avatars";
    const string OutDir = "Assets/Script/Network/Resources/PlayerAvatars";
    const string ControllerPath = Dir + "/PlayerAvatar.controller";

    [MenuItem("InnerFrame/Multiplayer/Build Player Avatars")]
    public static void Build()
    {
        NetworkPlayerPrefabBuilder.EnsureFolder(OutDir);
        var controller = Controller();
        int built = 0;
        foreach (var name in PlayerAvatarBody.Order)
        {
            string fbx = $"{Dir}/{name}.fbx";
            if (!File.Exists(fbx)) { Debug.LogWarning($"[PlayerAvatarBuilder] {fbx} isn't there yet (rig it in Mixamo first): skipped"); continue; }
            if (BuildOne(name, fbx, controller)) built++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[PlayerAvatarBuilder] Built {built} of {PlayerAvatarBody.Order.Length} player avatars in {OutDir}");
    }

    public static void BuildFromCommandLine()
    {
        try { Build(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static bool BuildOne(string name, string fbx, AnimatorController controller)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        // Mixamo's one-frame T-pose take, kept as a standing pose: with no clip at all a Humanoid puts its hips on the
        // floor (the body sank a metre). Feet on the ground, no root motion; the IK moves the arms and head from it.
        importer.importAnimation = true;
        var takes = importer.defaultClipAnimations;
        foreach (var c in takes)
        {
            c.name = name + "_Stand";
            c.loopTime = true;
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true;
            c.heightFromFeet = true;
        }
        importer.clipAnimations = takes;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;  // our own material, below
        importer.importCameras = importer.importLights = importer.importBlendShapes = false;
        importer.SaveAndReimport();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
        if (model == null || avatar == null || !avatar.isHuman || !avatar.isValid)
        {
            Debug.LogError($"[PlayerAvatarBuilder] {fbx}: no valid Humanoid avatar (is it a rigged, skinned FBX?)");
            return false;
        }

        var stand = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        var go = Object.Instantiate(model);
        go.name = name;
        if (!go.TryGetComponent(out Animator animator)) animator = go.AddComponent<Animator>();   // not ??: Unity's missing component isn't a real null
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        if (stand != null && controller.layers[0].stateMachine.defaultState.motion == Placeholder())
        {
            // no Idle clip: this avatar stands in its own T-pose (an override, so each avatar uses its own take)
            var overrides = new AnimatorOverrideController(controller) { name = name + "_Animator" };
            string overridePath = $"{Dir}/{name}.overrideController";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (existing == null) { AssetDatabase.CreateAsset(overrides, overridePath); existing = overrides; }
            existing.runtimeAnimatorController = controller;
            existing[Placeholder()] = stand;
            EditorUtility.SetDirty(existing);
            animator.runtimeAnimatorController = existing;
        }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the arms follow the controllers even off screen

        var material = Material(name);
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.updateWhenOffscreen = true;   // IK moves the hands outside the bind-pose bounds
        }

        // eyes: a little above and in front of the Humanoid head bone (Mixamo's sits at the base of the skull)
        if (!go.TryGetComponent(out PlayerAvatarRig rig)) rig = go.AddComponent<PlayerAvatarRig>();
        var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        rig.eyeHeight = headBone.position.y - go.transform.position.y + 0.08f;
        rig.eyeForward = 0.09f;

        // a long skirt (Skirt_* bones, the Girl): keep it clear of the legs
        bool skirt = go.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Skirt_Front_1");
        if (skirt && !go.TryGetComponent(out PlayerAvatarSkirt _)) go.AddComponent<PlayerAvatarSkirt>();

        string prefabPath = $"{OutDir}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[PlayerAvatarBuilder] {prefabPath}: eyes at {rig.eyeHeight:F2} m, {avatar.humanDescription.human.Length} Humanoid bones");
        return true;
    }

    static Material Material(string name)
    {
        string path = $"{Dir}/M_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        string texPath = $"{Dir}/Textures/{name}_BaseColor.jpg";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) Debug.LogWarning($"[PlayerAvatarBuilder] {texPath} missing: run Art/Source/Avatars/make_textures.py");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // One layer with an IK pass (PlayerAvatarRig.OnAnimatorIK), standing still or playing an Idle clip if there is one.
    static AnimatorController Controller()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;

        var machine = controller.layers[0].stateMachine;
        var stand = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Stand") ?? machine.AddState("Stand");
        machine.defaultState = stand;
        stand.motion = IdleClip() ?? Placeholder();   // the placeholder is swapped for each avatar's T-pose
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // An empty clip in the controller's state, for AnimatorOverrideController to replace with each avatar's T-pose.
    static AnimationClip Placeholder()
    {
        string path = Dir + "/PlayerAvatar_Stand.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = "PlayerAvatar_Stand" }; AssetDatabase.CreateAsset(clip, path); }
        return clip;
    }

    static AnimationClip IdleClip()
    {
        foreach (var guid in AssetDatabase.FindAssets("Idle t:Model", new[] { Dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                var clips = importer.defaultClipAnimations;
                foreach (var c in clips) { c.loopTime = true; c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true; }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip != null) return clip;
        }
        return null;
    }
}
