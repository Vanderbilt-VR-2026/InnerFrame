using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;

namespace InnerFrame.Gallery.EditorTools
{
    /// <summary>
    /// The Gallery shell's side of Unity (run by GalleryDecorSetup before the decor), for Gallery.fbx as
    /// Art/Source/Gallery/build_gallery.py makes it (7 x 9 x 4 m, 2026-11-10). What it writes, each only when it differs:
    ///   - Assets/Art/Gallery/Materials/Mat_*.mat for every surface in Slots, and Gallery.fbx's remap of each FBX
    ///     material to its .mat. A surface with a ChatGPT texture (Assets/Art/Gallery/Textures/GT_&lt;Name&gt;_BaseColor.jpg,
    ///     made by Art/Source/Gallery/make_textures.py) gets it at its real-world size (Gallery.fbx's UVs are in metres;
    ///     the rug and the door are mapped once); without one it keeps a plain colour. Mat_Frame (every painting's frame: The
    ///     Bedroom's, and The Starry Night's and Vincent's portrait's, which share it) takes the trim's painted wood tinted
    ///     to the carved frame corners' colour (Tints), so the frame and its corners read as one carving. Mat_Metal,
    ///     Mat_Bench and Mat_Baseboard stay as they are;
    ///   - Gallery.unity: the Rug gets a copy of the Floor's TeleportationArea (Gallery.fbx generates a mesh collider for
    ///     every object, so the rug's would otherwise stop teleports in the middle of the room); the Door keeps its
    ///     generated collider, so nobody walks out through the arch; the XR Origin at Spawn_Point; Slot_Bedroom and
    ///     Button_Bedroom at their distance from the north wall (FromNorthWall).
    /// </summary>
    public static class GalleryShellSetup
    {
        public const string Fbx = "Assets/Art/Gallery/Gallery.fbx";
        public const string Materials = "Assets/Art/Gallery/Materials";
        public const string Textures = "Assets/Art/Gallery/Textures";
        const string Tag = "[GalleryShell]";

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        // material, plain colour (no texture), texture name; metres per repeat across (u) and up (v), 0 = once across the
        // object (the rug and the door are UV-mapped 0..1); v0: the height (m) where the image's bottom edge sits, for a
        // band mapped once up its height (Gallery.fbx's wall UVs are (along the wall, height) in metres); smoothness; both
        // faces. Colours from the concept sheet: Pompeian red walls, grey wood wainscot, warm stone trim, honey oak floor.
        // The band heights are build_gallery.py's: cornice 0.25 m under the 4 m ceiling, rail 0.06 m round the 1 m wainscot.
        public static readonly (string mat, Color plain, string tex, float mu, float mv, float v0, float smooth, bool twoSided)[] Slots =
        {
            ("Mat_Wall",      Hex("8E2A1F"), "Wall",      2.0f, 2.0f,  0f,    0.05f, false),
            ("Mat_Ceiling",   Hex("A8957E"), "Ceiling",   2.0f, 2.0f,  0f,    0.05f, false),
            ("Mat_Wainscot",  Hex("7C766C"), "Wainscot",  1.5f, 1.0f,  0f,    0.15f, false),
            ("Mat_Cornice",   Hex("B49A78"), "Cornice",   0.5f, 0.25f, 3.75f, 0.15f, false),
            ("Mat_Rail",      Hex("B49A78"), "Rail",      0.5f, 0.06f, 0.97f, 0.15f, false),
            ("Mat_Trim",      Hex("B49A78"), "Trim",      0.5f, 0.5f,  0f,    0.15f, false),
            ("Mat_Floor",     Hex("9A5A2A"), "Floor",     2.0f, 2.0f,  0f,    0.25f, false),
            ("Mat_Rug",       Hex("7A1E1E"), "Rug",       0f,   0f,    0f,    0.0f,  false),
            ("Mat_BenchWood", Hex("3B2414"), "BenchWood", 0.6f, 0.6f,  0f,    0.35f, false),
            ("Mat_Cushion",   Hex("4A2A1E"), "Cushion",   0.5f, 0.5f,  0f,    0.2f,  false),
            ("Mat_Door",      Hex("4F5A36"), "Door",      0f,   0f,    0f,    0.2f,  true),
            ("Mat_Frame",     Hex("B0612A"), "Trim",      0.4f, 0.4f,  0f,    0.3f,  false),
        };
        // A texture's tint where it isn't white. Mat_Frame: GT_Trim's median colour (linear 0.658, 0.337, 0.159) times this
        // is GP_FrameCorner_BaseColor's (linear 0.597, 0.136, 0.011; its black gutters left out), measured 2026-11-10.
        public static readonly Dictionary<string, Color> Tints = new Dictionary<string, Color>
        {
            ["Mat_Frame"] = Hex("F4AA4A"),   // (0.958, 0.667, 0.291) to the nearest 1/255
        };
        public static string TexPath(string name) => $"{Textures}/GT_{name}_BaseColor.jpg";

        // The scene's own adjustments to The Bedroom's objects (overrides on Gallery.fbx, set by hand in the 14 m room): each
        // as its distance into the room from the north wall's face, so it follows the wall (z -7 then, -4.5 now). The
        // painting's slot stands 0.1 m off the wall (Slot_Bedroom z -6.9 then); the button sits 1.518 m out (z -5.482
        // then), lower and smaller than Gallery.fbx's. SN_GallerySetup copies both onto The Starry Night.
        public static readonly (string name, float fromWall)[] FromNorthWall = { ("Slot_Bedroom", 0.1f), ("Button_Bedroom", 1.518f) };

        public static void Apply(List<string> changes)
        {
            var mats = Slots.ToDictionary(s => s.mat, s => MakeSurface(s, changes));
            Remap(mats, changes);
            var scene = EditorSceneManager.OpenScene(GalleryDecorSetup.GalleryPath, OpenSceneMode.Single);
            var why = new List<string>();
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();

            var door = all.FirstOrDefault(t => t.name == "Door") ?? throw new Exception($"{Tag} Gallery.unity has no Door: is Gallery.fbx rebuilt?");
            if (door.GetComponent<Collider>() == null) { door.gameObject.AddComponent<MeshCollider>(); why.Add("a collider on the Door"); }
            var floorArea = all.Select(t => t.GetComponent<TeleportationArea>()).FirstOrDefault(a => a != null && a.name == "Floor")
                            ?? throw new Exception($"{Tag} the Floor has no TeleportationArea");
            var rug = all.First(t => t.name == "Rug");
            var rugArea = rug.GetComponent<TeleportationArea>();
            if (rugArea == null) { rugArea = rug.gameObject.AddComponent<TeleportationArea>(); why.Add("a TeleportationArea on the Rug"); }
            // the Floor's settings (its colliders list is empty: each area finds its own object's collider on Awake)
            if (EditorJsonUtility.ToJson(rugArea) != EditorJsonUtility.ToJson(floorArea))
            {
                EditorUtility.CopySerialized(floorArea, rugArea);
                why.Add("the Rug's TeleportationArea set as the Floor's");
            }

            float wall = -GalleryDecorSetup.HalfL;
            foreach (var (name, fromWall) in FromNorthWall)
            {
                var t = all.First(x => x.name == name);
                var p = t.localPosition;
                if (Mathf.Abs(p.z - (wall + fromWall)) > 1e-4f) { t.localPosition = new Vector3(p.x, p.y, wall + fromWall); why.Add($"{name} {fromWall} m from the north wall"); }
            }

            var spawn = all.FirstOrDefault(t => t.name == "Spawn_Point") ?? throw new Exception($"{Tag} no Spawn_Point");
            var origin = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null);
            if (origin != null && ((origin.transform.position - spawn.position).sqrMagnitude > 1e-8f || Quaternion.Angle(origin.transform.rotation, spawn.rotation) > 1e-3f))
            {
                origin.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                why.Add($"XR Origin to Spawn_Point {spawn.position:F2}");
            }
            if (why.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception($"{Tag} Could not save the Gallery");
                changes.Add($"{GalleryDecorSetup.GalleryPath}: {string.Join(", ", why)}");
            }
        }

        static Material MakeSurface((string mat, Color plain, string tex, float mu, float mv, float v0, float smooth, bool twoSided) s, List<string> changes)
        {
            string texPath = TexPath(s.tex);
            bool has = File.Exists(texPath);
            bool once = s.mu <= 0f;
            if (has) GalleryDecorSetup.ImportTexture(texPath, 2048, changes, repeat: !once);
            var tiling = once ? Vector2.one : new Vector2(1f / s.mu, 1f / s.mv);
            var offset = once ? Vector2.zero : new Vector2(0f, -s.v0 / s.mv);
            return GalleryDecorSetup.MakeMaterial($"{Materials}/{s.mat}.mat", has ? texPath : null, has ? (Tints.TryGetValue(s.mat, out var tint) ? tint : Color.white) : s.plain,
                s.smooth, s.twoSided, false, changes, tiling, offset);
        }

        static void Remap(Dictionary<string, Material> mats, List<string> changes)
        {
            var mi = AssetImporter.GetAtPath(Fbx) as ModelImporter ?? throw new Exception($"{Tag} {Fbx} missing");
            var existing = mi.GetExternalObjectMap();
            var why = new List<string>();
            foreach (var (name, m) in mats)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (existing.TryGetValue(id, out var have) && have == m) continue;
                mi.AddRemap(id, m);
                why.Add(name);
            }
            if (why.Count > 0) { mi.SaveAndReimport(); changes.Add($"{Fbx}: materials {string.Join(", ", why)}"); }
        }
    }
}
