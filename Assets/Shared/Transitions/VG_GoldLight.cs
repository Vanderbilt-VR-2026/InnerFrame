using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The opening's gold light (Rowling's Entrance: EntranceMenu.GoldGradient, on main 2026-11-11) between two paintings:
/// the same radial texture, made by the same formula (Texture), on the material a Canvas RawImage uses, over the whole
/// view. Only its colour and texture are the opening's: it rises smoothly over `rise` seconds (no flicker: a full-view
/// flicker in a headset is uncomfortable and a photosensitivity risk), its bright centre on Focus (Vincent, or where he
/// went) while it rises. Once full, the project's own scene change runs under it (SceneFader.FadeToScene, its black
/// hidden), and the gold stays until the next scene is up and has drawn two frames, then clears in ClearSeconds (the
/// opening's reveal). It draws last: render queue 5000 (the highest URP draws) at the top sorting order, no depth
/// test. It survives the load and removes itself. Stepped by Update in Play mode, by the checks (Step) in edit mode.
/// </summary>
public class VG_GoldLight : MonoBehaviour
{
    public const float RiseSeconds = 0.8f, ClearSeconds = 0.6f, TimeoutSeconds = 15f;
    public const float CentreMaxDeg = 25f;   // the bright centre stays this near the middle of the view
    /// <summary>Metres ahead of the eye, as the Entrance's canvas (3 m): both eyes see one picture. Nothing hides it (no depth test).</summary>
    public const float Distance = 3f;
    /// <summary>The view spans the middle third of the gradient, as in the Entrance (its image is three times the canvas).</summary>
    public const float HalfSizePerDistance = 3.6f;
    public static readonly Color Edge = new Color(.72f, .40f, .08f), Centre = new Color(1f, .9f, .46f);

    public enum Phase { Rising, Loading, Clearing }
    public static VG_GoldLight Active { get; private set; }
    public Phase Now { get; private set; }
    public float Alpha { get; private set; }
    public string Scene { get; private set; }
    /// <summary>Seconds from the start to full gold (for the checks and the report).</summary>
    public float FullAfter { get; private set; } = -1f;
    public event Action Full, Failed;
    /// <summary>Seconds from nothing to full (smooth: it never dims on the way up).</summary>
    public float rise = RiseSeconds;
    /// <summary>The scene change (the checks replace both): begins the load, true if it began; then true once it is over.</summary>
    public Func<string, bool> Load = LoadWithFader, Arrived = FaderArrived;
    public Func<Vector3?> Focus;
    /// <summary>The active scene's name (the checks replace it).</summary>
    public Func<string> ActiveScene = () => SceneManager.GetActiveScene().name;
    public Transform View;   // the head; null: the main camera
    /// <summary>Where the bright centre sits in the view plane, per metre of distance (0: the middle of the view, as in the opening).</summary>
    public Vector2 Offset;

    float t, waited;
    string from;   // the scene it began in: if another scene change took the player away first, it doesn't load
    int framesUp;
    bool failed;
    Material mat;
    Transform quad;

    public static VG_GoldLight Begin(string scene, Func<Vector3?> focus = null)
    {
        if (Active != null) return Active;
        var go = new GameObject("VG_GoldLight");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Active = go.AddComponent<VG_GoldLight>();
        Active.Scene = scene;
        Active.from = Active.ActiveScene();
        Active.Focus = focus;
        Active.Build();
        return Active;
    }

    /// <summary>The opening's texture, as EntranceMenu.GoldGradient makes it (128 x 128, linear, the same colours and falloff).</summary>
    public static Texture2D Texture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "Entrance radial gold light", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float nx = (x + 0.5f) / size * 2f - 1f, ny = (y + 0.5f) / size * 2f - 1f;
            float glow = Mathf.Pow(1f - Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny)), 1.5f);
            pixels[y * size + x] = Color.Lerp(Edge, Centre, glow);
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>The project's scene change, its own fade as short as it goes (it is under the gold). True only if SceneFader
    /// really began it: a busy fader or a scene not in the build returns without calling BeforeSceneChange.</summary>
    public static bool LoadWithFader(string scene)
    {
        bool began = false;
        var before = SceneFader.BeforeSceneChange;
        SceneFader.BeforeSceneChange = name => { began = true; return before != null ? before(name) : Nothing(); };
        try { SceneFader.Instance.FadeToScene(scene, 0.05f, null, 0.05f); }
        finally { SceneFader.BeforeSceneChange = before; }
        return began;
    }

    static System.Collections.IEnumerator Nothing() { yield break; }

    /// <summary>The load is over: SceneFader reports a scene up or a failure (both cleared when a load begins).</summary>
    public static bool FaderArrived(string scene) => SceneFader.Instance.LastCompletedScene != null || SceneFader.Instance.LastFailure != null;

    void Build()
    {
        var go = new GameObject("Gold", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        quad = go.transform;
        var mesh = new Mesh { name = "VG_GoldLight" };
        mesh.vertices = new[] { new Vector3(-1f, -1f), new Vector3(1f, -1f), new Vector3(1f, 1f), new Vector3(-1f, 1f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);   // never culled
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        mat = new Material(Canvas.GetDefaultCanvasMaterial()) { mainTexture = Texture(), renderQueue = 5000 };   // a RawImage's material, as in the opening
        mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingOrder = short.MaxValue;   // after SceneFader's sphere in the same queue
        Paint();
        Application.onBeforeRender += Place;
    }

    void Update() => Step(Time.unscaledDeltaTime);
    void LateUpdate() => Place();

    public void Step(float dt)
    {
        t += dt;
        switch (Now)
        {
            case Phase.Rising:
                Alpha = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(rise, 1e-3f));   // eased, and never down on the way up
                if (Alpha < 1f) break;
                FullAfter = t;
                Full?.Invoke();
                if (ActiveScene() == from && Load(Scene)) { Now = Phase.Loading; waited = 0f; }
                else { failed = true; Now = Phase.Clearing; }
                break;
            case Phase.Loading:
                waited += dt;
                if (Arrived(Scene) && ++framesUp >= 2)   // the next scene has drawn under the gold
                {
                    failed = ActiveScene() != Scene;   // the load ended somewhere else
                    Now = Phase.Clearing;
                }
                else if (waited > TimeoutSeconds) { Debug.LogWarning($"[VG_GoldLight] {Scene} wasn't up after {TimeoutSeconds:F0} s; clearing the gold anyway."); Now = Phase.Clearing; }
                break;
            case Phase.Clearing:
                Alpha = Mathf.MoveTowards(Alpha, 0f, dt / ClearSeconds);
                if (Alpha > 0f) break;
                Debug.Log($"[VG_GoldLight] full gold {t - FullAfter - ClearSeconds:F2} s -> {(failed ? "no scene change" : Scene)}");
                try { if (failed) Failed?.Invoke(); }
                finally { End(); }
                return;
        }
        Paint();
    }

    void Paint() { if (mat != null) mat.color = new Color(1f, 1f, 1f, Alpha); }

    /// <summary>In front of the eye, filling the view; while it rises its centre moves onto Focus.</summary>
    public void Place()
    {
        var eye = View != null ? View : Camera.main != null ? Camera.main.transform : null;
        if (eye == null || quad == null) return;
        var cam = eye.GetComponent<Camera>();
        float d = Mathf.Max(cam != null ? cam.nearClipPlane * 2f : 0f, Distance);
        var focus = Now == Phase.Rising && Focus != null ? Focus() : null;
        if (focus.HasValue)
        {
            var local = eye.InverseTransformPoint(focus.Value);
            if (local.z > 0.05f) Offset = Vector2.ClampMagnitude(new Vector2(local.x, local.y) / local.z, Mathf.Tan(CentreMaxDeg * Mathf.Deg2Rad));
        }
        quad.SetPositionAndRotation(eye.position + eye.rotation * new Vector3(Offset.x * d, Offset.y * d, d), eye.rotation);
        quad.localScale = Vector3.one * (HalfSizePerDistance * d);
    }

    /// <summary>Gone, with what it made (also for the checks, in edit mode, where OnDestroy isn't called).</summary>
    public void End()
    {
        Release();
        if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
    }

    void OnDestroy() => Release();

    void Release()
    {
        Application.onBeforeRender -= Place;
        if (Active == this) Active = null;
        foreach (var o in new UnityEngine.Object[] { mat != null ? mat.mainTexture : null, mat, quad != null ? quad.GetComponent<MeshFilter>().sharedMesh : null })
            if (o != null) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
        mat = null;
    }
}
