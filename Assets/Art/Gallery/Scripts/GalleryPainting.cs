using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InnerFrame.Gallery
{
    /// <summary>
    /// One painting on the Gallery's wall (built by GalleryLayoutSetup from GalleryLayout): what its menu says and where
    /// Enter goes. Pointing at it lights a pale rim round its frame, the same look and material as #178's hover rim in the
    /// Painted Bedroom (Resources/HeldOutline.mat, a hidden copy of the mesh); the trigger opens its menu
    /// (GalleryPaintingPointer, GalleryPaintingMenu). Enter calls this painting's SceneChangeButton.Activate, the way the
    /// pedestals and the cards' ENTER did, so the fade and the network hand-over are unchanged.
    /// </summary>
    public class GalleryPainting : MonoBehaviour
    {
        public string title;
        public string caption;
        [Tooltip("Off (none) for a painting with no scene yet: its menu says Coming soon.")] public SceneChangeButton entrance;
        public Renderer canvas;
        public Renderer frame;
        [Tooltip("Metres: the menu closes when the player's head is farther than this from the canvas.")] public float closeDistance = 5f;

        [Header("Almond Blossom's blank frame (Serena, 2026-11-17; set by Build Layout)")]
        [Tooltip("A primed, white canvas until the player has been through Almond Studio (PaintingNames.AlmondPaintedPref). " +
                 "Then Almond Blossom appears on it from the branches' base out, once (AlmondRevealedPref), and stays; its Enter " +
                 "then leads into the studio too. Its wall label carries a Vincent quote and says where to find it until then.")]
        public bool blankUntilPainted;
        [Tooltip("The blank canvas: a primed canvas's cream (Almond Studio fades to the same colour, AS_Layout.leaveCream).")]
        public Color blankCream = new Color(0.96f, 0.94f, 0.89f);
        [Tooltip("Coming back from the studio you stand this far in front of the canvas, metres: the frame then fills about as much of your view as the window did.")]
        public float arriveDistance = 2.3f;
        [Tooltip("After the fade-in (or once you look at it), seconds before it starts to appear.")] public float revealDelay = 1.2f;
        [Tooltip("Seconds for the painting to come through the cream, from revealFrom out.")] public float revealSeconds = 5f;
        [Tooltip("Where it starts, as a fraction of the canvas (0, 0 bottom left): where Almond Blossom's thick branch comes in.")]
        public Vector2 revealFrom = new Vector2(0f, 0.25f);
        public string blankTitle = "?";
        public string blankCaption = "Find it in Window in the Studio";
        [Tooltip("The menu's button while it's blank (Enter is off).")] public string blankButton = "Not painted yet";
        [TextArea(2, 4)] public string blankHint = "This canvas is still waiting.\nStep into Window in the Studio\nto find out what belongs here.";
        [TextArea(2, 4)] public string blankQuote = "\u201cThere is nothing more truly artistic than to love people.\u201d\n\u2014 Vincent to Theo, Arles, 1888";
        [Tooltip("The wall label's tab while it's blank, after the number (\"4 · To discover\").")] public string blankTab = "To discover";

        static readonly Color RimColor = new Color(1f, 0.93f, 0.7f);   // #178's (PaintedBedroomHoverHighlight): warm candle cream
        const float RimWidth = 4f;                                      // pixels of the eye buffer, as #178's
        static Material rimMaterial;
        Renderer rim;
        bool hovered;
        // A frame inside Gallery.fbx (The Bedroom's) is static, so Unity merges it into the room's one static-batch mesh:
        // its MeshFilter then holds the WHOLE ROOM, and a copy of that lit a rim round the room (a cream wash over the
        // walls). Such a frame's rim draws only the frame's own sub-meshes of the merged mesh, every frame while lit.
        Mesh batchedMesh;
        int batchedFirst, batchedCount;
        RenderParams batchedParams;

        /// <summary>The rim round its frame is showing (for the checks).</summary>
        public bool RimLit => hovered && (rim != null ? rim.enabled : batchedMesh != null);

        // The canvas's and the frame's colliders -> their painting (The Bedroom's frame is Gallery.fbx's, not a child).
        static readonly Dictionary<Collider, GalleryPainting> byCollider = new Dictionary<Collider, GalleryPainting>();
        public static GalleryPainting Of(Collider c) => c != null && byCollider.TryGetValue(c, out var p) ? p : null;

        void OnEnable()
        {
            foreach (var r in new[] { canvas, frame })
                if (r != null) foreach (var c in r.GetComponents<Collider>()) byCollider[c] = this;
        }

        void OnDisable()
        {
            Hover(false);
            foreach (var r in new[] { canvas, frame })
                if (r != null) foreach (var c in r.GetComponents<Collider>()) byCollider.Remove(c);
        }

        public bool CanEnter => entrance != null && !string.IsNullOrEmpty(entrance.targetScene);
        /// <summary>The menu's button when Enter is off.</summary>
        public string ClosedLabel => Blank ? blankButton : "Coming soon";
        /// <summary>Its canvas is the blank one, or the cream is still over the painting (for the checks).</summary>
        public bool Blank { get; private set; }
        public Vector3 Centre => canvas != null ? canvas.bounds.center : transform.position;

        public void Enter()
        {
            if (!CanEnter) return;
            Debug.Log($"[Gallery] {title} -> {entrance.targetScene}", this);
            entrance.Activate();
        }

        public void Hover(bool on)
        {
            if (on == hovered) return;
            hovered = on;
            if (on && rim == null && batchedMesh == null) BuildRim();
            if (rim != null) rim.enabled = on;
        }

        // Update, not LateUpdate: a draw queued here is in every camera that renders this frame, a Camera.Render() from a
        // coroutine included (they run between Update and LateUpdate)
        void Update()
        {
            if (!hovered || batchedMesh == null) return;
            // the scene's static batch has no root: the merged mesh's vertices are already in world space
            for (int i = 0; i < batchedCount; i++) Graphics.RenderMesh(batchedParams, batchedMesh, batchedFirst + i, Matrix4x4.identity);
        }

        void BuildRim()
        {
            if (frame == null) return;
            if (rimMaterial == null) rimMaterial = Resources.Load<Material>("HeldOutline");
            var mesh = frame.GetComponent<MeshFilter>();
            if (rimMaterial == null || mesh == null || mesh.sharedMesh == null) { Debug.LogWarning("[Gallery] no hover rim (Resources/HeldOutline.mat or the frame's mesh is missing)", this); return; }
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", RimColor);
            block.SetFloat("_Width", RimWidth);
            block.SetFloat("_Pulse", 0f);

            if (frame.isPartOfStaticBatch && frame is MeshRenderer batched)
            {
                batchedMesh = mesh.sharedMesh;
                batchedFirst = batched.subMeshStartIndex;
                batchedCount = Mathf.Max(1, frame.sharedMaterials.Length);
                batchedParams = new RenderParams(rimMaterial)
                {
                    layer = frame.gameObject.layer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    matProps = block,
                };
                return;
            }

            var go = new GameObject("Hover Outline");
            go.layer = frame.gameObject.layer;
            go.transform.SetParent(frame.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            rim = go.AddComponent<MeshRenderer>();
            var materials = new Material[Mathf.Max(1, mesh.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = rimMaterial;
            rim.sharedMaterials = materials;
            rim.shadowCastingMode = ShadowCastingMode.Off;
            rim.receiveShadows = false;
            rim.lightProbeUsage = LightProbeUsage.Off;
            rim.reflectionProbeUsage = ReflectionProbeUsage.Off;
            rim.SetPropertyBlock(block);
            rim.enabled = false;
        }

        // ------------------------------------------------------------------------------------------------ the blank frame
        // Almond Studio (AS_Game) ends every visit on the window in full bloom, fading to cream; the Gallery comes out of
        // the same cream with you in front of this frame (PaintingNames.LeftAlmondStudio), and the first time, Almond
        // Blossom comes through the cream on it. Build Layout leaves the canvas, title and wall label as the painting's;
        // until it has been painted they are swapped here at load (outside Play the scene shows the painting).

        // The wall label's parts, named as on The Bedroom's card it was copied from (GalleryLayoutSetup.FillCopy)
        const string CardsRoot = "Cici Artwork Cards";
        Material paintedMaterial, blankMaterial, washMaterial;
        Texture2D washMask;
        Color32[] washPixels;
        GameObject wash;
        string paintedTitle, paintedCaption;
        readonly List<(TMPro.TMP_Text text, string painted, string blank)> cardTexts = new List<(TMPro.TMP_Text, string, string)>();
        UnityEngine.UI.RawImage cardPicture;

        void Start()
        {
            if (!blankUntilPainted || canvas == null) return;
            bool painted = PlayerPrefs.GetInt(PaintingNames.AlmondPaintedPref, 0) == 1;
            bool revealed = PlayerPrefs.GetInt(PaintingNames.AlmondRevealedPref, 0) == 1;
            bool back = PaintingNames.TakeLeftAlmondStudio();
            paintedMaterial = canvas.sharedMaterial;
            paintedTitle = title;
            paintedCaption = caption;
            ReadCard();
            if (back) StartCoroutine(Arrive());
            if (!painted) ShowBlank(lit: true);
            else if (!revealed) { ShowBlank(lit: false); StartCoroutine(Reveal()); }
            else Painted();
        }

        void OnDestroy()
        {
            if (blankMaterial != null) Destroy(blankMaterial);
            if (washMaterial != null) Destroy(washMaterial);
            if (washMask != null) Destroy(washMask);
        }

        /// <summary>You stand arriveDistance in front of the canvas, facing it (under the fade-in; again a frame later,
        /// with the headset's newest pose).</summary>
        IEnumerator Arrive()
        {
            var into = transform.forward;
            into.y = 0f;
            var at = Centre + into.normalized * arriveDistance;
            float yaw = Mathf.Atan2(-into.x, -into.z) * Mathf.Rad2Deg;
            NetworkSession.PutPlayer(at, yaw);
            yield return null;
            NetworkSession.PutPlayer(at, yaw);
        }

        /// <summary>The blank state: lit, a cream canvas under the spotlight like the others (never painted); or not lit,
        /// the painting under an unlit cream veil the colour of the fade (the reveal follows).</summary>
        void ShowBlank(bool lit)
        {
            Blank = true;
            title = blankTitle;
            caption = blankCaption;
            foreach (var c in cardTexts) c.text.text = c.blank;
            if (cardPicture != null) cardPicture.enabled = false;
            if (lit)
            {
                blankMaterial = new Material(paintedMaterial) { mainTexture = Texture2D.whiteTexture, color = blankCream };
                canvas.sharedMaterial = blankMaterial;
            }
            else MakeWash();
        }

        /// <summary>Painted and shown: the painting, its own title and label, and Enter into the studio.</summary>
        void Painted()
        {
            Blank = false;
            canvas.sharedMaterial = paintedMaterial;
            title = paintedTitle;
            caption = paintedCaption;
            foreach (var c in cardTexts) c.text.text = c.painted;
            if (cardPicture != null) cardPicture.enabled = true;
            if (entrance == null)
                foreach (var other in FindObjectsByType<GalleryPainting>(FindObjectsSortMode.None))
                    if (other != this && other.entrance != null && other.entrance.targetScene == PaintingNames.AlmondStudio) { entrance = other.entrance; break; }
        }

        /// <summary>Once the fade-in is over and you are looking at it, the painting comes through the cream from
        /// revealFrom out, with a ragged edge; then it is remembered.</summary>
        IEnumerator Reveal()
        {
            while (SceneFader.IsFading || !LookedAt()) yield return null;
            for (float t = 0f; t < revealDelay; t += Time.deltaTime) yield return null;
            var aspect = new Vector2(washMask.width, washMask.height).normalized;
            float far = 0f;
            foreach (var corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                far = Mathf.Max(far, Vector2.Scale(corner - revealFrom, aspect).magnitude);
            const float soft = 0.18f, ragged = 0.22f;
            for (float t = 0f; t <= revealSeconds; t += Time.deltaTime)
            {
                float front = Mathf.Lerp(-soft, far + ragged + soft, Mathf.SmoothStep(0f, 1f, t / revealSeconds));
                Paint(front, soft, ragged);
                yield return null;
            }
            PlayerPrefs.SetInt(PaintingNames.AlmondRevealedPref, 1);
            PlayerPrefs.Save();
            Destroy(wash);
            Painted();
        }

        bool LookedAt()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var to = Centre - cam.transform.position;
            return to.magnitude < 6f && Vector3.Angle(cam.transform.forward, to) < 30f;
        }

        /// <summary>The veil: an unlit quad just in front of the canvas, cream, its alpha a small mask redrawn each frame.</summary>
        void MakeWash()
        {
            canvas.sharedMaterial = paintedMaterial;
            const int w = 64, h = 48;
            washMask = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Blank frame veil" };
            washPixels = new Color32[w * h];
            for (int i = 0; i < washPixels.Length; i++) washPixels[i] = new Color32(255, 255, 255, 255);
            washMask.SetPixels32(washPixels);
            washMask.Apply(false);
            washMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = washMask, color = blankCream };
            wash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(wash.GetComponent<Collider>());   // the pointer's ray goes on to the canvas
            wash.name = "Blank frame veil";
            wash.transform.SetParent(transform, true);
            var b = canvas.bounds;
            var right = transform.right;
            float width = Mathf.Abs(right.x) * b.size.x + Mathf.Abs(right.z) * b.size.z;
            // a quad's +x is the viewer's right when it faces away from them (Sprites/Default draws both sides)
            wash.transform.SetPositionAndRotation(b.center + transform.forward * 0.004f, Quaternion.LookRotation(-transform.forward, Vector3.up));
            wash.transform.localScale = new Vector3(width, b.size.y, 1f);
            var r = wash.GetComponent<Renderer>();
            r.sharedMaterial = washMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>Clears the veil within `front` of revealFrom (canvas fractions, its aspect kept), softly, the edge
        /// made ragged by a little noise.</summary>
        void Paint(float front, float soft, float ragged)
        {
            int w = washMask.width, h = washMask.height;
            var aspect = new Vector2(w, h).normalized;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var uv = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                    float d = Vector2.Scale(uv - revealFrom, aspect).magnitude + ragged * Mathf.PerlinNoise(uv.x * 5f + 3.1f, uv.y * 5f + 7.7f);
                    float a = Mathf.Clamp01((d - front + soft) / soft);
                    washPixels[y * w + x].a = (byte)(a * 255f);
                }
            washMask.SetPixels32(washPixels);
            washMask.Apply(false);
        }

        /// <summary>The wall label's words as Build Layout left them (the painting's), and the blank frame's.</summary>
        void ReadCard()
        {
            var root = GameObject.Find(CardsRoot);
            var card = root != null ? root.transform.Find(name + " Introduction") : null;
            if (card == null) { Debug.LogWarning($"[Gallery] {name}: no wall label ({CardsRoot}/{name} Introduction) to blank", this); return; }
            string both = blankQuote + "\n\n" + blankHint.Replace("\n", " ");
            foreach (var t in card.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                string n = t.gameObject.name, blank;
                if (n == "The Bedroom") blank = blankTitle;                                  // the title
                else if (n == "Artwork") { int dot = t.text.IndexOf('\u00b7'); blank = (dot > 0 ? t.text.Substring(0, dot + 1) + " " : "") + blankTab; }
                else if (n.StartsWith("1889\nOil")) blank = blankHint;                       // the details: the way in
                else if (n.StartsWith("Van Gogh painted his bedroom")) blank = blankQuote;    // the summary: his words
                else if (n.StartsWith("Painted in 1889, The Bedroom")) blank = both;          // the longer text
                else if (n == "Artist portrait caption" || n.StartsWith("Vincent van Gogh \u00b7")) blank = "";
                else continue;
                cardTexts.Add((t, t.text, blank));
            }
            cardPicture = System.Array.Find(card.GetComponentsInChildren<UnityEngine.UI.RawImage>(true), i => i.name == "Portrait");
        }
    }
}
