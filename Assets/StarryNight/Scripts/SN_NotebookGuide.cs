using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Four manually browsed instructions printed on the existing sketchbook's raised right page. The original
    /// left drawing and grab/letter behaviour stay intact. Navigation never advances a task or moves the book.
    /// </summary>
    public sealed class SN_NotebookGuide : MonoBehaviour
    {
        enum Control { Grip, Trigger, None }
        const float TextScale = .01f;
        static readonly Color Ink = new Color32(29, 48, 78, 255);
        static readonly Color Paper = new Color32(236, 216, 171, 255);
        static readonly Color Ochre = new Color32(177, 135, 60, 255);
        readonly List<Object> owned = new List<Object>();
        SN_Game game;
        TMP_FontAsset font;
        Material ink, gold, paper, controllerArt, discoveryGold;
        TextMeshPro heading, words, buttonName, pageNumber, currentTask, previousLabel, nextLabel;
        MeshRenderer previousBorder, nextBorder;
        Material previousGold, nextGold;
        readonly HashSet<int> capturedTriggers = new HashSet<int>();
        readonly HashSet<int> previousHover = new HashSet<int>(), nextHover = new HashSet<int>();
        readonly Dictionary<Transform, NearFarInteractor> nativeHands = new Dictionary<Transform, NearFarInteractor>();
        readonly RaycastHit[] blockers = new RaycastHit[32];
        static readonly Rect PreviousArea = new Rect(-.066f, -.103f, .058f, .028f);
        static readonly Rect NextArea = new Rect(.008f, -.103f, .058f, .028f);
        bool manuallyBrowsed;
        Transform drawing;
        MeshRenderer discoveryEdge;
        XRGrabInteractable grab;
        Control shownControl = (Control)(-1);
        bool shownWind;

        public const int PageCount = 4;
        public int CurrentPage { get; private set; }
        public int CurrentTaskPage => game == null ? -1 : game.State == SN_State.Lantern ? 0 :
            game.State == SN_State.Stars ? 1 : game.State == SN_State.Wind ? 2 :
            game.State == SN_State.Dawn && game.PageShown ? 3 : -1;
        public bool IsCurrentTaskPage => CurrentTaskPage == CurrentPage;
        public Vector3 PreviousTarget => PageRoot.TransformPoint(new Vector3(PreviousArea.center.x, PreviousArea.center.y, -.002f));
        public Vector3 NextTarget => PageRoot.TransformPoint(new Vector3(NextArea.center.x, NextArea.center.y, -.002f));
        public string Title { get; private set; }
        public string Instruction { get; private set; }
        public Transform PageRoot { get; private set; }
        public bool IsPresent => PageRoot != null && PageRoot.gameObject.activeInHierarchy && heading != null;
        public bool HasBeenPickedUp { get; private set; }
        public bool PickupReminderVisible => !HasBeenPickedUp && IsPresent;
        public bool DiscoveryCueVisible => discoveryEdge != null && discoveryEdge.enabled && discoveryEdge.gameObject.activeInHierarchy;

        public static SN_NotebookGuide Attach(SN_Game game)
        {
            if (game == null || game.sketchbook == null || game.ui == null || game.ui.font == null) return null;
            var existing = game.sketchbook.GetComponent<SN_NotebookGuide>();
            if (existing != null) return existing;
            var material = Resources.Load<Material>(SN_PageOutline.MaterialName);
            var lifted = game.sketchbook.transform.Find("LiftedHalf");
            if (material == null || lifted == null)
            {
                Debug.LogError("[SN] The notebook guide needs the existing raised page and SN_PageOutline material.");
                return null;
            }
            var guide = game.sketchbook.gameObject.AddComponent<SN_NotebookGuide>();
            guide.game = game;
            guide.Build(lifted, material);
            guide.grab = game.sketchbook.GetComponent<XRGrabInteractable>();
            if (guide.grab != null) guide.grab.selectEntered.AddListener(guide.OnPickedUp);
            guide.Refresh();
            return guide;
        }

        void Build(Transform lifted, Material source)
        {
            // LiftedHalf's +y is the actual page normal, +z its top. TMP's front is -z.
            PageRoot = new GameObject("SN_InstructionInsert").transform;
            PageRoot.SetParent(lifted, false);
            PageRoot.localPosition = new Vector3(0, .0075f, 0);
            PageRoot.localRotation = Quaternion.Euler(90, 0, 0);
            paper = Tint(source, Paper, "SN notebook paper");
            ink = Tint(source, Ink, "SN notebook ink");
            gold = Tint(source, Ochre, "SN notebook clip");
            // Reuse the exact painted controller pixels shown by the full Controls guide, not a second icon design.
            var controllerTexture = InnerFrame.Tutorial.ControllerDiagram.Texture;
            if (controllerTexture != null)
            {
                controllerArt = Tint(source, Color.white, "SN notebook shared Controls artwork");
                controllerArt.SetTexture("_BaseMap", controllerTexture);
            }

            // An uneven paper edge, not another screen or a shiny framed sign. Existing book remains underneath it.
            var sheet = new List<Vector2> {
                new Vector2(-.069f,-.101f), new Vector2(.065f,-.103f), new Vector2(.070f,-.098f),
                new Vector2(.068f,.099f), new Vector2(.063f,.103f), new Vector2(-.067f,.101f),
                new Vector2(-.070f,.096f), new Vector2(-.068f,-.049f)
            };
            Polygon("Cream paper insert", PageRoot, sheet, 0, paper);
            discoveryGold = Tint(source, Ochre, "SN notebook discovery edge");
            discoveryEdge = PaperEdge(sheet, discoveryGold);
            Polygon("Painted brass clip", PageRoot, new[] {
                new Vector2(-.012f,.098f), new Vector2(.012f,.098f),
                new Vector2(.010f,.108f), new Vector2(-.010f,.108f)
            }, -.0013f, gold);

            font = TMP_FontAsset.CreateFontAsset(game.ui.font, 90, 9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = "SN notebook Libre Baskerville (runtime)";
            owned.Add(font);
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?' /|");
            // This runtime font owns its material/atlas. It is never an overlay: objects can occlude the page and ink.
            owned.Add(font.material);
            foreach (var atlas in font.atlasTextures) if (atlas != null) owned.Add(atlas);
            font.material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.LessEqual);
            heading = Label("Action title", .080f, .038f, .0097f);
            words = Label("Action instruction", .035f, .036f, .0080f);
            buttonName = Label("Controller button", -.062f, .012f, .0055f);
            pageNumber = Label("Guide page count", .099f, .012f, .0047f);
            pageNumber.transform.localPosition += Vector3.left * .048f;
            pageNumber.rectTransform.sizeDelta = new Vector2(.032f, .012f) / TextScale;
            currentTask = Label("Current task marker", .099f, .012f, .0047f);
            currentTask.transform.localPosition += Vector3.right * .036f;
            currentTask.rectTransform.sizeDelta = new Vector2(.060f, .012f) / TextScale;
            previousGold = Tint(source, Ochre, "SN notebook Previous border");
            nextGold = Tint(source, Ochre, "SN notebook Next border");
            previousBorder = Navigation("Previous", PreviousArea, previousGold, out previousLabel);
            nextBorder = Navigation("Next", NextArea, nextGold, out nextLabel);
            drawing = new GameObject("Printed controller diagram").transform;
            drawing.SetParent(PageRoot, false);
        }

        /// <summary>Refreshes the task marker and current printed page. Story progress never turns a page for the reader.</summary>
        public void Refresh()
        {
            if (!IsPresent || game == null) return;
            RefreshDiscovery();
            string title, instruction, titleLines, instructionLines;
            switch (CurrentPage)
            {
                case 1:
                    title = "Wake the stars"; titleLines = "Wake the\nstars";
                    instruction = "Point at a star. Pull the trigger."; instructionLines = "Point at a star.\nPull the trigger.";
                    break;
                case 2:
                    title = "Trace the wind"; titleLines = "Trace the\nwind";
                    instruction = "Hold the trigger. Follow the dots."; instructionLines = "Hold the trigger.\nFollow the dots.";
                    break;
                case 3:
                    title = "Take his page"; titleLines = "Take his\npage";
                    instruction = "Point at his page. Pull the trigger."; instructionLines = "Point at his page.\nPull the trigger.";
                    break;
                default:
                    title = "Dim the lantern"; titleLines = "Dim the\nlantern";
                    instruction = "Point at the lantern. Pull the trigger."; instructionLines = "Point at the lantern.\nPull the trigger.";
                    break;
            }
            if (Title != title || Instruction != instruction)
            {
                Title = title; Instruction = instruction;
                heading.text = titleLines; words.text = instructionLines;
            }
            pageNumber.text = (CurrentPage + 1) + " / " + PageCount;
            currentTask.text = IsCurrentTaskPage ? "Current task" : "";
            buttonName.text = PickupReminderVisible ? "Trigger | Hold Grip to lift" : "Trigger";
            RefreshNavigation();
            bool wind = CurrentPage == 2;
            if (shownControl == Control.Trigger && shownWind == wind) return;
            shownControl = Control.Trigger; shownWind = wind;
            DrawController(Control.Trigger, wind);
        }

        void OnPickedUp(SelectEnterEventArgs args)
        {
            if (args.interactorObject == null || game == null || game.sketchbook.Following || game.sketchbook.HeldByFallback) return;
            if (!HasBeenPickedUp && !manuallyBrowsed && CurrentTaskPage >= 0) CurrentPage = CurrentTaskPage;
            HasBeenPickedUp = true;
            Refresh();
        }

        /// <summary>Call once per hand before scene Trigger/held-Trigger actions, even while a menu blocks input.
        /// True means only this hand belongs to the printed navigation. A press remains owned until release,
        /// even if the ray leaves the paper, so page browsing cannot also dim a lantern, wake a star or draw wind.</summary>
        public bool TryHandlePageInput(int hand, SN_IInput input, bool allowed = true)
        {
            if (input == null || hand < 0 || hand >= input.HandCount) return false;
            bool held = input.TriggerHeld(hand), down = input.TriggerDown(hand);
            bool captured = capturedTriggers.Contains(hand);
            previousHover.Remove(hand); nextHover.Remove(hand);
            if (captured && !held && !down) capturedTriggers.Remove(hand);
            if (!allowed || !IsPresent || game.sketchbook.Following)
            {
                RefreshNavigation();
                return captured;
            }
            int direction = PageControl(hand, input);
            if (direction < 0) previousHover.Add(hand);
            if (direction > 0) nextHover.Add(hand);
            if (!captured && direction != 0 && down)
            {
                capturedTriggers.Add(hand); captured = true;
                int page = Mathf.Clamp(CurrentPage + direction, 0, PageCount - 1);
                if (page != CurrentPage)
                {
                    CurrentPage = page; manuallyBrowsed = true;
                    input.Haptic(hand, .18f, .035f);
                    Refresh();
                }
            }
            RefreshNavigation();
            return captured || direction != 0;
        }

        int PageControl(int hand, SN_IInput input)
        {
            if (!input.TryGetAim(hand, out var ray)) return 0;
            float reach = 2f;
            var controller = input.Hand(hand);
            if (controller != null)
            {
                if (!nativeHands.TryGetValue(controller, out var native) || native == null)
                {
                    native = controller.GetComponentInChildren<NearFarInteractor>(true);
                    if (native != null) nativeHands[controller] = native;
                }
                if (native != null)
                {
                    if (!native.isActiveAndEnabled || !native.enableFarCasting) return 0;
                    var start = native.curveOrigin != null ? native.curveOrigin : native.transform;
                    ray = new Ray(start.position, start.forward);
                    if (native.farInteractionCaster is CurveInteractionCaster caster) reach = caster.castDistance;
                }
            }
            // Ink is only on the front of this physical page. No reverse-side or through-wall controls.
            if (Vector3.Dot(ray.direction, PageRoot.forward) <= .001f) return 0;
            var plane = new Plane(PageRoot.forward, PageRoot.TransformPoint(new Vector3(0, 0, -.002f)));
            if (!plane.Raycast(ray, out float distance) || distance > reach) return 0;
            var local = PageRoot.InverseTransformPoint(ray.GetPoint(distance));
            int direction = PreviousArea.Contains(new Vector2(local.x, local.y)) ? -1 :
                NextArea.Contains(new Vector2(local.x, local.y)) ? 1 : 0;
            if (direction == 0) return 0;
            int count = gameObject.scene.GetPhysicsScene().Raycast(ray.origin, ray.direction, blockers, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (blockers[i].collider != null && !blockers[i].collider.transform.IsChildOf(transform) &&
                    blockers[i].distance < distance - .002f) return 0;
            return direction;
        }

        MeshRenderer Navigation(string title, Rect area, Material border, out TextMeshPro label)
        {
            var outside = new[] { new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, area.yMin),
                new Vector2(area.xMax, area.yMax), new Vector2(area.xMin, area.yMax) };
            var frame = Polygon("Painted " + title + " border", PageRoot, outside, -.0016f, border);
            const float inset = .0015f;
            Polygon("Painted " + title + " paper", PageRoot, new[] {
                new Vector2(area.xMin + inset, area.yMin + inset), new Vector2(area.xMax - inset, area.yMin + inset),
                new Vector2(area.xMax - inset, area.yMax - inset), new Vector2(area.xMin + inset, area.yMax - inset)
            }, -.0017f, paper);
            label = Label(title, area.center.y + .005f, .018f, .0062f);
            label.transform.localPosition = new Vector3(area.center.x, area.center.y + .005f, -.002f);
            label.rectTransform.sizeDelta = new Vector2(area.width - .004f, .018f) / TextScale;
            label.text = title;
            return frame;
        }

        void RefreshNavigation()
        {
            if (previousBorder == null || nextBorder == null) return;
            previousGold.SetColor("_BaseColor", CurrentPage == 0 ? Color.Lerp(Paper, Ochre, .3f) : previousHover.Count > 0 ? SN_PageOutline.Bright : Ochre);
            nextGold.SetColor("_BaseColor", CurrentPage == PageCount - 1 ? Color.Lerp(Paper, Ochre, .3f) : nextHover.Count > 0 ? SN_PageOutline.Bright : Ochre);
            previousLabel.color = CurrentPage == 0 ? Color.Lerp(Paper, Ink, .45f) : Ink;
            nextLabel.color = CurrentPage == PageCount - 1 ? Color.Lerp(Paper, Ink, .45f) : Ink;
        }

        void OnDisable()
        {
            capturedTriggers.Clear(); previousHover.Clear(); nextHover.Clear();
        }

        // A slow change of warm ink on the physical paper edge. It never moves, changes the book's pose, emits
        // light into the scene or advances a task. Menus and live conversation can temporarily suppress it.
        void LateUpdate() => RefreshDiscovery();

        void RefreshDiscovery()
        {
            if (discoveryEdge == null || game == null) return;
            discoveryEdge.enabled = PickupReminderVisible && !SN_Game.MenuOpen && (game.voice == null || !game.voice.ConversationPaused);
            if (!discoveryEdge.enabled) return;
            float blend = .2f + .4f * (.5f + .5f * Mathf.Sin(game.Clock * Mathf.PI / 2f));
            discoveryGold.SetColor("_BaseColor", Color.Lerp(Ochre, SN_PageOutline.Bright, blend));
        }

        MeshRenderer PaperEdge(IList<Vector2> sheet, Material material)
        {
            const float width = .006f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var point in sheet)
            {
                Vector2 outside = point + point.normalized * width;
                vertices.Add(new Vector3(point.x, point.y, -.0003f));
                vertices.Add(new Vector3(outside.x, outside.y, -.0003f));
            }
            for (int i = 0; i < sheet.Count; i++)
            {
                int a = i * 2, b = ((i + 1) % sheet.Count) * 2;
                triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
            return MeshObject("Warm paper edge until first Grip", PageRoot, vertices, triangles, material);
        }

        Material Tint(Material source, Color colour, string name)
        {
            var material = new Material(source) { name = name };
            material.SetColor("_BaseColor", colour);
            material.renderQueue = (int)RenderQueue.Geometry;
            owned.Add(material);
            return material;
        }

        TextMeshPro Label(string name, float top, float height, float cap)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            text.transform.SetParent(PageRoot, false);
            text.transform.localPosition = new Vector3(0, top, -.001f);
            text.transform.localScale = Vector3.one * TextScale;
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(.128f, height) / TextScale;
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.color = Ink;
            text.isOrthographic = false;
            text.isOverlay = false;
            text.fontSize = cap * font.faceInfo.pointSize / (font.faceInfo.capLine * .1f * TextScale);
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Top;
            text.richText = false;
            text.renderer.shadowCastingMode = ShadowCastingMode.Off;
            text.renderer.receiveShadows = false;
            return text;
        }

        void DrawController(Control control, bool wind)
        {
            // Rebuild only when changing between Grip / Trigger / no-control pages, never every frame.
            for (int i = drawing.childCount - 1; i >= 0; i--)
            {
                var child = drawing.GetChild(i).gameObject;
                var filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) { owned.Remove(filter.sharedMesh); Release(filter.sharedMesh); }
                child.SetActive(false); Release(child);
            }
            if (control == Control.None) return;
            if (controllerArt != null) DrawSharedController(control);
            if (wind)
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                for (int i = 0; i < 10; i++)
                {
                    float x = .031f + i * .003f, y = -.027f + Mathf.Sin(i * .65f) * .010f;
                    int n = vertices.Count;
                    vertices.Add(new Vector3(x, y, -.0015f));
                    for (int k = 0; k < 6; k++)
                    {
                        float angle = k * Mathf.PI / 3f;
                        vertices.Add(new Vector3(x + Mathf.Cos(angle) * .0012f, y + Mathf.Sin(angle) * .0012f, -.0015f));
                        triangles.AddRange(new[] { n, n + 1 + (k + 1) % 6, n + 1 + k });
                    }
                }
                MeshObject("Printed wind dots", drawing, vertices, triangles, ink);
            }
        }

        void DrawSharedController(Control control)
        {
            // Pixel coordinates on the original 1536 x 1024 ControllerOverview.png (top-left origin).
            // A silhouette mesh clips away the navy background and external leader line without changing any pixels.
            // The isolated thumbstick is a second contour in the same mesh. Both controls retain the original cream
            // handle, navy top, painted strokes and gold button, exactly as on the Gallery Controls panel.
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uvs = new List<Vector2>();
            if (control == Control.Trigger)
            {
                AddArtworkContour(new[] {
                    new Vector2(634,728),new Vector2(648,730),new Vector2(682,741),new Vector2(715,753),
                    new Vector2(735,764),new Vector2(740,775),new Vector2(736,789),new Vector2(721,804),
                    new Vector2(710,837),new Vector2(699,868),new Vector2(685,897),new Vector2(671,914),
                    new Vector2(653,918),new Vector2(637,912),new Vector2(626,899),new Vector2(620,881),
                    new Vector2(618,859),new Vector2(621,824),new Vector2(614,820),new Vector2(615,809),
                    new Vector2(622,795),new Vector2(620,788),new Vector2(613,774),new Vector2(610,760),
                    new Vector2(614,745),new Vector2(624,733)
                }, 676f, vertices, triangles, uvs);
                AddArtworkContour(new[] {
                    new Vector2(690,725),new Vector2(702,726),new Vector2(718,732),new Vector2(724,738),
                    new Vector2(724,743),new Vector2(716,745),new Vector2(687,735),new Vector2(686,730)
                }, 676f, vertices, triangles, uvs);
            }
            else
            {
                AddArtworkContour(new[] {
                    new Vector2(1318,728),new Vector2(1335,730),new Vector2(1384,747),new Vector2(1414,762),
                    new Vector2(1424,774),new Vector2(1420,790),new Vector2(1405,806),new Vector2(1395,830),
                    new Vector2(1386,856),new Vector2(1375,885),new Vector2(1363,906),new Vector2(1349,916),
                    new Vector2(1335,914),new Vector2(1322,906),new Vector2(1312,891),new Vector2(1307,871),
                    new Vector2(1308,848),new Vector2(1314,820),new Vector2(1317,801),new Vector2(1308,793),
                    new Vector2(1296,784),new Vector2(1289,774),new Vector2(1289,758),new Vector2(1297,741),
                    new Vector2(1308,732)
                }, 1357f, vertices, triangles, uvs);
                AddArtworkContour(new[] {
                    new Vector2(1368,724),new Vector2(1376,723),new Vector2(1393,729),new Vector2(1401,736),
                    new Vector2(1400,741),new Vector2(1394,743),new Vector2(1365,732),new Vector2(1364,728)
                }, 1357f, vertices, triangles, uvs);
            }
            MeshObject("Controller artwork from Controls", drawing, vertices, triangles, controllerArt, uvs);
        }

        static void AddArtworkContour(Vector2[] points, float centreX, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            int start = vertices.Count;
            const float scale = .064f / 200f;
            var remaining = new List<int>();
            float area = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i]; var next = points[(i + 1) % points.Length];
                vertices.Add(new Vector3((p.x - centreX) * scale, (820f - p.y) * scale - .030f, -.0015f));
                uvs.Add(new Vector2(p.x / 1536f, 1f - p.y / 1024f));
                remaining.Add(i); area += p.x * next.y - next.x * p.y;
            }
            float orientation = Mathf.Sign(area);
            // Ear clipping keeps the concave neck/button silhouette; a triangle fan would expose background wedges.
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[b] - points[a], points[c] - points[b]) * orientation <= 0) continue;
                    bool contains = false;
                    foreach (int index in remaining)
                    {
                        if (index == a || index == b || index == c) continue;
                        var p = points[index];
                        if (Cross(points[b] - points[a], p - points[a]) * orientation >= 0 &&
                            Cross(points[c] - points[b], p - points[b]) * orientation >= 0 &&
                            Cross(points[a] - points[c], p - points[c]) * orientation >= 0) { contains = true; break; }
                    }
                    if (contains) continue;
                    triangles.Add(start + a); triangles.Add(start + b); triangles.Add(start + c);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) { Debug.LogError("[SN] Invalid shared controller silhouette."); break; }
            }
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        MeshRenderer Polygon(string name, Transform parent, IList<Vector2> points, float z, Material material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var p in points) vertices.Add(new Vector3(p.x, p.y, z));
            for (int i = 1; i < points.Count - 1; i++) triangles.AddRange(new[] {0, i + 1, i});
            return MeshObject(name, parent, vertices, triangles, material);
        }

        MeshRenderer MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material, List<Vector2> uvs = null)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            if (uvs != null) mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            owned.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }

        static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        void OnDestroy()
        {
            if (grab != null) grab.selectEntered.RemoveListener(OnPickedUp);
            if (PageRoot != null) Release(PageRoot.gameObject);
            foreach (var item in owned) Release(item);
            owned.Clear();
        }
    }
}
