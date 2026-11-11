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
        public Material wall, floor, ceiling, wood, frame, revealLeft, revealRight, canvas, brushHead, vincent, bark, ink, skyMaterial, groundMaterial, glow, guideDot, blossom;
        [Tooltip("VG_DialogueUI.prefab: Vincent's words and the hint, as in every scene.")] public GameObject uiPrefab;
        public VG_DialogueSettings uiSettings;
        [Tooltip("The comparison pictures, one per AS_Layout.compareViews entry (Textures/Reference), set by Build.")] public Texture[] references = new Texture[0];
        [Tooltip("The Bedroom's chair, saved for this scene by AS_Setup (Prefabs/AS_Chair.prefab). Boxes if missing.")] public GameObject chairPrefab;

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
            RenderSettings.ambientLight = layout.ambient;
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
            float sp = layout.revealSplay, zg = D + T * layout.glassDepth;
            float wo = ww + 2f * sp;                             // the opening's width at the room face
            float spring = layout.Springing;                     // where the arch starts
            float side = (W + 2 * T - wo) / 2f, zN = D + T / 2f;
            Box(world, "Wall N left", new Vector3(-(wo / 2f + side / 2f), H / 2f, zN), new Vector3(side, H, T), wall);
            Box(world, "Wall N right", new Vector3(wo / 2f + side / 2f, H / 2f, zN), new Vector3(side, H, T), wall);
            Box(world, "Wall N below", new Vector3(0f, wb / 2f, zN), new Vector3(wo, wb, T), wall);
            ArchWall(world, "Wall N arch", spring, wo / 2f, H, D, D + T, wall);
            Reveal(world, ww, wo, wb, spring, D, zg, revealLeft, revealRight);
            Box(world, "Sill", new Vector3(0f, wb + 0.02f, (D - layout.sillOverhang + zg) / 2f), new Vector3(wo + 0.06f, 0.04f, zg - D + layout.sillOverhang), wood);

            // the sill's five objects (stand-ins), in his order: tall jar, small bottle, tall bottle, glass, cup
            float y0 = wb + 0.04f, zs = (D + zg) / 2f;
            float[] xs = { -0.42f, -0.12f, 0.02f, 0.2f, 0.36f };
            float[] hs = { 0.32f, 0.16f, 0.3f, 0.12f, 0.12f };
            float[] rs = { 0.055f, 0.035f, 0.04f, 0.04f, 0.05f };
            for (int i = 0; i < 5; i++)
            {
                var c = Prim(world, PrimitiveType.Cylinder, "Sill object " + (i + 1), new Vector3(xs[i], y0 + hs[i] / 2f, zs), new Vector3(rs[i] * 2f, hs[i] / 2f, rs[i] * 2f), frame);
                DestroyImmediate(c.GetComponent<Collider>());
            }

            // furniture from the floor plan: table and two rush chairs in the middle, door and easel south, chest east
            // the table along the west side, its chairs at either end, so the way from the arrival spot to the window is clear
            Box(world, "Table top", new Vector3(-0.8f, 0.74f, 1.9f), new Vector3(0.75f, 0.05f, 1.1f), wood);
            foreach (var p in new[] { new Vector2(-1.1f, 1.45f), new Vector2(-0.5f, 1.45f), new Vector2(-1.1f, 2.35f), new Vector2(-0.5f, 2.35f) })
                Box(world, "Table leg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.05f, 0.72f, 0.05f), wood);
            Chair(world, new Vector3(-0.8f, 0f, 1.05f), 0f);
            Chair(world, new Vector3(-0.8f, 0f, 2.75f), 180f);

            var compare = world.gameObject.AddComponent<AS_Compare>();
            compare.layout = layout;
            compare.references = references;
            Box(world, "Door", new Vector3(-0.6f, 1.05f, 0.02f), new Vector3(0.9f, 2.1f, 0.05f), wood);
            Easel(world, new Vector3(0.9f, 0f, 0.35f));
            Box(world, "Chest", new Vector3(W / 2f - 0.3f, 0.3f, D - 0.4f), new Vector3(0.5f, 0.6f, 0.6f), wood);
            Box(world, "Boxes", new Vector3(W / 2f - 0.3f, 0.72f, D - 0.4f), new Vector3(0.4f, 0.24f, 0.45f), frame);
            // his works: two each side of the window on the north wall, as in the painting; two on the west wall
            foreach (var (x, y, w2, h2) in new[] { (-1.35f, 2.25f, 0.55f, 0.45f), (-1.4f, 1.35f, 0.38f, 0.5f), (1.2f, 2.3f, 0.32f, 0.4f), (1.3f, 1.6f, 0.55f, 0.42f) })
                Box(world, "Canvas N", new Vector3(x, y, D - 0.02f), new Vector3(w2, h2, 0.03f), canvas);
            foreach (var (z, w2, h2) in new[] { (1.5f, 0.6f, 0.5f), (2.7f, 0.45f, 0.6f) })
                Box(world, "Canvas W", new Vector3(-W / 2f + 0.02f, 1.7f, z), new Vector3(0.03f, h2, w2), canvas);

            // light: morning sun through the window, a little from the east
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(world, false);
            sun.type = LightType.Directional;
            sun.color = layout.sunColour;
            sun.intensity = layout.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            // the window's casements
            var win = new GameObject("Window").AddComponent<AS_Window>();
            win.transform.SetParent(world, false);
            win.openDegrees = layout.casementOpenDegrees;
            win.openSeconds = layout.casementOpenSeconds;
            float zc = zg, hgt = spring - wb - 0.04f, yc = (wb + spring) / 2f + 0.02f;
            Fanlight(win.transform, ww / 2f, spring, zg);
            win.hingeLeft = Casement(win.transform, "Hinge L", new Vector3(-ww / 2f, yc, zc), +1f, ww / 2f, hgt);
            win.hingeRight = Casement(win.transform, "Hinge R", new Vector3(ww / 2f, yc, zc), -1f, ww / 2f, hgt);
            win.Wire();

            // the brush on the sill, tip toward the window
            var brush = Brush(world, new Vector3(0.42f, wb + 0.07f, D + 0.04f), Quaternion.Euler(0f, -60f, 0f), out var tip);
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
            const float bar = 0.045f, depth = 0.04f;
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(hinge, false);
            leaf.localPosition = new Vector3(dir * width / 2f, 0f, 0f);
            Box(leaf, "Top", new Vector3(0f, height / 2f - bar / 2f, 0f), new Vector3(width, bar, depth), frame, collider: false);
            Box(leaf, "Bottom", new Vector3(0f, -height / 2f + bar / 2f, 0f), new Vector3(width, bar, depth), frame, collider: false);
            Box(leaf, "Outer", new Vector3(-dir * (width / 2f - bar / 2f), 0f, 0f), new Vector3(bar, height, depth), frame, collider: false);
            Box(leaf, "Inner", new Vector3(dir * (width / 2f - bar / 2f), 0f, 0f), new Vector3(bar, height, depth), frame, collider: false);
            for (int k = 1; k <= 3; k++)   // the small panes' bars
                Box(leaf, "Bar", new Vector3(0f, -height / 2f + height * k / 4f, 0f), new Vector3(width, 0.022f, depth * 0.8f), frame, collider: false);
            Box(leaf, "Mullion", Vector3.zero, new Vector3(0.022f, height, depth * 0.8f), frame, collider: false);
            // one box collider over the whole leaf, for grabbing it (it swings away; the rig is never pushed by it)
            var col = leaf.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(width, height, depth * 2f);
            col.isTrigger = true;
            leaf.gameObject.AddComponent<XRSimpleInteractable>();
            return hinge;
        }

        /// <summary>The wall above the springing line, across the opening's width, with a half-round hole of radius `r`.</summary>
        static void ArchWall(Transform parent, string name, float spring, float r, float top, float zIn, float zOut, Material m)
        {
            const int n = 24;
            var v = new List<Vector3>(); var tri = new List<int>();
            foreach (float z in new[] { zIn, zOut })
            {
                int start = v.Count;
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI * i / n;                                  // from the right (0) over to the left (pi)
                    Vector3 onArc = new Vector3(Mathf.Cos(a) * r, spring + Mathf.Sin(a) * r, z);
                    Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    // the same direction out to the rectangle round the arch (x = +-r, y = top)
                    float tx = Mathf.Abs(dir.x) > 1e-4f ? r / Mathf.Abs(dir.x) : float.MaxValue;
                    float ty = dir.y > 1e-4f ? (top - spring) / dir.y : float.MaxValue;
                    float t = Mathf.Min(tx, ty);
                    Vector3 onBox = new Vector3(dir.x * t, spring + dir.y * t, z);
                    v.Add(onArc); v.Add(onBox);
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
            mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>The splayed reveal: from the glass (width ww, at zGlass) out to the room face (width wo, at zRoom); the
        /// straight sides up to the springing line and the arch's soffit above, left half pale blue, right half green.</summary>
        static void Reveal(Transform parent, float ww, float wo, float bottom, float spring, float zRoom, float zGlass, Material left, Material right)
        {
            const int n = 12;
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var v = new List<Vector3>(); var tri = new List<int>();
                // the straight jamb
                v.Add(new Vector3(sx * ww / 2f, bottom, zGlass)); v.Add(new Vector3(sx * wo / 2f, bottom, zRoom));
                v.Add(new Vector3(sx * ww / 2f, spring, zGlass)); v.Add(new Vector3(sx * wo / 2f, spring, zRoom));
                tri.AddRange(new[] { 0, 1, 2, 2, 1, 3 });
                // the soffit's half on this side, from the springing line to the crown
                int s0 = v.Count;
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI / 2f * i / n;                             // 0 at the springing line, pi/2 at the crown
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    v.Add(new Vector3(sx * c * ww / 2f, spring + s * ww / 2f, zGlass));
                    v.Add(new Vector3(sx * c * wo / 2f, spring + s * wo / 2f, zRoom));
                }
                for (int i = 0; i < n; i++)
                {
                    int a0 = s0 + i * 2, b0 = a0 + 2;
                    tri.AddRange(new[] { a0, a0 + 1, b0, b0, a0 + 1, b0 + 1 });
                }
                var mesh = new Mesh { name = (side == 0 ? "Reveal L" : "Reveal R") + " (runtime)" };
                mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject(side == 0 ? "Reveal L (blue)" : "Reveal R (green)");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = side == 0 ? left : right;
            }
        }

        /// <summary>The fixed fanlight in the arch: a transom on the springing line, the arc, and five radiating bars.</summary>
        void Fanlight(Transform parent, float r, float spring, float z)
        {
            var f = new GameObject("Fanlight").transform;
            f.SetParent(parent, false);
            const float bar = 0.035f, depth = 0.04f;
            Box(f, "Transom", new Vector3(0f, spring, z), new Vector3(r * 2f, bar * 1.4f, depth), frame, collider: false);
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n, am = (a0 + a1) / 2f;
                var seg = Box(f, "Arc", new Vector3(Mathf.Cos(am) * (r - bar / 2f), spring + Mathf.Sin(am) * (r - bar / 2f), z),
                              new Vector3(bar, 2f * r * Mathf.Sin((a1 - a0) / 2f) + 0.01f, depth), frame, collider: false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg);
            }
            for (int k = 1; k <= 5; k++)
            {
                float a = Mathf.PI * k / 6f;
                var ray = Box(f, "Ray", new Vector3(Mathf.Cos(a) * r * 0.5f, spring + Mathf.Sin(a) * r * 0.5f, z),
                              new Vector3(0.02f, r, depth * 0.8f), frame, collider: false);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            }
        }

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

        void Easel(Transform parent, Vector3 at)
        {
            var e = new GameObject("Easel").transform;
            e.SetParent(parent, false);
            e.SetPositionAndRotation(at, Quaternion.Euler(0f, 180f, 0f));
            foreach (var x in new[] { -0.25f, 0.25f })
            {
                var leg = Box(e, "Leg", new Vector3(x, 0.85f, 0f), new Vector3(0.04f, 1.75f, 0.04f), wood);
                leg.transform.localRotation = Quaternion.Euler(-8f, 0f, x > 0 ? -6f : 6f);
            }
            var back = Box(e, "Back leg", new Vector3(0f, 0.8f, -0.25f), new Vector3(0.04f, 1.65f, 0.04f), wood);
            back.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            Box(e, "Ledge", new Vector3(0f, 0.8f, 0.07f), new Vector3(0.6f, 0.04f, 0.06f), wood);
        }

        static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material m, bool collider = true)
        {
            var go = Prim(parent, PrimitiveType.Cube, name, at, size, m);
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            return go;
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

        // ------------------------------------------------------------------------------------------------ Editor keys

        void Update()
        {
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
