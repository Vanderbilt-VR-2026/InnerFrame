using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InnerFrame.Entrance
{
    public sealed class PaintingEntryTransition : MonoBehaviour
    {
        public static int Beat { get; private set; } = -1;
        static PaintingEntryTransition instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Beat = -1;
            instance = null;
            // Bedroom entry uses the normal scene fade. Animation runs only in EntranceMenu.
        }

        static IEnumerator PlayIfNeeded(string destination)
        {
            if (SceneManager.GetActiveScene().name != "Gallery" || destination != "PaintedBedroom")
                yield break;
            Beat = -1;
            var assets = Resources.Load<EntranceTransitionAssets>("Entrance/TransitionAssets");
            if (assets == null || assets.wake == null || assets.burst == null || assets.inside == null)
                throw new System.InvalidOperationException("[Entrance] Missing transition assets. Run Tools > InnerFrame > Entrance > Prepare Land Transitions before Play/build.");
            var cam = Camera.main;
            if (cam == null) throw new System.InvalidOperationException("[Entrance] No active camera for painting entry.");
            var go = new GameObject("Painting entry P1-P4 (runtime)");
            instance = go.AddComponent<PaintingEntryTransition>();
            // Fixed in world space. Neither the camera nor the player is animated.
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            go.transform.SetPositionAndRotation(cam.transform.position + forward.normalized * 3f,
                Quaternion.LookRotation(forward.normalized, Vector3.up));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 200;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1536, 1024);
            rect.localScale = Vector3.one * (3.6f / 1536f);
            var image = MakeImage(go.transform, "Painting still");
            var overlay = MakeImage(go.transform, "Crossfade");
            overlay.color = Color.clear;
            Texture2D[] frames = {assets.wake, assets.burst, assets.inside};
            float[] lengths = {1.75f, 1.75f, 2f};
            float[] starts = {1.03f,1.03f,1.08f}, ends = {1.10f,1.12f,1.03f};
            for (int i = 0; i < frames.Length; i++)
            {
                if (i > 0)
                {
                    overlay.texture = frames[i];
                    for (float t = 0; t < .375f; t += Time.unscaledDeltaTime)
                    {
                        overlay.color = new Color(1, 1, 1, t / .375f);
                        yield return null;
                    }
                }
                image.texture = frames[i];
                overlay.color = Color.clear;
                Beat = i;
                for (float t = 0; t < lengths[i]; t += .125f)
                {
                    image.rectTransform.anchoredPosition = new Vector2(Random.Range(-3f, 3f), Random.Range(-3f, 3f));
                    image.rectTransform.localScale = Vector3.one * Mathf.Lerp(starts[i], ends[i], t / lengths[i]);
                    yield return new WaitForSecondsRealtime(.125f);
                }
            }
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.localScale = Vector3.one;
            // SceneFader's persistent gold overlay now covers loading and fades into the actual room.
            var gold = GoldGradient();
            SceneFader.Instance.SetTransitionImage(gold);
            Destroy(gold);
            Beat = 3;
        }

        static Texture2D GoldGradient()
        {
            const int size=128;
            var goldGradient=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
            goldGradient.name="Entrance radial gold light";
            goldGradient.wrapMode=TextureWrapMode.Clamp;
            goldGradient.filterMode=FilterMode.Bilinear;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float nx=(x+0.5f)/size*2f-1f;
                float ny=(y+0.5f)/size*2f-1f;
                float glow=Mathf.Pow(1f-Mathf.Clamp01(Mathf.Sqrt(nx*nx+ny*ny)),1.5f);
                pixels[y*size+x]=Color.Lerp(new Color(.39f,.16f,.015f),new Color(1f,.9f,.46f),glow);
            }
            goldGradient.SetPixels(pixels);
            goldGradient.Apply(false,false);
            return goldGradient;
        }


        static RawImage MakeImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
