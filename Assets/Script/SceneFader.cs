using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// VR fade using a sphere that surrounds the headset camera.
/// Fades to black, or to an image if you pass one in.
/// Creates itself automatically the first time it's used.
/// </summary>
public class SceneFader : MonoBehaviour
{
    static SceneFader instance;

    public static SceneFader Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("SceneFader");
                instance = go.AddComponent<SceneFader>();
                instance.BuildSphere();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    Renderer sphereRenderer;
    Transform sphere;
    Material mat;
    Camera cam;
    Color tint = Color.whiteSmoke;
    bool busy;
    public bool IsTransitioning => busy;
    public static bool IsFading => instance != null && instance.busy;
    public float Coverage => mat != null && sphereRenderer.enabled ? mat.color.a : 0f;
    public string LastCompletedScene { get; private set; }
    public string LastFailure { get; private set; }
    public static System.Func<string, IEnumerator> BeforeSceneChange;
    Texture2D ownedImage;
    TMPro.TextMeshPro loadingLabel;
    float holdStarted;
    bool holdingGold;

    void LateUpdate()
    {
        if (!holdingGold || mat == null) return;
        float pulse = Mathf.FloorToInt((Time.unscaledTime-holdStarted)*8f)%4 < 2 ? 1f : .9f;
        mat.color = new Color(pulse,pulse,pulse,1f);
        FollowCamera();
        if (loadingLabel != null && cam != null)
            loadingLabel.transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * .7f,
                cam.transform.rotation);
    }

    void ShowLoading(string destination)
    {
        if (ownedImage == null) return;
        holdingGold = true;
        holdStarted = Time.unscaledTime;
        var go = new GameObject("Entrance loading status");
        go.transform.SetParent(transform,false);
        loadingLabel = go.AddComponent<TMPro.TextMeshPro>();
        var font = Resources.Load<TMPro.TMP_FontAsset>("Entrance/BrushFont");
        if (font != null) loadingLabel.font = font;
        loadingLabel.text = destination == "Gallery" ? "Loading the gallery…" : "Loading The Bedroom…";
        loadingLabel.fontSize = 2;
        loadingLabel.alignment = TMPro.TextAlignmentOptions.Center;
        loadingLabel.rectTransform.sizeDelta = new Vector2(1.1f,.15f);
        loadingLabel.color = new Color(.12f,.07f,.02f);
        loadingLabel.fontSharedMaterial = new Material(loadingLabel.fontSharedMaterial);
        loadingLabel.fontSharedMaterial.renderQueue = 5010;
        loadingLabel.fontSharedMaterial.SetFloat("_ZTestMode",8);
    }

    void HideLoading()
    {
        holdingGold = false;
        if (loadingLabel != null)
        {
            Destroy(loadingLabel.fontSharedMaterial);
            Destroy(loadingLabel.gameObject);
            loadingLabel = null;
        }
    }


    public void SetTransitionImage(Texture2D image)
    {
        if (ownedImage != null) Destroy(ownedImage);
        ownedImage = image != null ? Instantiate(image) : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { instance = null; BeforeSceneChange = null; }


    void OnEnable()  { Application.onBeforeRender += FollowCamera; }
    void OnDisable() { Application.onBeforeRender -= FollowCamera; }

    void BuildSphere()
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(s.GetComponent<Collider>());
        s.name = "FadeSphere";
        s.transform.SetParent(transform, false);
        sphere = s.transform;

        mat = new Material(Shader.Find("Sprites/Default"));
        mat.renderQueue = 5000; // draw last, on top of everything

        sphereRenderer = s.GetComponent<Renderer>();
        sphereRenderer.sharedMaterial = mat;
        sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sphereRenderer.receiveShadows = false;
        sphereRenderer.enabled = false;
    }

    void FollowCamera()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null && Camera.allCamerasCount > 0) cam = Camera.allCameras[0];
            if (cam == null) return;
        }

        sphere.position = cam.transform.position;

        float radius = Mathf.Max(cam.nearClipPlane * 3f, 0.1f);
        float d = radius * 2f;
        // Negative X flips the image so it isn't mirrored when seen from inside
        sphere.localScale = new Vector3(-d, d, d);
    }

    /// <param name="fadeImage">Optional. Leave null for plain black.</param>
    public void FadeToScene(string sceneName, float fadeTime = 1f, Texture2D fadeImage = null)
    {
        if (busy) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            LastFailure = "Scene is not enabled in Build Settings: " + sceneName;
            Debug.LogError("[SceneFader] " + LastFailure);
            return;
        }
        busy = true;
        LastFailure = null;
        LastCompletedScene = null;
        SetTransitionImage(fadeImage);
        StartCoroutine(Guard(FadeRoutine(sceneName, Mathf.Max(.01f, fadeTime), fadeImage)));
    }

    IEnumerator FadeRoutine(string sceneName, float fadeTime, Texture2D fadeImage)
    {
        if (BeforeSceneChange != null) yield return BeforeSceneChange(sceneName);

        // Image: white tint so it shows true colours. No image: black.
        mat.mainTexture = ownedImage;
        tint = ownedImage != null ? Color.white : Color.black;

        yield return Fade(0f, 1f, fadeTime);              // fade out

        ShowLoading(sceneName);

        if (NetworkSession.Instance != null)              // leave this scene's multiplayer room
            yield return NetworkSession.Instance.Leave();

        var op = SceneManager.LoadSceneAsync(sceneName);  // load new scene
        if (op == null) throw new System.InvalidOperationException("Scene load did not start: " + sceneName);
        while (!op.isDone) yield return null;

        cam = null;                                       // find the new scene's camera
        yield return null;

        // Let the destination choose its XR/desktop rig before revealing it.
        yield return null;
        float cameraDeadline = Time.realtimeSinceStartup + 10f;
        while (Camera.main == null && Time.realtimeSinceStartup < cameraDeadline) yield return null;
        if (Camera.main == null) throw new System.InvalidOperationException("Destination has no Main Camera: " + sceneName);
        cam = Camera.main;
        if (ownedImage != null && NetworkSession.Instance != null)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((NetworkSession.Instance.Runner == null || !NetworkSession.Instance.Runner.IsRunning)
                && Time.realtimeSinceStartup < deadline) yield return null;
            if (NetworkSession.Instance.Runner == null || !NetworkSession.Instance.Runner.IsRunning)
                Debug.LogWarning("[Entrance] Scene loaded, but multiplayer is not connected after 30 seconds. Revealing local scene; this is NOT a multiplayer pass.");
        }
        HideLoading();
        FollowCamera();
        yield return Fade(1f, 0f, Mathf.Max(fadeTime,1.2f));              // fade in
        LastCompletedScene = sceneName;
        Debug.Log("[Entrance] Land complete: " + sceneName + ". Physical Quest validation remains pending.");
        mat.mainTexture = null;
        if (ownedImage != null) Destroy(ownedImage);
        ownedImage = null;
        busy = false;
    }

    IEnumerator Guard(IEnumerator routine)
    {
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            bool more = false;
            object next = null;
            System.Exception failure = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (System.Exception error) { failure = error; }
            if (failure != null)
            {
                LastFailure = failure.ToString();
                busy = false;
                HideLoading();
                sphereRenderer.enabled = false;
                Debug.LogException(failure);
                yield break;
            }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator nested) stack.Push(nested);
            else yield return next;
        }
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        sphereRenderer.enabled = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetAlpha(to);
        sphereRenderer.enabled = to > 0f;
    }

    void SetAlpha(float a)
    {
        mat.color = new Color(tint.r, tint.g, tint.b, a);
    }
}