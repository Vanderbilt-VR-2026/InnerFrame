using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// The only thing in AlmondStudio.unity besides the player rig (blockout, step 1). At runtime it builds the studio
    /// from boxes (walls with the window opening, sill, floor, ceiling, table, chairs, easel, door), the two casements,
    /// the brush on the sill, a stand-in Vincent, the painted sky, the 3D almond tree (AS_Tree), the guides and the shared
    /// dialogue box, places the rig from AS_Layout, and starts AS_Game.
    /// Editor fallback (no headset; DesktopTestRig walks and looks with W A S D and the mouse): O opens the window,
    /// G takes or puts down the brush, Space is the trigger, N blooms the next branch at once, R starts over.
    /// </summary>
    public class AS_Bootstrap : MonoBehaviour
    {
        public AS_Layout layout;
        [Tooltip("Assets/AlmondStudio/Data/AS_Branches.json: the branch paths.")] public TextAsset branchesJson;
        public Material wall, floor, ceiling, wood, frame, windowFrame, sillTop, wallPictures, revealLeft, revealRight, canvas, brushHead, vincent, bark, ink, skyMaterial, groundMaterial, glow, guideDot, blossom;
        [Tooltip("VG_DialogueUI.prefab: Vincent's words and the hint, as in every scene.")] public GameObject uiPrefab;
        public VG_DialogueSettings uiSettings;
        [Tooltip("The comparison pictures, one per AS_Layout.compareViews entry (Textures/Reference), set by Build.")] public Texture[] references = new Texture[0];
        [Tooltip("The Bedroom's chair, saved for this scene by AS_Setup (Prefabs/AS_Chair.prefab). Boxes if missing.")] public GameObject chairPrefab;
        [Tooltip("Vincent's easel (VanGogh/Art/Props/VG_Easel.fbx) and its painted material M_Easel. Boxes if missing.")] public GameObject easelModel;
        [Tooltip("The sill's five objects as one Tripo model (Art/Props/AS_SillStillLife.fbx). Cylinders if missing.")] public GameObject sillModel;
        public Material easelMaterial;

        public AS_Game Game { get; private set; }
        AS_InputXR xrInput;
        Transform brushT;
        Pose brushHome;

        void Awake()
        {
            PlaceRig();
            Build();
        }

        void Start()
        {
#if UNITY_EDITOR
            if (!XRSettings.isDeviceActive) Game.Input = new AS_InputEditor();
            else
#endif
                Game.Input = xrInput = new AS_InputXR(FindAnyObjectByType<XROrigin>());
            if (Game.ui != null) Game.ui.Snap();
            Game.Begin();
        }

        void OnDestroy() => xrInput?.Dispose();

        void PlaceRig()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin != null) origin.transform.SetPositionAndRotation(layout.playerStart, Quaternion.Euler(0f, layout.playerStartYaw, 0f));
        }

        // ------------------------------------------------------------------------------------------------ the room

        void Build()
        {
            var world = new GameObject("AS_World").transform;
            float W = layout.roomWidth, D = layout.roomDepth, H = layout.roomHeight, T = layout.wallThickness;
            float ww = layout.windowWidth, wb = layout.windowBottom, wt = layout.windowTop;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = layout.flatShading ? layout.flatAmbient : layout.ambient;
            ApplyShade();
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = layout.skyBehind; }

            // floor (the teleport target), ceiling
            var floorGo = Box(world, "Floor", new Vector3(0f, -0.05f, D / 2f), new Vector3(W + 2 * T, 0.1f, D + 2 * T), floor);
            var teleport = floorGo.AddComponent<TeleportationArea>();
            int mask = InteractionLayerMask.GetMask("Teleport");
            if (mask != 0) teleport.interactionLayers = mask;
            else Debug.LogWarning("[AS] No Teleport interaction layer; the floor uses the default layers.");
            Box(world, "Ceiling", new Vector3(0f, H + 0.05f, D / 2f), new Vector3(W + 2 * T, 0.1f, D + 2 * T), ceiling);

            // walls: south (door), east, west, and the north wall around the window opening
            Box(world, "Wall S", new Vector3(0f, H / 2f, -T / 2f), new Vector3(W + 2 * T, H, T), wall);
            Box(world, "Wall E", new Vector3(W / 2f + T / 2f, H / 2f, D / 2f), new Vector3(T, H, D), wall);
            Box(world, "Wall W", new Vector3(-W / 2f - T / 2f, H / 2f, D / 2f), new Vector3(T, H, D), wall);
            // the north wall round an arched opening, after Window in the Studio: the opening widens into the room (the
            // splayed reveal, pale blue on the left, green on the right), round-arched above the springing line; the
            // casements and the fanlight sit deeper, at the glass plane
            float sL = layout.revealSplayLeft, sR = layout.revealSplayRight, zg = D + T * layout.glassDepth;
            // at the room face the opening runs from -ww/2 - sL to ww/2 + sR: centre cx, half-width ro (S02 shows the
            // green right jamb wide and the blue left one barely, so the splay is uneven)
            float cx = (sR - sL) / 2f, ro = ww / 2f + (sL + sR) / 2f;
            float spring = layout.Springing;                     // where the arch starts
            float xL = -(W / 2f + T), xR = W / 2f + T, zN = D + T / 2f;
            Box(world, "Wall N left", new Vector3((xL + cx - ro) / 2f, H / 2f, zN), new Vector3(cx - ro - xL, H, T), wall);
            Box(world, "Wall N right", new Vector3((cx + ro + xR) / 2f, H / 2f, zN), new Vector3(xR - cx - ro, H, T), wall);
            Box(world, "Wall N below", new Vector3(cx, wb / 2f, zN), new Vector3(2f * ro, wb, T), wall);
            ArchWall(world, "Wall N arch", spring, cx, ro, H, D, D + T, wall);
            Reveal(world, ww, cx, ro, wb, spring, D, zg, revealLeft, revealRight);
            // the sill: pale, as in the painting, with a dark grey-green front edge
            Box(world, "Sill", new Vector3(cx, wb + 0.02f, (D - layout.sillOverhang + zg) / 2f), new Vector3(2f * ro + 0.06f, 0.04f, zg - D + layout.sillOverhang), sillTop != null ? sillTop : wood);
            Box(world, "Sill edge", new Vector3(cx, wb + 0.015f, D - layout.sillOverhang - 0.006f), new Vector3(2f * ro + 0.07f, 0.06f, 0.012f), WF, collider: false);
            // the ceiling's beams, across the room (S03)
            for (int i = 0; i < layout.beamCount; i++)
                Box(world, "Beam", new Vector3(0f, H - layout.beamDepth / 2f, D * (i + 1f) / (layout.beamCount + 1f)), new Vector3(W, layout.beamDepth, layout.beamWidth), frame, collider: false);

            // the sill's five objects (stand-ins), in his order: tall jar, small bottle, tall bottle, glass, cup
            float y0 = wb + 0.04f, zs = (D + zg) / 2f;
            float[] xs = { -0.42f, -0.12f, 0.02f, 0.2f, 0.36f };
            float[] hs = { 0.32f, 0.16f, 0.3f, 0.12f, 0.12f };
            float[] rs = { 0.055f, 0.035f, 0.04f, 0.04f, 0.05f };
            if (sillModel != null) SillStillLife(world, new Vector3(0f, y0, zs), D);   // the Tripo model; the cylinders only without it
            else for (int i = 0; i < 5; i++)
            {
                var c = Prim(world, PrimitiveType.Cylinder, "Sill object " + (i + 1), new Vector3(xs[i], y0 + hs[i] / 2f, zs), new Vector3(rs[i] * 2f, hs[i] / 2f, rs[i] * 2f), frame);
                DestroyImmediate(c.GetComponent<Collider>());
            }

            // furniture after the concept S03 (looking south): the table in the middle, a rush chair at each end facing it;
            // its near edge stays 0.3 m short of the arrival spot (S02's viewpoint, z 2.52)
            Box(world, "Table top", new Vector3(0f, 0.74f, 1.8f), new Vector3(1.3f, 0.05f, 0.8f), wood);
            foreach (var p in new[] { new Vector2(-0.6f, 1.45f), new Vector2(0.6f, 1.45f), new Vector2(-0.6f, 2.15f), new Vector2(0.6f, 2.15f) })
                Box(world, "Table leg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.05f, 0.72f, 0.05f), wood);
            Chair(world, new Vector3(-1.05f, 0f, 1.8f), 90f);
            Chair(world, new Vector3(1.05f, 0f, 1.8f), -90f);
            // on the table: his paint box, lid open, and a pot of brushes
            PaintBox(world, new Vector3(layout.paintBoxSpot.x, TableTop, layout.paintBoxSpot.y), layout.paintBoxYaw);
            BrushPot(world, new Vector3(layout.brushPotSpot.x, TableTop, layout.brushPotSpot.y));

            var compare = world.gameObject.AddComponent<AS_Compare>();
            compare.layout = layout;
            compare.references = references;
            // S03: the door a little east of the middle of the south wall, the easel west of it (both seen facing south)
            Door(world, new Vector3(0.3f, 0f, 0f), 1.0f, 2.15f);   // grey-green, as in S03
            Easel(world, new Vector3(-1.15f, 0f, 0.35f));
            Box(world, "Chest", new Vector3(W / 2f - 0.3f, 0.3f, D - 0.4f), new Vector3(0.5f, 0.6f, 0.6f), wood);
            Box(world, "Boxes", new Vector3(W / 2f - 0.3f, 0.72f, D - 0.4f), new Vector3(0.4f, 0.24f, 0.45f), frame);
            // his works: two each side of the window on the north wall, as in the painting; two on the west wall
            // placed where S02 shows them, seen from its viewpoint (two left of the window, two right)
            if (wallPictures != null && wallPictures.GetTexture("_BaseMap") != null && layout.wallPictures.Length > 0)
                foreach (var pic in layout.wallPictures) WallPicture(world, pic, D);
            else
                foreach (var (x, y, w2, h2) in new[] { (-1.0f, 1.9f, 0.7f, 0.67f), (-0.95f, 1.12f, 0.5f, 0.56f), (1.52f, 2.08f, 0.46f, 0.64f), (1.27f, 1.19f, 0.7f, 0.76f) })
                    Box(world, "Canvas N", new Vector3(x, y, D - 0.02f), new Vector3(w2, h2, 0.03f), canvas);
            foreach (var (z, w2, h2) in new[] { (1.5f, 0.6f, 0.5f), (2.7f, 0.45f, 0.6f) })
                Box(world, "Canvas W", new Vector3(-W / 2f + 0.02f, 1.7f, z), new Vector3(0.03f, h2, w2), canvas);

            // light: morning sun through the window, a little from the east
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(world, false);
            sun.type = LightType.Directional;
            sun.color = layout.sunColour;
            // with flat shading the room ignores lights (AS_Painted); the sun only touches the Tripo props, softly, no shadows
            sun.intensity = layout.flatShading ? layout.flatSunIntensity : layout.sunIntensity;
            sun.shadows = layout.flatShading ? LightShadows.None : LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            // the window's casements
            var win = new GameObject("Window").AddComponent<AS_Window>();
            win.transform.SetParent(world, false);
            win.openDegrees = layout.casementOpenDegrees;
            win.openSeconds = layout.casementOpenSeconds;
            // after the painting: a broad grey-green frame round the opening at the glass, a heavy transom on the
            // springing line, and the two casements inside the frame
            float fw = layout.windowFrameWidth, tr = layout.windowTransom, zc = zg;
            float inner = ww / 2f - fw, bottom = wb + fw, top = spring - tr / 2f, hgt = top - bottom, yc = (top + bottom) / 2f;
            var outer = new GameObject("Frame").transform;
            outer.SetParent(win.transform, false);
            Box(outer, "Frame L", new Vector3(-ww / 2f + fw / 2f, (wb + spring) / 2f, zc), new Vector3(fw, spring - wb, 0.06f), WF, collider: false);
            Box(outer, "Frame R", new Vector3(ww / 2f - fw / 2f, (wb + spring) / 2f, zc), new Vector3(fw, spring - wb, 0.06f), WF, collider: false);
            Box(outer, "Frame bottom", new Vector3(0f, wb + fw / 2f, zc), new Vector3(ww, fw, 0.06f), WF, collider: false);
            Merge(outer);
            Fanlight(win.transform, ww / 2f, spring, zg);
            win.hingeLeft = Casement(win.transform, "Hinge L", new Vector3(-inner, yc, zc), +1f, inner, hgt);
            win.hingeRight = Casement(win.transform, "Hinge R", new Vector3(inner, yc, zc), -1f, inner, hgt);
            win.Wire();

            // the brush on the sill, tip toward the window
            // the brush: standing in one of the sill objects (the cut still life), else lying on the sill
            Vector3 brushAt = new Vector3(0.42f, wb + 0.07f, D + 0.04f);
            Quaternion brushRot = Quaternion.Euler(0f, -60f, 0f);
            if (brushStand.HasValue)
            {
                brushRot = Quaternion.Euler(0f, 200f, 0f) * Quaternion.Euler(-75f, 0f, 0f);   // head up, leaning a little toward the room
                brushAt = brushStand.Value + new Vector3(0f, 0.015f, 0f) + brushRot * new Vector3(0f, 0f, 0.13f);   // the handle's end in the glass
            }
            var brush = Brush(world, brushAt, brushRot, out var tip);
            brushT = brush.transform;
            brushHome = new Pose(brushT.position, brushT.rotation);
            brush.selectExited.AddListener(_ => ReturnBrush());

            // Vincent (stand-in until Vincent v2)
            var v = new GameObject("Vincent (stand-in)").transform;
            v.SetParent(world, false);
            v.SetPositionAndRotation(layout.vincentSpot, Quaternion.Euler(0f, layout.vincentYaw, 0f));
            var body = Prim(v, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.8f, 0f), new Vector3(0.42f, 0.8f, 0.3f), vincent);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var head = Prim(v, PrimitiveType.Sphere, "Head", Vector3.zero, Vector3.one * 0.22f, wood);
            head.transform.localPosition = new Vector3(0f, 1.68f, 0f);
            var letter = Prim(v, PrimitiveType.Cube, "Letter", Vector3.zero, new Vector3(0.21f, 0.27f, 0.005f), ceiling);
            letter.transform.localPosition = new Vector3(0.12f, 1.2f, 0.32f);
            letter.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            DestroyImmediate(letter.GetComponent<Collider>());

            // outside: the painted sky all round, the ground, and the almond tree fitted to the painting
            var sky = Prim(world, PrimitiveType.Sphere, "Sky", layout.WindowCentreOuter, Vector3.one * layout.skyRadius * 2f, skyMaterial);
            DestroyImmediate(sky.GetComponent<Collider>());
            sky.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var ground = Box(world, "Garden ground", new Vector3(0f, -0.06f, D + T + 6f), new Vector3(24f, 0.1f, 12f), groundMaterial, collider: false);
            ground.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var branches = AS_Branches.Load(branchesJson);
            AS_Tree tree = null;
            var guides = new GameObject("Guides").AddComponent<AS_Guides>();
            guides.transform.SetParent(world, false);
            if (layout.showGeneratedTree)
            {
                tree = new GameObject("Almond tree").AddComponent<AS_Tree>();
                tree.transform.SetParent(world, false);
                tree.Build(branches, layout, bark, ink);
                guides.Build(branches, glow, guideDot);
            }
            else guides.Build(new AS_Branch[0], glow, guideDot);   // the brush's beam only

            // the Tripo tree placed by hand in the scene, with its blossoms (Tools > ... > Scatter Blossoms On Selected Tree)
            AS_Blossoms blossoms = null;
            var tripo = FindAnyObjectByType<AS_TripoTree>();
            if (tripo != null && tripo.positions.Length > 0 && blossom != null)
            {
                blossoms = new GameObject("Blossoms").AddComponent<AS_Blossoms>();
                blossoms.transform.SetParent(world, false);
                blossoms.Build(tripo, layout, blossom);
            }
            else if (tripo == null) Debug.Log("[AS] No AS_TripoTree in the scene: no blossoms. Select the Tripo tree and run Tools > InnerFrame > Almond Studio > Scatter Blossoms On Selected Tree.");

            VG_DialogueUI ui = null;
            if (uiPrefab != null)
            {
                ui = Instantiate(uiPrefab, world).GetComponent<VG_DialogueUI>();
                ui.name = "AS_UI";
                ui.settings = uiSettings;
            }

            Game = world.gameObject.AddComponent<AS_Game>();
            Game.layout = layout;
            Game.window = win;
            Game.tree = tree;
            Game.blossoms = blossoms;
            Game.guides = guides;
            Game.brush = brush;
            Game.brushTip = tip;
            Game.ui = ui;
            Game.Branches = branches;
        }

        Transform Casement(Transform parent, string name, Vector3 hingeAt, float dir, float width, float height)
        {
            var hinge = new GameObject(name).transform;
            hinge.SetParent(parent, false);
            hinge.localPosition = hingeAt;
            float bar = layout.windowStile, glazing = layout.windowBar;
            const float depth = 0.045f;
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(hinge, false);
            leaf.localPosition = new Vector3(dir * width / 2f, 0f, 0f);
            Box(leaf, "Top", new Vector3(0f, height / 2f - bar / 2f, 0f), new Vector3(width, bar, depth), WF, collider: false);
            Box(leaf, "Bottom", new Vector3(0f, -height / 2f + bar / 2f, 0f), new Vector3(width, bar, depth), WF, collider: false);
            Box(leaf, "Outer", new Vector3(-dir * (width / 2f - bar / 2f), 0f, 0f), new Vector3(bar, height, depth), WF, collider: false);
            // the meeting stile, heavier: the painting's dark upright down the middle of the window
            Box(leaf, "Inner", new Vector3(dir * (width / 2f - bar * 0.65f), 0f, 0f), new Vector3(bar * 1.3f, height, depth), WF, collider: false);
            for (int k = 1; k <= 3; k++)   // the small panes' bars: four rows
                Box(leaf, "Bar", new Vector3(0f, -height / 2f + height * k / 4f, 0f), new Vector3(width, glazing, depth * 0.8f), WF, collider: false);
            Box(leaf, "Mullion", Vector3.zero, new Vector3(glazing, height, depth * 0.8f), WF, collider: false);
            // one box collider over the whole leaf, for grabbing it (it swings away; the rig is never pushed by it)
            var col = leaf.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(width, height, depth * 2f);
            col.isTrigger = true;
            leaf.gameObject.AddComponent<XRSimpleInteractable>();
            return hinge;
        }

        /// <summary>The wall above the springing line, across the opening's width, with a half-round hole of radius `r` centred at x = `cx`.</summary>
        static void ArchWall(Transform parent, string name, float spring, float cx, float r, float top, float zIn, float zOut, Material m)
        {
            const int n = 24;
            var v = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
            foreach (float z in new[] { zIn, zOut })
            {
                int start = v.Count;
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI * i / n;                                  // from the right (0) over to the left (pi)
                    Vector3 onArc = new Vector3(cx + Mathf.Cos(a) * r, spring + Mathf.Sin(a) * r, z);
                    Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    // the same direction out to the rectangle round the arch (x = +-r, y = top)
                    float tx = Mathf.Abs(dir.x) > 1e-4f ? r / Mathf.Abs(dir.x) : float.MaxValue;
                    float ty = dir.y > 1e-4f ? (top - spring) / dir.y : float.MaxValue;
                    float t = Mathf.Min(tx, ty);
                    Vector3 onBox = new Vector3(cx + dir.x * t, spring + dir.y * t, z);
                    v.Add(onArc); v.Add(onBox);
                    uv.Add(new Vector2(onArc.x, onArc.y)); uv.Add(new Vector2(onBox.x, onBox.y));   // metres, as MetricCube
                }
                for (int i = 0; i < n; i++)
                {
                    int a0 = start + i * 2, b0 = a0 + 2;
                    if (z == zIn) { tri.AddRange(new[] { a0, b0, a0 + 1, b0, b0 + 1, a0 + 1 }); }
                    else { tri.AddRange(new[] { a0, a0 + 1, b0, b0, a0 + 1, b0 + 1 }); }
                }
            }
            // the two corners above the springing line that the fan misses (x beyond the rectangle's corner rays): none, as the
            // rays from the centre reach every point of the rectangle; done
            var mesh = new Mesh { name = name + " (runtime)" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>The splayed reveal: from the glass (width ww, centred on x = 0, at zGlass) out to the room face (half-width
        /// ro, centred on cx, at zRoom); the straight sides up to the springing line and the arch's soffit above, left half
        /// pale blue, right half green.</summary>
        static void Reveal(Transform parent, float ww, float cx, float ro, float bottom, float spring, float zRoom, float zGlass, Material left, Material right)
        {
            const int n = 12;
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var v = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
                float depth = Vector2.Distance(new Vector2(sx * ww / 2f, zGlass), new Vector2(cx + sx * ro, zRoom));
                // the straight jamb (UVs in metres: u from the glass toward the room, v up, then on round the soffit)
                v.Add(new Vector3(sx * ww / 2f, bottom, zGlass)); v.Add(new Vector3(cx + sx * ro, bottom, zRoom));
                v.Add(new Vector3(sx * ww / 2f, spring, zGlass)); v.Add(new Vector3(cx + sx * ro, spring, zRoom));
                uv.Add(new Vector2(0f, bottom)); uv.Add(new Vector2(depth, bottom)); uv.Add(new Vector2(0f, spring)); uv.Add(new Vector2(depth, spring));
                tri.AddRange(new[] { 0, 1, 2, 2, 1, 3 });
                // the soffit's half on this side, from the springing line to the crown
                int s0 = v.Count;
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI / 2f * i / n;                             // 0 at the springing line, pi/2 at the crown
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    v.Add(new Vector3(sx * c * ww / 2f, spring + s * ww / 2f, zGlass));
                    v.Add(new Vector3(cx + sx * c * ro, spring + s * ro, zRoom));
                    float along = spring + a * (ww + ro * 2f) / 4f;      // the arc's length so far, between glass and room
                    uv.Add(new Vector2(0f, along)); uv.Add(new Vector2(depth, along));
                }
                for (int i = 0; i < n; i++)
                {
                    int a0 = s0 + i * 2, b0 = a0 + 2;
                    tri.AddRange(new[] { a0, a0 + 1, b0, b0, a0 + 1, b0 + 1 });
                }
                var mesh = new Mesh { name = (side == 0 ? "Reveal L" : "Reveal R") + " (runtime)" };
                mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject(side == 0 ? "Reveal L (blue)" : "Reveal R (green)");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = side == 0 ? left : right;
            }
        }

        /// <summary>The fixed fanlight in the arch, after the painting: a heavy transom on the springing line, a broad arc
        /// at the frame's width, a half-round hub on the transom's middle and five bars radiating from it.</summary>
        void Fanlight(Transform parent, float r, float spring, float z)
        {
            var f = new GameObject("Fanlight").transform;
            f.SetParent(parent, false);
            float fw = layout.windowFrameWidth, tr = layout.windowTransom, hub = layout.windowHub, ray = layout.windowBar * 1.2f;
            const float depth = 0.06f;
            Box(f, "Transom", new Vector3(0f, spring, z), new Vector3(r * 2f, tr, depth), WF, collider: false);
            const int n = 18;
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n, am = (a0 + a1) / 2f;
                var seg = Box(f, "Arc", new Vector3(Mathf.Cos(am) * (r - fw / 2f), spring + Mathf.Sin(am) * (r - fw / 2f), z),
                              new Vector3(fw, 2f * r * Mathf.Sin((a1 - a0) / 2f) + 0.012f, depth), WF, collider: false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg);
            }
            HalfDisc(f, "Hub", new Vector3(0f, spring + tr / 2f, z), hub, depth, WF);
            float y0 = spring + tr / 2f, len = r - fw - hub;
            for (int k = 1; k <= 5; k++)
            {
                float a = Mathf.PI * k / 6f, mid = hub + len / 2f;
                var b = Box(f, "Ray", new Vector3(Mathf.Cos(a) * mid, y0 + Mathf.Sin(a) * mid, z), new Vector3(ray, len + 0.01f, depth * 0.8f), WF, collider: false);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            }
            Merge(f);
        }

        /// <summary>A half-round slab standing on its flat edge (the fanlight's hub): centre of the flat edge at `at`, facing z.</summary>
        static void HalfDisc(Transform parent, string name, Vector3 at, float r, float depth, Material m)
        {
            const int n = 12;
            var v = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
            foreach (float dz in new[] { -depth / 2f, depth / 2f })
            {
                int c0 = v.Count;
                v.Add(new Vector3(0f, 0f, dz)); uv.Add(Vector2.zero);
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI * i / n;
                    v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, dz)); uv.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
                for (int i = 0; i < n; i++)
                    if (dz < 0f) tri.AddRange(new[] { c0, c0 + 1 + i + 1, c0 + 1 + i });   // faces -z (into the room)
                    else tri.AddRange(new[] { c0, c0 + 1 + i, c0 + 1 + i + 1 });
            }
            int back = n + 2;   // the second ring starts here
            for (int i = 0; i < n; i++)   // the curved rim
            {
                int a0 = 1 + i, a1 = 2 + i, b0 = back + 1 + i, b1 = back + 2 + i;
                tri.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
            var mesh = new Mesh { name = name + " (runtime)" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>The window's frame material (lighter grey-green, as painted); the old frame material if it is missing.</summary>
        Material WF => windowFrame != null ? windowFrame : frame;

        XRGrabInteractable Brush(Transform parent, Vector3 at, Quaternion rot, out Transform tip)
        {
            var root = new GameObject("Brush");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(at, rot);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // the handle along +z, the head at the front; held at the back of the handle
            var handle = Prim(root.transform, PrimitiveType.Cylinder, "Handle", Vector3.zero, new Vector3(0.016f, 0.13f, 0.016f), wood);
            handle.transform.localPosition = new Vector3(0f, 0f, 0f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            DestroyImmediate(handle.GetComponent<Collider>());
            var head = Prim(root.transform, PrimitiveType.Cube, "Head (flat bristles)", Vector3.zero, new Vector3(0.022f, 0.008f, 0.04f), brushHead);
            head.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            DestroyImmediate(head.GetComponent<Collider>());
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.025f;
            col.height = 0.34f;
            col.center = new Vector3(0f, 0f, 0.02f);
            var attach = new GameObject("Attach").transform;
            attach.SetParent(root.transform, false);
            attach.localPosition = new Vector3(0f, 0f, -0.08f);
            tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = new Vector3(0f, 0f, 0.17f);
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.attachTransform = attach;
            return grab;
        }

        void ReturnBrush()
        {
            if (brushT != null) brushT.SetPositionAndRotation(brushHome.position, brushHome.rotation);
        }

        void Chair(Transform parent, Vector3 at, float yaw)
        {
            if (chairPrefab != null)
            {
                var ch = Instantiate(chairPrefab, parent);
                ch.name = "Chair (Bedroom)";
                ch.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw + layout.chairYawOffset, 0f));
                return;
            }
            var c = new GameObject("Chair").transform;
            c.SetParent(parent, false);
            c.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            Box(c, "Seat", new Vector3(0f, 0.44f, 0f), new Vector3(0.42f, 0.04f, 0.4f), wood);
            Box(c, "Back", new Vector3(0f, 0.7f, -0.19f), new Vector3(0.42f, 0.5f, 0.03f), wood);
            foreach (var p in new[] { new Vector2(-0.18f, -0.17f), new Vector2(0.18f, -0.17f), new Vector2(-0.18f, 0.17f), new Vector2(0.18f, 0.17f) })
                Box(c, "Leg", new Vector3(p.x, 0.21f, p.y), new Vector3(0.03f, 0.42f, 0.03f), wood, collider: false);
        }

        /// <summary>The Tripo still life on the sill: turned by AS_Layout.sillModelEuler, scaled so its tallest object (the
        /// lidded jar) is sillModelHeight tall, its feet on the sill and its middle at `at` + sillModelOffset. With flat
        /// shading its texture goes on an AS_Painted copy, so it reads like the room.</summary>
        void SillStillLife(Transform parent, Vector3 at, float roomFace)
        {
            var root = new GameObject("Sill still life").transform;
            root.SetParent(parent, false);
            var m = Instantiate(sillModel, root);
            m.name = sillModel.name;
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.Euler(layout.sillModelEuler);
            m.transform.localScale = Vector3.one;
            var rs = m.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) { Debug.LogWarning("[AS] The sill model has no renderer."); return; }
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            // the model's height should be the middle of its three sizes (the row is wider, a jar is less deep)
            var e = b.size;
            if (e.y < e.x && e.y < e.z || e.y > e.x && e.y > e.z)
                Debug.LogWarning($"[AS] The sill model is {e.x:0.00} x {e.y:0.00} x {e.z:0.00} (x y z) before scaling: it may be lying down or turned. Change AS_Layout.sillModelEuler (try X 270 or X 90) and Play again.");
            float k = e.y > 1e-5f ? layout.sillModelHeight / e.y : 1f;
            m.transform.localScale = Vector3.one * k;
            // after scaling: feet on the sill, centred
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 target = at + layout.sillModelOffset;
            m.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
            if (layout.flatShading && wall != null)
                foreach (var r in rs)
                {
                    var src = r.sharedMaterials;
                    var dst = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        dst[i] = new Material(wall.shader) { name = (src[i] != null ? src[i].name : "Sill") + " (painted)" };
                        var tex = src[i] != null ? src[i].mainTexture : null;
                        if (tex != null) dst[i].SetTexture("_BaseMap", tex);
                        dst[i].SetColor("_BaseColor", Color.white);
                    }
                    r.sharedMaterials = dst;
                }
            Debug.Log($"[AS] Sill still life: {sillModel.name}, scaled x{k:0.000}, now {b.size.x:0.00} m wide, {b.size.y:0.00} m tall, {b.size.z:0.00} m deep.");
            if (layout.sillSplit && SplitSill(root, m, at.y, roomFace)) Destroy(m);
        }

        /// <summary>Cuts the still life into its objects where there are gaps across x (sillSplitGap), and stands each on the
        /// sill at its own AS_Layout.sillItemSpots (x across, z from the room face) and sillItemYaw, as in the painting.
        /// False (and the model stays whole) if the mesh can't be read or the cut doesn't give one piece per spot.</summary>
        bool SplitSill(Transform root, GameObject model, float sillY, float roomFace)
        {
            var mf = model.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            var mesh = mf.sharedMesh;
            if (!mesh.isReadable) { Debug.LogWarning("[AS] The sill model's mesh isn't readable (Read/Write off): run Build, which turns it on. It stays in one piece."); return false; }
            var toRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var src = mesh.vertices; var srcN = mesh.normals; var srcUv = mesh.uv;
            var p = new Vector3[src.Length];
            var nrm = srcN.Length == src.Length ? new Vector3[src.Length] : null;
            for (int i = 0; i < src.Length; i++)
            {
                p[i] = toRoot.MultiplyPoint3x4(src[i]);
                if (nrm != null) nrm[i] = toRoot.MultiplyVector(srcN[i]).normalized;
            }
            int subs = mesh.subMeshCount;
            var tris = new List<(int sub, int a, int b, int c, float x)>();
            for (int s = 0; s < subs; s++)
            {
                var t = mesh.GetTriangles(s);
                for (int k = 0; k + 2 < t.Length; k += 3)
                    tris.Add((s, t[k], t[k + 1], t[k + 2], (p[t[k]].x + p[t[k + 1]].x + p[t[k + 2]].x) / 3f));
            }
            if (tris.Count == 0) return false;
            tris.Sort((u, w) => u.x.CompareTo(w.x));
            var starts = new List<int> { 0 };
            for (int k = 1; k < tris.Count; k++)
                if (tris[k].x - tris[k - 1].x > layout.sillSplitGap) starts.Add(k);
            int n = layout.sillItemSpots.Length;
            if (starts.Count != n)
            {
                Debug.LogWarning($"[AS] The sill model cuts into {starts.Count} pieces across x, not {n}: it stays one piece. Try another AS_Layout.sillSplitGap (now {layout.sillSplitGap:0.000} m; smaller finds more gaps).");
                return false;
            }
            starts.Add(tris.Count);
            var mats = mf.GetComponent<MeshRenderer>().sharedMaterials;
            for (int c = 0; c < n; c++)
            {
                var map = new Dictionary<int, int>();
                var v = new List<Vector3>(); var vn = new List<Vector3>(); var uv = new List<Vector2>();
                var perSub = new List<int>[subs];
                for (int s = 0; s < subs; s++) perSub[s] = new List<int>();
                for (int k = starts[c]; k < starts[c + 1]; k++)
                {
                    var t = tris[k];
                    foreach (int idx in new[] { t.a, t.b, t.c })
                    {
                        if (!map.TryGetValue(idx, out int j))
                        {
                            j = v.Count; map[idx] = j; v.Add(p[idx]);
                            if (nrm != null) vn.Add(nrm[idx]);
                            if (srcUv.Length == src.Length) uv.Add(srcUv[idx]);
                        }
                        perSub[t.sub].Add(j);
                    }
                }
                // stand the piece's own feet on its origin
                Vector3 lo = v[0], hi = v[0];
                foreach (var q in v) { lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                var foot = new Vector3((lo.x + hi.x) / 2f, lo.y, (lo.z + hi.z) / 2f);
                for (int i = 0; i < v.Count; i++) v[i] -= foot;
                var piece = new Mesh { name = $"Sill object {c + 1} (runtime)" };
                if (v.Count > 65000) piece.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                piece.SetVertices(v);
                if (uv.Count == v.Count) piece.SetUVs(0, uv);
                piece.subMeshCount = subs;
                for (int s = 0; s < subs; s++) piece.SetTriangles(perSub[s], s);
                if (vn.Count == v.Count) piece.SetNormals(vn); else piece.RecalculateNormals();
                piece.RecalculateBounds();
                var go = new GameObject($"Sill object {c + 1}");
                go.transform.SetParent(root, false);
                var spot = layout.sillItemSpots[c];
                float yaw = c < layout.sillItemYaw.Length ? layout.sillItemYaw[c] : 0f;
                go.transform.SetLocalPositionAndRotation(new Vector3(spot.x, sillY, roomFace + spot.y), Quaternion.Euler(0f, yaw, 0f));
                go.AddComponent<MeshFilter>().sharedMesh = piece;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                if (c == layout.brushStandsIn) brushStand = go.transform.position;
            }
            Debug.Log($"[AS] Sill still life cut into {n} pieces and placed as in the painting (AS_Layout.sillItemSpots).");
            return true;
        }

        Vector3? brushStand;   // where the brush stands, if the still life was cut (SplitSill)

        /// <summary>A picture pinned on the north wall, showing its own part of the painting: a 2 cm slab whose front face
        /// maps the four corners in AS_Layout.wallPictures onto AS_WindowInTheStudio.jpg; its width follows that part's shape.</summary>
        void WallPicture(Transform parent, AS_WallPicture pic, float wallFace)
        {
            var tex = wallPictures.GetTexture("_BaseMap");
            // the corners in texture UVs (v up) and the part's shape in the painting's pixels
            Vector2 Uv(Vector2 f) => new Vector2(f.x, 1f - f.y);
            Vector2[] uvc = { Uv(pic.topLeft), Uv(pic.topRight), Uv(pic.bottomRight), Uv(pic.bottomLeft) };
            float pw = ((pic.topRight - pic.topLeft).magnitude + (pic.bottomRight - pic.bottomLeft).magnitude) / 2f * tex.width;
            float ph = ((pic.bottomLeft - pic.topLeft).magnitude + (pic.bottomRight - pic.topRight).magnitude) / 2f * tex.height;
            float h = pic.height, w = ph > 1f ? h * pw / ph : h, t = 0.02f;
            // the face toward the room (-z), corners clockwise as seen from the room, then the four edges back to the wall
            Vector3[] front = { new Vector3(-w / 2f, h / 2f, -t), new Vector3(w / 2f, h / 2f, -t), new Vector3(w / 2f, -h / 2f, -t), new Vector3(-w / 2f, -h / 2f, -t) };
            var v = new List<Vector3>(front); var uv = new List<Vector2>(uvc); var tri = new List<int> { 0, 1, 2, 0, 2, 3 };
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4, b = v.Count;
                v.Add(front[i] + new Vector3(0f, 0f, t)); v.Add(front[j] + new Vector3(0f, 0f, t)); v.Add(front[j]); v.Add(front[i]);
                uv.Add(uvc[i]); uv.Add(uvc[j]); uv.Add(uvc[j]); uv.Add(uvc[i]);
                tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = pic.name + " (runtime)" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Picture: " + pic.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pic.centre.x, pic.centre.y, wallFace);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = wallPictures;
        }

        const float TableTop = 0.765f;   // the table top's upper face (0.74 + half its 0.05)

        /// <summary>The door in the south wall: a casing on the wall, the leaf, raised mouldings round
        /// layout.doorPanels panels, and a knob; merged into one mesh with one collider.</summary>
        void Door(Transform parent, Vector3 at, float dw, float dh)
        {
            var d = new GameObject("Door").transform;
            d.SetParent(parent, false);
            d.localPosition = at;                               // x across, y up, z into the room; the wall's face is z = 0
            const float casing = 0.07f, leafDepth = 0.03f, bead = 0.025f, proud = 0.012f;
            const float stile = 0.12f, topRail = 0.12f, bottomRail = 0.22f, midRail = 0.1f;
            Box(d, "Leaf", new Vector3(0f, dh / 2f, leafDepth / 2f), new Vector3(dw, dh, leafDepth), frame, collider: false);
            Box(d, "Casing L", new Vector3(-dw / 2f - casing / 2f, (dh + casing) / 2f, 0.02f), new Vector3(casing, dh + casing, 0.04f), frame, collider: false);
            Box(d, "Casing R", new Vector3(dw / 2f + casing / 2f, (dh + casing) / 2f, 0.02f), new Vector3(casing, dh + casing, 0.04f), frame, collider: false);
            Box(d, "Casing top", new Vector3(0f, dh + casing / 2f, 0.02f), new Vector3(dw + 2f * casing, casing, 0.04f), frame, collider: false);
            int cols = Mathf.Max(1, layout.doorPanels.x), rows = Mathf.Max(1, layout.doorPanels.y);
            float pw = (dw - 2f * stile - (cols - 1) * midRail) / cols, ph = (dh - topRail - bottomRail - (rows - 1) * midRail) / rows;
            float z = leafDepth + proud / 2f;
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    float x = -dw / 2f + stile + pw / 2f + c * (pw + midRail), y = bottomRail + ph / 2f + r * (ph + midRail);
                    Box(d, "Bead", new Vector3(x, y + ph / 2f - bead / 2f, z), new Vector3(pw, bead, proud), frame, collider: false);
                    Box(d, "Bead", new Vector3(x, y - ph / 2f + bead / 2f, z), new Vector3(pw, bead, proud), frame, collider: false);
                    Box(d, "Bead", new Vector3(x - pw / 2f + bead / 2f, y, z), new Vector3(bead, ph - 2f * bead, proud), frame, collider: false);
                    Box(d, "Bead", new Vector3(x + pw / 2f - bead / 2f, y, z), new Vector3(bead, ph - 2f * bead, proud), frame, collider: false);
                }
            Box(d, "Knob", new Vector3(dw / 2f - 0.08f, 1.0f, leafDepth + 0.025f), new Vector3(0.035f, 0.035f, 0.05f), wood, collider: false);
            Merge(d);
            var col = d.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, dh / 2f, leafDepth / 2f);
            col.size = new Vector3(dw, dh, leafDepth);
        }

        /// <summary>His paint box on the table: a shallow wooden tray with its lid open past upright and a row of tin tubes.</summary>
        void PaintBox(Transform parent, Vector3 at, float yaw)
        {
            var g = new GameObject("Paint box").transform;
            g.SetParent(parent, false);
            g.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            const float w = 0.36f, dp = 0.26f, h = 0.055f, t = 0.01f;
            Box(g, "Bottom", new Vector3(0f, t / 2f, 0f), new Vector3(w, t, dp), wood, collider: false);
            Box(g, "Side F", new Vector3(0f, h / 2f, dp / 2f - t / 2f), new Vector3(w, h, t), wood, collider: false);
            Box(g, "Side B", new Vector3(0f, h / 2f, -dp / 2f + t / 2f), new Vector3(w, h, t), wood, collider: false);
            Box(g, "Side L", new Vector3(-w / 2f + t / 2f, h / 2f, 0f), new Vector3(t, h, dp - 2f * t), wood, collider: false);
            Box(g, "Side R", new Vector3(w / 2f - t / 2f, h / 2f, 0f), new Vector3(t, h, dp - 2f * t), wood, collider: false);
            // the lid, hinged on the back's top edge and swung up and a little back
            var hinge = new GameObject("Lid hinge").transform;
            hinge.SetParent(g, false);
            hinge.SetLocalPositionAndRotation(new Vector3(0f, h, -dp / 2f), Quaternion.Euler(-105f, 0f, 0f));
            Box(hinge, "Lid", new Vector3(0f, t / 2f, dp / 2f), new Vector3(w, t, dp), wood, collider: false);
            // the tubes, lying across the tray, each with a dark cap
            var tin = new Material(brushHead) { name = "AS_Tin (runtime)" };
            tin.SetColor("_BaseColor", new Color(0.74f, 0.74f, 0.7f));
            for (int i = 0; i < 6; i++)
            {
                float x = -0.14f + i * 0.055f, turn = (i % 3 - 1) * 6f;
                var tube = new GameObject("Tube").transform;
                tube.SetParent(g, false);
                tube.SetLocalPositionAndRotation(new Vector3(x, t + 0.011f, 0.01f), Quaternion.Euler(90f, 0f, turn));
                Prim(tube, PrimitiveType.Cylinder, "Body", Vector3.zero, new Vector3(0.022f, 0.045f, 0.022f), tin);
                Prim(tube, PrimitiveType.Cylinder, "Cap", new Vector3(0f, 0.052f, 0f), new Vector3(0.012f, 0.008f, 0.012f), frame);
            }
            Merge(g);
        }

        /// <summary>An earthenware pot on the table with five brushes standing in it, leaning out.</summary>
        void BrushPot(Transform parent, Vector3 at)
        {
            var p = new GameObject("Brush pot").transform;
            p.SetParent(parent, false);
            p.localPosition = at;
            Prim(p, PrimitiveType.Cylinder, "Pot", new Vector3(0f, 0.06f, 0f), new Vector3(0.09f, 0.06f, 0.09f), ceiling);
            var tin = new Material(brushHead) { name = "AS_Ferrule (runtime)" };
            tin.SetColor("_BaseColor", new Color(0.62f, 0.6f, 0.55f));
            for (int i = 0; i < 5; i++)
            {
                var b = new GameObject("Brush").transform;
                b.SetParent(p, false);
                float lean = 8f + (i % 3) * 3f;
                b.SetLocalPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.Euler(0f, 15f + i * 72f, 0f) * Quaternion.Euler(lean, 0f, 0f));
                float len = 0.24f + (i % 2) * 0.04f;
                Prim(b, PrimitiveType.Cylinder, "Handle", new Vector3(0f, len / 2f, 0f), new Vector3(0.008f, len / 2f, 0.008f), wood);
                Prim(b, PrimitiveType.Cylinder, "Ferrule", new Vector3(0f, len + 0.012f, 0f), new Vector3(0.009f, 0.012f, 0.009f), tin);
                Prim(b, PrimitiveType.Cube, "Bristles", new Vector3(0f, len + 0.04f, 0f), new Vector3(0.007f + i * 0.002f, 0.03f, 0.004f), brushHead);
            }
            Merge(p);
        }

        /// <summary>Merges every mesh under `group` into one mesh on `group` (one submesh per material) and removes the
        /// children with their colliders: one renderer instead of dozens, for the headset's draw calls.</summary>
        static void Merge(Transform group)
        {
            var toLocal = group.worldToLocalMatrix;
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            foreach (var f in group.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.transform == group || f.sharedMesh == null) continue;
                var m = f.GetComponent<MeshRenderer>().sharedMaterial;
                if (!byMaterial.TryGetValue(m, out var list)) byMaterial[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = f.sharedMesh, transform = toLocal * f.transform.localToWorldMatrix });
            }
            var parts = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var kv in byMaterial)
            {
                var sub = new Mesh();
                sub.CombineMeshes(kv.Value.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
                materials.Add(kv.Key);
            }
            var mesh = new Mesh { name = group.name + " (merged)" };
            mesh.CombineMeshes(parts.ToArray(), false, true);
            foreach (var part in parts) Destroy(part.mesh);
            mesh.RecalculateBounds();
            for (int i = group.childCount - 1; i >= 0; i--) DestroyImmediate(group.GetChild(i).gameObject);
            group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            group.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
        }

        void Easel(Transform parent, Vector3 at)
        {
            if (easelModel != null)
            {
                // Vincent's easel (Cici's Tripo model): origin between its feet, ledge toward +z, i.e. into the room here
                var model = Instantiate(easelModel, parent);
                model.name = "Easel (Vincent's)";
                model.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, layout.easelYaw, 0f));
                var r = model.GetComponent<Renderer>();
                if (r != null && easelMaterial != null) r.sharedMaterial = easelMaterial;
                var blank = model.transform.Find("VG_Canvas");
                if (blank != null) blank.gameObject.SetActive(layout.easelCanvas);
                return;
            }
            var e = new GameObject("Easel").transform;
            e.SetParent(parent, false);
            e.SetPositionAndRotation(at, Quaternion.identity);   // its ledge toward the room, as S03 shows it
            foreach (var x in new[] { -0.25f, 0.25f })
            {
                var leg = Box(e, "Leg", new Vector3(x, 0.85f, 0f), new Vector3(0.04f, 1.75f, 0.04f), wood);
                leg.transform.localRotation = Quaternion.Euler(-8f, 0f, x > 0 ? 6f : -6f);   // tops lean in (an A, not a V)
            }
            var back = Box(e, "Back leg", new Vector3(0f, 0.8f, -0.25f), new Vector3(0.04f, 1.65f, 0.04f), wood);
            back.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            Box(e, "Ledge", new Vector3(0f, 0.8f, 0.07f), new Vector3(0.6f, 0.04f, 0.06f), wood);
        }

        static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material m, bool collider = true)
        {
            var go = Prim(parent, PrimitiveType.Cube, name, at, size, m);
            go.GetComponent<MeshFilter>().sharedMesh = MetricCube(size);
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static readonly Dictionary<Vector3, Mesh> metricCubes = new Dictionary<Vector3, Mesh>();

        /// <summary>A unit cube like Unity's, but with UVs in metres for a box of this size, so a painted surface keeps its
        /// stroke size on a long wall and a short leg alike (the material's tiling says how many metres one repeat is).
        /// Side faces run u along the face and v up; top and bottom run u along x and v along z.</summary>
        static Mesh MetricCube(Vector3 size)
        {
            if (metricCubes.TryGetValue(size, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nn in normals)
            {
                // the face's two in-plane axes: u and v (v is up on the side faces)
                Vector3 ua = nn.y != 0f ? Vector3.right : (nn.x != 0f ? Vector3.forward : Vector3.right);
                Vector3 va = nn.y != 0f ? Vector3.forward : Vector3.up;
                float us = Vector3.Scale(ua, size).magnitude, vs = Vector3.Scale(va, size).magnitude;
                int b = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float su = (k == 1 || k == 2) ? 0.5f : -0.5f, sv = k >= 2 ? 0.5f : -0.5f;
                    v.Add(nn * 0.5f + ua * su + va * sv);
                    n.Add(nn);
                    uv.Add(new Vector2((su + 0.5f) * us, (sv + 0.5f) * vs));
                }
                // wind so the front is on the normal's side
                bool front = Vector3.Dot(Vector3.Cross(v[b + 1] - v[b], v[b + 2] - v[b]), nn) > 0f;
                if (front) tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                else tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = $"AS box {size.x:0.##}x{size.y:0.##}x{size.z:0.##}" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            metricCubes[size] = mesh;
            return mesh;
        }

        static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static readonly int ShadeDirId = Shader.PropertyToID("_AS_ShadeDir"), ShadeMinId = Shader.PropertyToID("_AS_ShadeMin");

        /// <summary>Hands AS_Layout's shade direction and depth to AS_Painted (globals, so every painted material follows).</summary>
        void ApplyShade()
        {
            Vector3 d = layout.shadeDirection.sqrMagnitude > 1e-6f ? layout.shadeDirection.normalized : Vector3.up;
            Shader.SetGlobalVector(ShadeDirId, new Vector4(d.x, d.y, d.z, 0f));
            Shader.SetGlobalFloat(ShadeMinId, layout.shadeMin);
        }

        // ------------------------------------------------------------------------------------------------ Editor keys

        void Update()
        {
#if UNITY_EDITOR
            ApplyShade();   // so shadeDirection and shadeMin can be tuned in AS_Layout during Play
#endif
            if (XRSettings.isDeviceActive) return;
            var kb = Keyboard.current;
            var cam = Camera.main;
            if (kb == null || cam == null || Game == null) return;
            if (kb.oKey.wasPressedThisFrame) Game.window.Open();
            if (kb.nKey.wasPressedThisFrame) Game.FinishNextBranch();
            if (kb.rKey.wasPressedThisFrame) { Game.BrushHeldInEditor = false; ReturnBrush(); brushT.SetParent(Game.transform, true); Game.Begin(); }
            if (kb.gKey.wasPressedThisFrame)
            {
                if (Game.BrushHeldInEditor)
                {
                    Game.BrushHeldInEditor = false;
                    brushT.SetParent(Game.transform, true);
                    ReturnBrush();
                }
                else if (Vector3.Distance(cam.transform.position, brushT.position) <= layout.editorGrabReach)
                {
                    Game.BrushHeldInEditor = true;
                    brushT.SetParent(cam.transform, false);
                    brushT.localPosition = layout.editorHoldOffset;
                    brushT.localRotation = Quaternion.identity;
                }
            }
        }
    }
}
