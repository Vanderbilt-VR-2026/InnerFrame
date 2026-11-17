using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// (Re)creates Van Gogh's assets from the files in Assets/VanGogh/Art and Audio (the dialogue box's in Assets/Shared/UI): import settings, materials, the
/// baked sign, the caption sprites and font with the VG_Caption prefab, and the VG_Vincent prefab, with the mirror scene's canvas
/// stages and lines. Safe to run again: it only reimports what differs. Tools > InnerFrame > Van Gogh > Rebuild assets,
/// or -executeMethod VanGoghAssetSetup.Rebuild.
/// </summary>
public static class VanGoghAssetSetup
{
    const string Art = "Assets/VanGogh/Art";
    const string ResourcesDir = "Assets/VanGogh/Resources";   // loaded at runtime by VanGoghSpawner
    const string PortraitTexture = Art + "/VG_SelfPortrait_1887.jpg";
    const string PortraitMaterial = ResourcesDir + "/VG_SelfPortrait.mat";
    // Vincent v2 (2026-11-17, replacing the static v1 VG_Vincent.fbx): Mixamo's rig of the Tripo model and its clips,
    // imported (Humanoid, the clips' avatar) and given its material by the Starry Night's setup (SN_Setup), used here as they are
    const string V2Dir = "Assets/StarryNight/Models/Vincent/", V2 = V2Dir + "VincentV2.fbx", V2Material = "Assets/StarryNight/Materials/SN_VincentV2.mat";
    const string V2Controller = Art + "/Vincent/VG_VincentV2.controller";   // made here: Standing (BreathingIdle), Walking
    static readonly Vector3 MouthInHead = new Vector3(0f, 0.07f, 0.1f);   // his voice in his head bone's space (SN_Layout.vincentMouthInHead)
    const string Props = Art + "/Props/";
    // the Tripo easel and palette, cleaned by Art/Source/VanGogh/Props/clean_easel.py and clean_palette.py
    const string EaselTexture = Props + "VG_Easel_BaseColor.jpg", EaselMaterial = Props + "M_Easel.mat";
    const string PaletteTexture = Props + "VG_Palette_BaseColor.jpg", PaletteMaterial = Props + "M_Palette.mat";
    const string VincentPrefab = ResourcesDir + "/VG_Vincent.prefab";
    const string CanvasStages = Art + "/Canvas/";       // from Art/Source/VanGogh/make_canvas_stages.py
    static readonly string[] Stages = { "VG_Canvas_1_Sketch", "VG_Canvas_2_Colour", "VG_Canvas_3_Finished" };
    const string MirrorAudio = "Assets/VanGogh/Audio/";  // from Art/Source/VanGogh/make_mirror_lines.sh
    const string MirrorLines = "Art/Source/VanGogh/mirror_lines.txt";   // relative to the project folder
    const string CaptionUI = Art + "/UI/";              // from Art/Source/VanGogh/make_caption_sprites.py
    // the shared dialogue box set (prefab, scripts, sprites, font, Resources/VG_Caption), used by every scene
    const string SharedUI = "Assets/Shared/UI", SharedArt = SharedUI + "/Art/";   // sprites: make_box_sprites.py, and make_caption_sprites.py's button
    const string CaptionFont = SharedUI + "/Fonts/LibreBaskerville-Regular.ttf";
    const string CaptionPrefab = SharedUI + "/Resources/VG_Caption.prefab";
    const string SignUnlit = CaptionUI + "VG_Sign_A.png", SignLit = CaptionUI + "VG_Sign_B.png";   // from Art/Source/VanGogh/Caption/bake_sign.py
    // the 9-slice borders drawn by make_caption_sprites.py (PARCHMENT_BORDER, PILL_H / 2)
    const int ParchmentBorderPixels = 96, PillBorderPixels = 64;
    // the dialogue box's sprites, from Art/Source/VanGogh/make_box_sprites.py (SLICE)
    const int BoxBorderPixels = 64;
    // the reusable dialogue UI (Assets/Shared/UI): its prefab there, and here one settings asset per scene that uses it
    const string Prefabs = "Assets/VanGogh/Prefabs/", DialoguePrefab = SharedUI + "/Prefabs/VG_DialogueUI.prefab";
    const string OutlineMaterial = CaptionUI + "VG_Outline.mat";   // the mirror's outlines (VG_MirrorOutline)
    static readonly Color Chrome = new Color32(0xE9, 0xC1, 0x4A, 0xFF);
    const float VincentHeight = 1.70f;

    // Prefab layout in Vincent's own space: he faces +z, his right hand is at +x, metres at 1.70 m tall.
    static readonly Vector3 EaselAt = new Vector3(0.627f, 0f, 0.073f);   // puts the canvas's centre where the 10/26 placeholder's was (clean_easel.py)
    const float EaselTurn = -45f;   // canvas faces halfway between him and a visitor in front of him
    // the palette in his left hand, held from below as v1 held it: its grip (the thumb hole) and its turn in his left hand
    // bone's space, so that with VG_Hands' held pose (palm up, his wrist at his waist in front of him) it lies flat on his
    // palm and fingers, his thumb under the hole, nothing of his hand or forearm inside it over his whole idle loop
    // (derived 2026-11-17 by sweeping its height; VG_BedroomV2Check counts it)
    static readonly Vector3 PaletteGripInHand = new Vector3(-0.00284f, 0.12274f, 0.11087f);
    static readonly Quaternion PaletteInHand = new Quaternion(0.139219f, -0.608354f, -0.693267f, -0.360424f);
    static readonly Vector3 BrushTurn = new Vector3(20f, 0f, 0f);   // in his right hand bone's space

    [MenuItem("Tools/InnerFrame/Van Gogh/Rebuild assets")]
    public static void Rebuild()
    {
        SaveMaterial(PortraitMaterial, "Universal Render Pipeline/Unlit", ImportTexture(PortraitTexture, 1024));
        BuildCaption();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(V2) != null)
        {
            foreach (var prop in new[] { "VG_Easel", "VG_Palette", "VG_Brush_01" }) ImportModel(Props + prop + ".fbx", true);
            var easel = Furniture(SaveMaterial(EaselMaterial, "Universal Render Pipeline/Lit", ImportTexture(EaselTexture, 1024)));
            var palette = Furniture(SaveMaterial(PaletteMaterial, "Universal Render Pipeline/Lit", ImportTexture(PaletteTexture, 1024)));
            var stages = Stages.Select(s => ImportTexture(CanvasStages + s + ".png", 1024)).ToArray();
            BuildVincent(easel, palette, stages, ReadMirrorLines(), Outline());
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[VanGoghAssetSetup] Rebuild done");
    }

    // Max size, mipmaps on, and no Android override, so Android uses its default compression.
    static Texture2D ImportTexture(string path, int maxSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.maxTextureSize != maxSize || !importer.mipmapEnabled
            || importer.textureCompression != TextureImporterCompression.Compressed
            || importer.GetPlatformTextureSettings("Android").overridden)
        {
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.ClearPlatformTextureSettings("Android");
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // A sprite with a 9-slice border (pixels on every side), mipmaps on, clamped, no Android override.
    static Sprite ImportSprite(string path, int border, int maxSize = 1024)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var borders = Vector4.one * border;
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
            || importer.spriteBorder != borders || importer.maxTextureSize != maxSize || !importer.mipmapEnabled
            || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp
            || importer.GetPlatformTextureSettings("Android").overridden)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = borders;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.ClearPlatformTextureSettings("Android");
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Resources/VG_Caption: VanGoghCaption with its sprites and font, in Box style; it builds the panels itself at runtime.
    static void BuildCaption()
    {
        var root = new GameObject("VG_Caption", typeof(RectTransform));
        var caption = root.AddComponent<VanGoghCaption>();
        caption.signUnlit = ImportSprite(SignUnlit, 0, 2048);
        caption.signLit = ImportSprite(SignLit, 0, 2048);
        caption.lit = true;
        caption.parchment = ImportSprite(CaptionUI + "VG_Caption_Parchment.png", ParchmentBorderPixels);
        caption.pill = ImportSprite(CaptionUI + "VG_Caption_Pill.png", PillBorderPixels);
        caption.button = ImportSprite(SharedArt + "VG_Caption_Button.png", 0);
        caption.font = AssetDatabase.LoadAssetAtPath<Font>(CaptionFont);
        caption.style = VG_CaptionStyle.Box;
        caption.dialogue = BuildDialogueUI(caption.font, caption.button);
        PrefabUtility.SaveAsPrefabAsset(root, CaptionPrefab);
        Object.DestroyImmediate(root);
    }

    // Shared/UI/Prefabs/VG_DialogueUI: the dialogue UI with its sprites, font and the Bedroom's settings; it builds itself at runtime.
    static VG_DialogueUI BuildDialogueUI(Font font, Sprite key)
    {
        if (!AssetDatabase.IsValidFolder(Prefabs.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/VanGogh", "Prefabs");
        // the Bedroom as approved (1.3 m); the Starry Night from its blockout (7ab7a24: SN_DialogueBox.cs:16-20,
        // SN_Hint.cs:10-11, SN_UIRoot.cs:15), at 1.6 m
        var bedroom = DialogueSettings("Bedroom", 1.3f, 0.032f, 0.025f, 0.036f, 0.025f, "The Yellow House", 0f, null);
        DialogueSettings("StarryNight", 1.6f, 0.040f, 0.030f, 0.045f, 0.030f, "A Night with Vincent", 0.5f, Color.white);
        var root = new GameObject("VG_DialogueUI", typeof(RectTransform));
        var ui = root.AddComponent<VG_DialogueUI>();
        ui.settings = bedroom;
        ui.font = font;
        ui.key = key;
        ui.box = ImportSprite(SharedArt + "VG_Box.png", BoxBorderPixels);
        ui.nameTab = ImportSprite(SharedArt + "VG_NameTab.png", BoxBorderPixels);
        ui.hintPlate = ImportSprite(SharedArt + "VG_HintPlate.png", BoxBorderPixels);
        ui.star = ImportSprite(SharedArt + "VG_Star.png", 0);
        ui.starRing = ImportSprite(SharedArt + "VG_StarRing.png", 0);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefab).GetComponent<VG_DialogueUI>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Made once, then left alone, so tuning in the Inspector stays. Sizes in metres at `distance`, kept as angles; the
    // rest are VG_DialogueSettings' defaults (15 deg under the gaze, 20 deg dead zone, gaze pitch -30..45, 0.5 s).
    static VG_DialogueSettings DialogueSettings(string scene, float distance, float words, float name, float title, float line,
        string hintTitle, float hintFadeIn, Color? lineColour)
    {
        string path = Prefabs + "VG_Dialogue_" + scene + ".asset";
        var settings = AssetDatabase.LoadAssetAtPath<VG_DialogueSettings>(path);
        if (settings != null) return settings;
        settings = ScriptableObject.CreateInstance<VG_DialogueSettings>();
        float Degrees(float metres) => 2f * Mathf.Atan(metres / 2f / distance) * Mathf.Rad2Deg;
        settings.distance = distance;
        settings.boxWidth = Degrees(VG_DialogueBox.Width);
        settings.wordsCap = Degrees(words);
        settings.nameCap = Degrees(name);
        settings.titleCap = Degrees(title);
        settings.lineCap = Degrees(line);
        settings.hintTitle = hintTitle;
        settings.hintFadeIn = hintFadeIn;
        if (lineColour.HasValue) settings.lineColour = lineColour.Value;
        AssetDatabase.CreateAsset(settings, path);
        return settings;
    }

    // Mono, Vorbis quality 70, decompressed on load (short lines that should start at once), no Android override.
    static AudioClip ImportClip(string path)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var s = importer.defaultSampleSettings;
        if (!importer.forceToMono || s.compressionFormat != AudioCompressionFormat.Vorbis || !Mathf.Approximately(s.quality, 0.7f)
            || s.loadType != AudioClipLoadType.DecompressOnLoad || importer.ContainsSampleSettingsOverride("Android"))
        {
            importer.forceToMono = true;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
            importer.ClearSampleSettingOverride("Android");
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // mirror_lines.txt: an id, then the text, one line each; # starts a comment. Each id has its clip VG_Mirror_<id>.wav.
    static MirrorScene.Line[] ReadMirrorLines() =>
        File.ReadAllLines(MirrorLines).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#"))
            .Select(l => l.Split(new[] { ' ' }, 2))
            .Select(p => new MirrorScene.Line { id = p[0], text = p[1], clip = ImportClip(MirrorAudio + "VG_Mirror_" + p[0] + ".wav") })
            .ToArray();

    static Material SaveMaterial(string path, string shaderName, Texture texture)
    {
        var shader = Shader.Find(shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);   // URP copies _BaseMap here when the material is next loaded; set it now so the first run saves the same file
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    // The room's wooden furniture look (InnerFrameBedroomWeb/Generated/Materials/table.mat): URP Lit with no metal, no
    // gloss, no specular highlights or reflections, two-sided, receiving shadows. So the props sit in the room's light.
    static Material Furniture(Material m)
    {
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        m.SetFloat("_ReceiveShadows", 1f);
        m.doubleSidedGI = true;
        return m;
    }

    // Unlit chrome yellow, both faces, opaque: the outlines pulse in width, not alpha, so nothing is transparent.
    static Material Outline()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterial);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, OutlineMaterial);
        }
        material.SetColor("_BaseColor", Chrome);   // URP copies it into _Color itself when it loads the material
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.doubleSidedGI = true;             // as URP sets it for two-sided materials on the next load
        EditorUtility.SetDirty(material);
        return material;
    }

    // Read/Write off, medium mesh compression, no cameras, lights, animation or colliders.
    static void ImportModel(string path, bool keepMaterials)
    {
        var m = (ModelImporter)AssetImporter.GetAtPath(path);
        var materials = keepMaterials ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
        if (!m.isReadable && m.meshCompression == ModelImporterMeshCompression.Medium && !m.importCameras && !m.importLights
            && !m.addCollider && !m.importAnimation && m.animationType == ModelImporterAnimationType.None
            && m.materialImportMode == materials) return;
        m.isReadable = false;
        m.meshCompression = ModelImporterMeshCompression.Medium;
        m.importCameras = m.importLights = m.addCollider = m.importAnimation = false;
        m.animationType = ModelImporterAnimationType.None;
        m.materialImportMode = materials;
        m.SaveAndReimport();
    }

    public static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static GameObject Spawn(string path, Transform parent) =>
        (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);

    static void BuildVincent(Material easel, Material palette, Texture2D[] stages, MirrorScene.Line[] lines, Material outline)
    {
        var root = new GameObject("VG_Vincent");
        // v2, as tall as v1 (the same framing at the easel), saved in his idle pose: his head bone is MirrorScene's Head
        var body = Spawn(V2, root.transform);
        var anim = body.GetComponent<Animator>();
        if (anim == null) anim = body.AddComponent<Animator>();
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(V2Dir + "VincentV2@BreathingIdle.fbx").OfType<Avatar>().First();
        anim.runtimeAnimatorController = BuildController();
        anim.applyRootMotion = false;   // the walk clip plays in place (VG_Lead moves him)
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var skin = body.GetComponentInChildren<SkinnedMeshRenderer>(true);
        skin.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(V2Material);
        skin.quality = SkinQuality.Bone4;   // the Mobile quality level's skin weights cap it (a team setting)
        skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        skin.updateWhenOffscreen = false;
        Clip("BreathingIdle").SampleAnimation(body, 0f);
        var baked = Baked(skin);
        body.transform.localScale = Vector3.one * (VincentHeight / (baked.Max(v => v.y) - baked.Min(v => v.y)));
        var stand = Spawn(Props + "VG_Easel.fbx", root.transform);
        stand.transform.SetLocalPositionAndRotation(EaselAt, Quaternion.Euler(0f, EaselTurn, 0f));
        stand.GetComponent<Renderer>().sharedMaterial = easel;   // its child VG_Canvas keeps the FBX's M_Canvas: MirrorScene paints the stages on it
        // the palette in his left hand (held from below by IK, VG_Hands), the brush in his right hand
        var held = Hold(Props + "VG_Palette.fbx", anim.GetBoneTransform(HumanBodyBones.LeftHand), PaletteGripInHand, PaletteInHand, ownSize: true);
        held.GetComponent<Renderer>().sharedMaterial = palette;
        Hold(Props + "VG_Brush_01.fbx", anim.GetBoneTransform(HumanBodyBones.RightHand), Vector3.zero, BrushTurn);

        var capsule = root.AddComponent<CapsuleCollider>();   // Default layer, not Teleport: nobody lands inside him
        capsule.center = new Vector3(0f, VincentHeight / 2f, 0f);
        capsule.height = VincentHeight;
        capsule.radius = 0.3f;
        var mouth = new GameObject("Voice");
        mouth.transform.SetParent(anim.GetBoneTransform(HumanBodyBones.Head), false);
        mouth.transform.localPosition = MouthInHead;
        var talker = root.AddComponent<SpeechToProxy>();
        talker.voice = VanGoghSpawner.AddVoice(mouth);
        talker.labelOffset = new Vector3(0f, VincentHeight, 0f);   // the top of his head: the caption stands on it
        var mirrorScene = root.AddComponent<MirrorScene>();
        mirrorScene.stages = stages;
        mirrorScene.lines = lines;
        mirrorScene.outline = outline;

        // What VincentAlive moves: all of him (the palette, brush and voice are in his bones), about a pivot between his feet
        var figure = new GameObject("Figure").transform;
        figure.SetParent(root.transform, false);
        var feet = Baked(skin);
        float floor = feet.Min(v => v.y);
        feet = feet.Where(v => v.y < floor + 0.05f).ToArray();
        figure.localPosition = new Vector3(feet.Average(v => v.x), 0f, feet.Average(v => v.z));
        body.transform.SetParent(figure, true);
        anim.gameObject.AddComponent<VG_Hands>().palette = held.transform;
        root.AddComponent<VincentAlive>();
        PrefabUtility.SaveAsPrefabAsset(root, VincentPrefab);
        Object.DestroyImmediate(root);
    }

    static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath($"{V2Dir}VincentV2@{name}.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));

    // His skin as posed now, in world space (the root is at the origin here).
    static Vector3[] Baked(SkinnedMeshRenderer skin)
    {
        var mesh = new Mesh();
        skin.BakeMesh(mesh, true);
        var points = mesh.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
        Object.DestroyImmediate(mesh);
        return points;
    }

    // His Animator controller in the Bedroom: Standing (BreathingIdle; also while he paints and talks, there is no standing
    // talking clip) and Walking (the bool Walking, VG_Lead), with the IK pass his hands use (VG_Hands: the palette, the door).
    static AnimatorController BuildController()
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(V2Controller);
        if (c == null) c = AnimatorController.CreateAnimatorControllerAtPath(V2Controller);
        if (!c.parameters.Any(p => p.name == "Walking")) c.AddParameter("Walking", AnimatorControllerParameterType.Bool);
        var layers = c.layers;
        if (!layers[0].iKPass) { layers[0].iKPass = true; c.layers = layers; }
        var sm = c.layers[0].stateMachine;
        AnimatorState State(string name, string clip, Vector3 at)
        {
            var s = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == name);
            if (s == null) s = sm.AddState(name, at);
            s.motion = Clip(clip);
            return s;
        }
        var standing = State("Standing", "BreathingIdle", new Vector3(300f, 100f, 0f));
        var walking = State("Walking", "Walking", new Vector3(300f, 200f, 0f));
        sm.defaultState = standing;
        void T(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            if (from.transitions.Any(x => x.destinationState == to)) return;
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.25f;
            t.AddCondition(mode, 0f, "Walking");
        }
        T(standing, walking, AnimatorConditionMode.If);
        T(walking, standing, AnimatorConditionMode.IfNot);
        EditorUtility.SetDirty(c);
        return c;
    }

    // Places a prop so that its GrabPoint (an empty from Blender) sits at the hand.
    static GameObject Hold(string path, Transform parent, Vector3 hand, Vector3 turn) => Hold(path, parent, hand, Quaternion.Euler(turn), false);

    // ownSize: its own size, whatever the parent's scale (a bone)
    static GameObject Hold(string path, Transform parent, Vector3 hand, Quaternion turn, bool ownSize)
    {
        var prop = Spawn(path, parent);
        if (ownSize) prop.transform.localScale = Vector3.one / parent.lossyScale.x;
        prop.transform.localRotation = turn;
        var grab = prop.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("GrabPoint"));
        prop.transform.localPosition += hand - parent.InverseTransformPoint(grab.position);
        return prop;
    }
}
