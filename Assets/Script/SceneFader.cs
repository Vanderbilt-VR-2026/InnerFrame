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
        StartCoroutine(FadeRoutine(sceneName, fadeTime, fadeImage));
    }

    IEnumerator FadeRoutine(string sceneName, float fadeTime, Texture2D fadeImage)
    {
        busy = true;

        // Image: white tint so it shows true colours. No image: black.
        mat.mainTexture = fadeImage;
        tint = fadeImage != null ? Color.white : Color.black;

        yield return Fade(0f, 1f, fadeTime);              // fade out

        var op = SceneManager.LoadSceneAsync(sceneName);  // load new scene
        while (!op.isDone) yield return null;

        cam = null;                                       // find the new scene's camera
        yield return null;

        yield return Fade(1f, 0f, fadeTime);              // fade in
        busy = false;
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