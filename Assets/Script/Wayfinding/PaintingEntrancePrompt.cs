using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Shows how to step into a painting (#163). In the Gallery, every way in (a SceneChangeButton: the invisible box on a
/// pedestal) gets this at load, without editing the scene: a soft chrome-yellow glow over the box, so you can see what
/// to grip, and a hint above it with the painting's title and "Hold (grip) on the pedestal to step inside", the grip
/// button drawn as its icon. The hint shows
/// while a ray points at the box, and, until you have stepped into a painting once, whenever you're within NearMetres.
/// </summary>
public class PaintingEntrancePrompt : MonoBehaviour
{
    public const string Line = "Hold", Rest = "on the pedestal to step inside";   // Hold (grip icon) on the pedestal…
    const float NearMetres = 1.6f;          // horizontal, from the eyes to the box
    const float HintAbove = 0.22f;          // the hint's centre over the box's top, metres
    const float ReadDistance = 1.4f;        // the distance it is sized to be read from
    static readonly Color GlowLow = new Color(0.95f, 0.76f, 0.19f, 0.10f), GlowHigh = new Color(0.95f, 0.76f, 0.19f, 0.26f);

    /// <summary>True once the player has stepped into a painting this session: the hint then waits for a pointing ray.</summary>
    public static bool HasEntered { get; private set; }

    SceneChangeButton button;
    WayfindingHint hint;
    Material glow;
    Transform glowBox;
    Bounds box;
    int hovers;
    InnerFrame.ArtworkCici.ArtworkCiciActions[] cards;

    /// <summary>The hint, for the checks.</summary>
    public WayfindingHint Hint => hint;
    /// <summary>The scene this way in leads to.</summary>
    public string Target => button != null ? button.targetScene : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // the first scene loaded before this ran
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && scene.name == PaintingNames.Gallery) AttachAll(scene);
    }

    /// <summary>Adds a prompt to every way into a painting in `scene` that has none. Returns how many it has now.</summary>
    public static int AttachAll(Scene scene)
    {
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var b in root.GetComponentsInChildren<SceneChangeButton>(true))
            {
                if (string.IsNullOrEmpty(b.targetScene) || b.targetScene == PaintingNames.Gallery) continue;
                // a painting entered from its own menu (Part B, 2026-11-12: InnerFrame.Gallery.GalleryPaintingMenu): no pedestal to light
                if (b.GetComponentInParent<InnerFrame.Gallery.GalleryPainting>(true) != null) continue;
                if (b.GetComponent<PaintingEntrancePrompt>() == null) b.gameObject.AddComponent<PaintingEntrancePrompt>();
                count++;
            }
        return count;
    }

    void Awake() => Setup();

    /// <summary>Builds the glow and the hint; Awake calls it, and the edit-mode checks (where Awake doesn't run).</summary>
    public void Setup()
    {
        if (button != null) return;
        button = GetComponent<SceneChangeButton>();
        cards = FindObjectsByType<InnerFrame.ArtworkCici.ArtworkCiciActions>(FindObjectsSortMode.None);
        var simple = GetComponent<XRSimpleInteractable>();
        if (simple != null)
        {
            simple.hoverEntered.AddListener(_ => hovers++);
            simple.hoverExited.AddListener(_ => hovers = Mathf.Max(0, hovers - 1));
            simple.selectEntered.AddListener(_ => HasEntered = true);
        }
        BuildGlow();
        hint = WayfindingHint.Create("Entrance Hint", null, ReadDistance, false);
        if (hint != null)
        {
            SceneManager.MoveGameObjectToScene(hint.gameObject, gameObject.scene);
            hint.Show(PaintingNames.Title(button.targetScene), Line, WayfindingHint.GripKey, Rest);
            hint.transform.position = new Vector3(box.center.x, box.max.y + HintAbove, box.center.z);
        }
    }

    // The glow fills the button's box: a cube with no collider, transparent and unlit (Sprites/Default, as SceneFader).
    void BuildGlow()
    {
        var collider = GetComponent<BoxCollider>();
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Entrance Glow";
        Kill(cube.GetComponent<Collider>());
        glowBox = cube.transform;
        glowBox.SetParent(transform, false);
        if (collider != null) { glowBox.localPosition = collider.center; glowBox.localScale = collider.size; }
        var shader = Shader.Find("Sprites/Default");
        glow = new Material(shader) { color = GlowLow, renderQueue = 3000 };
        var r = cube.GetComponent<Renderer>();
        r.sharedMaterial = glow;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        box = r.bounds;
        if (collider == null) { var any = GetComponent<Collider>(); if (any != null) box = any.bounds; }
    }

    void Update()
    {
        bool reading = false;
        foreach (var card in cards)
            if (card && card.gameObject.scene == gameObject.scene && card.IsOpen) { reading = true; break; }
        if (glowBox) glowBox.gameObject.SetActive(!reading && !PlayerMenu.MenuOpen);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / 2.4f);
        if (glow != null) glow.color = Color.Lerp(GlowLow, GlowHigh, pulse);
        if (hint == null) return;

        var eyes = Camera.main;
        bool near = false;
        if (eyes != null)
        {
            var off = box.center - eyes.transform.position;
            off.y = 0f;
            near = off.magnitude < NearMetres;
            var face = hint.transform.position - eyes.transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 1e-4f) hint.transform.rotation = Quaternion.LookRotation(face, Vector3.up);
        }
        hint.Visible = !reading && !PlayerMenu.MenuOpen && (hovers > 0 || (near && !HasEntered));
    }

    void OnDestroy()
    {
        if (glow != null) Kill(glow);
        if (hint != null) Kill(hint.gameObject);
    }

    static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
}
