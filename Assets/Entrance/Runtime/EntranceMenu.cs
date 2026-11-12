using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.UI;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace InnerFrame.Entrance
{
    /// <summary>World-locked entrance painting with editable TMP signs and XR ray targets.</summary>
    public sealed class EntranceMenu : MonoBehaviour
    {
        [SerializeField] Renderer painting;
        [SerializeField] Texture2D titlePlate, squarePlate, door, repaint;
        [SerializeField] Texture2D sign, tile, slot, frame, boy, lady;
        [SerializeField] string galleryScene = "Gallery";
        EntranceAudioDirector audioDirector;
        Canvas canvas;
        RawImage backdrop;
        RawImage transition;
        RawImage galleryGold;
        Texture2D goldGradient;
        VideoPlayer interludePlayer;
        RenderTexture interludeBuffer;
        RectTransform root;
        readonly List<GameObject> controls = new List<GameObject>();
        readonly List<EntranceHoverFeedback> cardBorders = new List<EntranceHoverFeedback>();
        TextMeshProUGUI codeLabel;
        Button joinButton;
        Button storyButton;
        VisitorRole role;
        bool hasRole;
        string code = "";
        bool busy;
        bool skipIntro;
        Camera eyes;
        GraphicRaycaster uiRaycaster;
        TMP_FontAsset brushFont;
        readonly List<Sprite> sprites = new List<Sprite>();
        public bool Ready { get; private set; }
        public int CurrentPage { get; private set; }
        public int IntroBeat { get; private set; } = -1;
        public bool VanGoghInterludePlaying { get; private set; }
        public Camera ViewingCamera => eyes;
        public string EnteredCode => code;
#if UNITY_EDITOR
        public bool CaptureMode { get; set; }
        Behaviour desktopNavigation;
        bool desktopWasEnabled;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        bool ownsCursor;
#endif
        const float W = 1536, H = 1024;

        IEnumerator Start()
        {
            // The project's desktop test rig replaces the inactive XR camera
            // during scene startup. Wait before binding the UI camera.
            yield return null;
            yield return null;
            eyes = Camera.main;
            if (eyes == null || painting == null) { Debug.LogError("[Entrance] Main Camera or painting missing.", this); enabled = false; yield break; }
            brushFont = Resources.Load<TMP_FontAsset>("Entrance/BrushFontHD");
            if (brushFont == null)
            {
                Debug.LogError("[Entrance] Brush font missing. Run Tools > InnerFrame > Entrance > Prepare Quest Visuals.", this);
                enabled = false;
                yield break;
            }
            var facing = Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up);
            if (facing.sqrMagnitude < .001f) facing = Vector3.forward;
            transform.SetPositionAndRotation(eyes.transform.position + facing.normalized * 3f,
                Quaternion.LookRotation(facing.normalized, Vector3.up));
            // The Canvas supplies the background. Disable the old quad so it
            // cannot obscure the controls or show a second mirrored painting.
            painting.enabled = false;
            // Retire only the generated old frame; the new border shares the
            // exact canvas bounds and never participates in animation zoom.
            foreach (Transform child in transform)
                if (child.name.EndsWith(" gold frame")) child.gameObject.SetActive(false);
            gameObject.AddComponent<EntranceGoldRay>();
            foreach(var sceneRoot in gameObject.scene.GetRootGameObjects())
                if(sceneRoot.name=="Entrance floor" || sceneRoot.name=="Entrance floor collider")
                {
                    var surface=sceneRoot.GetComponent<Renderer>();
                    if(surface!=null) surface.enabled=false;
                }
#if UNITY_EDITOR
            if (!UnityEngine.XR.XRSettings.isDeviceActive)
            {
                previousCursorLock=Cursor.lockState;
                previousCursorVisible=Cursor.visible;
                ownsCursor=true;
                foreach(var behaviour in eyes.GetComponentsInParent<MonoBehaviour>())
                {
                    if(behaviour.GetType().Name!="DesktopTestRig") continue;
                    desktopNavigation=behaviour;
                    desktopWasEnabled=behaviour.enabled;
                    behaviour.enabled=false;
                    break;
                }
                Cursor.lockState=CursorLockMode.None;
                Cursor.visible=true;
            }
#endif
            var system = FindAnyObjectByType<EventSystem>();
            if (system == null)
            {
                var eventGo = new GameObject("Entrance XR EventSystem");
                system=eventGo.AddComponent<EventSystem>();
                eventGo.AddComponent<XRUIInputModule>();
            }
            else
            {
                var xrModule = system.GetComponent<XRUIInputModule>();
                if (xrModule == null) xrModule = system.gameObject.AddComponent<XRUIInputModule>();
                foreach (var module in system.GetComponents<BaseInputModule>()) module.enabled = module == xrModule;
                xrModule.enableMouseInput = true;
                xrModule.enableXRInput = true;
            }
            system.GetComponent<XRUIInputModule>().enableBuiltinActionsAsFallback=true;
            audioDirector = EntranceAudioDirector.Ensure();
            audioDirector.PlayOpening();
            BuildCanvas(); Show(0);
            yield return OpeningReveal();
            Ready = true;
        }

        void BuildCanvas()
        {
            var go = new GameObject("Editable painted signs", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0, -.03f);
            go.transform.localRotation = Quaternion.identity;
            root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(W,H); root.localScale = Vector3.one * (4.8f / W);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = eyes;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            uiRaycaster = go.AddComponent<GraphicRaycaster>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            // Mask all animated art BEFORE adding the stationary border/UI.
            var viewport = new GameObject("Painting viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(root, false);
            ((RectTransform)viewport.transform).sizeDelta = new Vector2(W,H);
            AddFrameBar("Top", new Vector2(0,H/2+9), new Vector2(W+36,18));
            AddFrameBar("Bottom", new Vector2(0,-H/2-9), new Vector2(W+36,18));
            AddFrameBar("Left", new Vector2(-W/2-9,0), new Vector2(18,H));
            AddFrameBar("Right", new Vector2(W/2+9,0), new Vector2(18,H));
            var background = new GameObject("Opening painting image", typeof(RectTransform), typeof(RawImage));
            background.transform.SetParent(viewport.transform, false);
            var bounds = (RectTransform)background.transform;
            bounds.anchoredPosition = Vector2.zero;
            bounds.sizeDelta = new Vector2(W, H);
            backdrop = background.GetComponent<RawImage>();
            backdrop.raycastTarget = false;
            var veil = new GameObject("Painted image dissolve", typeof(RectTransform), typeof(RawImage));
            veil.transform.SetParent(viewport.transform, false);
            var veilBounds = (RectTransform)veil.transform;
            veilBounds.anchoredPosition = Vector2.zero;
            veilBounds.sizeDelta = new Vector2(W, H);
            transition = veil.GetComponent<RawImage>();
            transition.raycastTarget = false;
            transition.color = Color.clear;
            var gold = new GameObject("Full screen Gallery gold", typeof(RectTransform), typeof(RawImage));
            gold.transform.SetParent(root, false);
            var goldRect = (RectTransform)gold.transform;
            goldRect.sizeDelta = new Vector2(W * 3f, H * 3f);
            galleryGold = gold.GetComponent<RawImage>();
            galleryGold.raycastTarget = false;
            galleryGold.texture = GoldGradient();
            galleryGold.color = Color.clear;
            gold.SetActive(false);
        }

        void AddFrameBar(string label, Vector2 position, Vector2 size)
        {
            var bar=new GameObject(label+" stationary gold border",typeof(RectTransform),typeof(Image));
            bar.transform.SetParent(root,false);
            var r=(RectTransform)bar.transform;r.anchoredPosition=position;r.sizeDelta=size;
            var image=bar.GetComponent<Image>();image.color=new Color(.86f,.62f,.13f);image.raycastTarget=false;
        }

        Sprite MakeSprite(Texture2D tex)
        {
            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
            sprites.Add(sprite);
            return sprite;
        }

        RectTransform AddImage(string name, Texture2D texture, float x, float y, float w, float h, bool hit = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w,h);
            var image = go.GetComponent<Image>(); image.sprite = MakeSprite(texture); image.preserveAspect = false;
            image.raycastTarget = hit;
            controls.Add(go);
            return rect;
        }

        TextMeshProUGUI Text(RectTransform parent, string content, float size)
        {
            var go = new GameObject("Editable label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = parent.sizeDelta * .82f;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = brushFont;
            text.text = content; text.fontSize = size; text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.04f,.11f,.25f); text.raycastTarget = false;
            text.enableAutoSizing = false;
            return text;
        }

        Button Sign(string caption, float x, float y, float w, float h, System.Action click, float font = 62)
        {
            var rect = AddImage(caption + " sign",sign,x,y,w,h,true);
            rect.GetComponent<Image>().type = UnityEngine.UI.Image.Type.Simple;
            Text(rect,caption,font);
            var button = rect.gameObject.AddComponent<Button>();
            EntranceButtonSound.Bind(button);
            var colors = button.colors; colors.highlightedColor = new Color(1.3f,1.13f,.72f,1f);
            colors.pressedColor = new Color(.83f,.68f,.48f,1f); button.colors = colors;
            button.onClick.AddListener(() => { if (!busy) click(); });
            rect.gameObject.AddComponent<EntranceHoverFeedback>().Configure(button,rect,rect.GetComponent<Image>());
            return button;
        }

        void Card(string caption, Texture2D portrait, VisitorRole choice, float x)
        {
            // Match the INNER opening to the source portrait aspect ratio.
            // Cover the widest inner aperture (x=45..698) without stretching
            // the portrait. The painted bevel overlaps only its edge pixels.
            const float frameHeight=400f;
            float openingHeight=frameHeight*1072f/1265f;
            float openingWidth=openingHeight*portrait.width/portrait.height;
            float frameWidth=openingWidth*745f/653f;
            var group=new GameObject(caption+" card group",typeof(RectTransform));
            group.transform.SetParent(root,false);
            var groupRect=(RectTransform)group.transform;
            groupRect.anchoredPosition=new Vector2(x,60);
            groupRect.sizeDelta=new Vector2(frameWidth,frameHeight);
            controls.Add(group);
            var image=AddImage(caption+" portrait",portrait,0,0,openingWidth,openingHeight,true);
            image.SetParent(groupRect,false);
            var frameImage=AddImage(caption+" frame",frame,0,0,frameWidth,frameHeight);
            frameImage.SetParent(groupRect,false);
            var button=image.gameObject.AddComponent<Button>();
            EntranceButtonSound.Bind(button);
            button.transition=Selectable.Transition.None;
            button.onClick.AddListener(()=>{if(!busy)SelectRole(choice);});
            var feedback=image.gameObject.AddComponent<EntranceHoverFeedback>();
            feedback.Configure(button,groupRect,frameImage.GetComponent<Image>());
            cardBorders.Add(feedback);
            var nameSign=Sign(caption,x,-260,290,100,()=>SelectRole(choice),40);
            cardBorders.Add(nameSign.GetComponent<EntranceHoverFeedback>());
        }

        void SelectRole(VisitorRole choice)
        {
            role = choice;
            hasRole = true;
            EntranceChoice.SetRole(choice);
            RefreshCards();
            if (storyButton != null) storyButton.interactable = true;
        }

        void RefreshCards()
        {
            for (int i=0;i<cardBorders.Count;i++)
            {
                cardBorders[i].SetChosen(hasRole && (int)role == i/2);
            }
        }

        void Clear()
        {
            foreach (var go in controls) { go.SetActive(false); Destroy(go); }
            foreach (var sprite in sprites) Destroy(sprite);
            sprites.Clear();
            controls.Clear(); cardBorders.Clear(); codeLabel = null; joinButton = null; storyButton = null;
        }

        void Show(int page)
        {
            CurrentPage = page;
            Clear();
            SetPainting(page == 0 ? titlePlate : squarePlate);
            switch (page)
            {
                case 0:
                    Sign("Begin",0,-230,450,215,() => Show(1),100);
                    break;
                case 1:
                    Sign("Choose Who You'll Be",0,395,860,110,() => {},66);
                    Card("Boy",boy,VisitorRole.Boy,-285);
                    Card("Girl",lady,VisitorRole.LadyInRed,285);
                    RefreshCards();
                    Sign("Back",-470,-422,270,100,() => Show(0),47);
                    storyButton = Sign("Enter the Story",350,-422,540,100,() => Show(2),50);
                    storyButton.interactable = hasRole;
                    break;
                case 2:
                    Sign("New Room",-260,10,445,250,() => { code = Random.Range(1000,10000).ToString(); Show(3); },70);
                    Sign("Join Room",260,10,445,250,() => { code = ""; Show(4); },70);
                    Sign("Back",0,-345,300,105,() => Show(1),53);
                    break;
                case 3:
                    Sign("Your room code",0,180,570,140,() => {},55);
                    var display = AddImage("Room code plate",slot,0,-20,610,195);
                    codeLabel = Text(display,code,118);
                    codeLabel.color = new Color(1f,.91f,.65f);
                    Sign("Start",-240,-313,370,135,Enter,68);
                    Sign("Back",235,-313,290,125,() => Show(2),54);
                    break;
                case 4:
                    Sign("Enter room code",0,340,620,135,() => {},55);
                    var plate = AddImage("Four code slots",slot,0,190,520,120);
                    codeLabel = Text(plate,"····",90);
                    codeLabel.color = new Color(1f,.91f,.65f);
                    for (int i=0;i<9;i++)
                    {
                        int digit=i+1;
                        Tile(digit.ToString(),-145+i%3*145,48-i/3*135,()=> Digit(digit));
                    }
                    Tile("DEL",-145,-362,()=> { if (code.Length>0) code=code.Substring(0,code.Length-1); RefreshCode(); });
                    Tile("0",0,-362,()=> Digit(0));
                    Sign("Join",310,-296,250,108,Enter,55);
                    joinButton = controls[controls.Count-1].GetComponent<Button>();
                    joinButton.interactable = false;
                    Sign("Back",-490,-430,230,92,() => Show(2),44);
                    break;
            }
        }

        void Tile(string caption,float x,float y,System.Action action)
        {
            var rect=AddImage("Key "+caption,tile,x,y,113,113,true);
            Text(rect,caption,57);
            var button=rect.gameObject.AddComponent<Button>();
            EntranceButtonSound.Bind(button);
            button.onClick.AddListener(() => { if (!busy) action(); });
            rect.gameObject.AddComponent<EntranceHoverFeedback>().Configure(button,rect,rect.GetComponent<Image>());
        }

        void Digit(int n)
        {
            if (code.Length==4) return;
            code += n.ToString(); RefreshCode();
        }
        void RefreshCode()
        {
            if(codeLabel!=null) codeLabel.text=code.PadRight(4,'·');
            if(joinButton!=null) joinButton.interactable=code.Length==4;
        }

        void Enter()
        {
            if (busy || (CurrentPage != 3 && CurrentPage != 4) || !EntranceChoice.SetRoomCode(code)) return;
            EntranceChoice.SetRole(role);
            var template = Resources.Load<GameObject>("EntranceControllerGuide");
            if (template == null) { Debug.LogError("Missing ControllerTutorial prefab"); return; }
            Clear();
            CurrentPage = 5;
            var panel = new GameObject("Controller Tutorial", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(root, false);
            rect.sizeDelta = ((RectTransform)template.transform).sizeDelta;
            rect.anchoredPosition3D = new Vector3(0, 0, -.1f);
            var layout = Instantiate(template);
            layout.SetActive(false);
            // The standalone preview supplies its own painting. Entrance already displays it.
            for (int i = 0; i < layout.transform.childCount;)
            {
                var element = layout.transform.GetChild(i);
                if (element.name == "Opening background") { i++; continue; }
                element.SetParent(rect, false);
            }
            Destroy(layout);
            controls.Add(panel);
            foreach (var button in panel.GetComponentsInChildren<Button>())
            {
                button.onClick = new Button.ButtonClickedEvent();
                EntranceButtonSound.Bind(button);
                button.gameObject.AddComponent<EntranceHoverFeedback>().Configure(
                    button, (RectTransform)button.transform, button.targetGraphic);
                button.onClick.AddListener(FinishControllerTutorial);
            }
        }

        void FinishControllerTutorial()
        {
            if (busy || CurrentPage != 5) return;
            CurrentPage = 6;
            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            busy=true;
            audioDirector.PlayLanding1();
            skipIntro=false;
            Ready=false;
            Clear();
            var skip=AddImage("Skip intro sign",sign,640,-435,170,66,true);
            Text(skip,"Skip",34);
            var skipButton=skip.gameObject.AddComponent<Button>();
            EntranceButtonSound.Bind(skipButton);
            skipButton.onClick.AddListener(()=>skipIntro=true);
            skip.gameObject.AddComponent<EntranceHoverFeedback>().Configure(skipButton,skip,skip.GetComponent<Image>());
            var images=new[]{squarePlate,door,repaint};
            var durations=new[]{2.5f,2.5f,2.2f};
            var starts=new[]{1.03f,1.03f,1.09f};
            var ends=new[]{1.10f,1.13f,1.03f};
            var originalPosition=root.anchoredPosition3D;
            var originalScale=backdrop.rectTransform.localScale;
            for (int i=0;i<images.Length && !skipIntro;i++)
            {
                if (i == 0) SetPainting(images[i]);
                else
                {
                    transition.texture=images[i];
                    for (float fade=0;fade<.375f && !skipIntro;fade+=Time.unscaledDeltaTime)
                    {
                        transition.color=new Color(1f,1f,1f,Mathf.Clamp01(fade/.375f));
                        yield return null;
                    }
                    SetPainting(images[i]);
                    transition.color=Color.clear;
                }
                if(skipIntro) break;
                IntroBeat=i;
                for (float t=0;t<durations[i] && !skipIntro;t+=.125f)
                {
                    // The Canvas is in world units; jitter the image in UI
                    // pixels, never move the whole painting by three metres.
                    backdrop.rectTransform.anchoredPosition=new Vector2(Random.Range(-3f,3f),Random.Range(-3f,3f));
                    backdrop.rectTransform.localScale=originalScale*Mathf.Lerp(starts[i],ends[i],t/durations[i]);
                    yield return new WaitForSecondsRealtime(.125f);
                }
            }
            root.anchoredPosition3D=originalPosition;
            backdrop.rectTransform.anchoredPosition=Vector2.zero;
            backdrop.rectTransform.localScale=originalScale;
            // C4 finishes before the painted Van Gogh title. The following
            // landing animation dissolves directly over its final video frame.
            IntroBeat=3;
            if (!skipIntro) yield return PlayVanGoghInterlude();
            Clear();
            if (skipIntro)
            {
                EndVanGoghInterlude();
                transition.texture=GoldGradient();
                transition.color=Color.white;
                yield return EnterGallery();
            }
            else yield return PaintingPreview();
        }

        IEnumerator EnterGallery()
        {
            const string target = "Assets/Scenes/Gallery.unity";
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogError("[Entrance] Cannot load " + target + ": scene is not enabled in Build Profiles.",this);
                if(galleryGold!=null) galleryGold.gameObject.SetActive(false);
                yield break;
            }
            var fader = SceneFader.Instance;
            if (fader.IsTransitioning) yield break;
            if(galleryGold!=null)
            {
                galleryGold.gameObject.SetActive(true);
                galleryGold.color=Color.white;
            }
            busy = true;
            Ready = false;
            audioDirector.PlayGold();
            fader.FadeToScene(target,1.2f,GoldGradient());
            // On success this Entrance object is unloaded. If loading fails while
            // Entrance still exists, restore the buttons so the user can retry.
            while (fader.IsTransitioning) yield return null;
            if (this != null && gameObject.scene.isLoaded)
            {
                busy=false;
                Ready=true;
                if(galleryGold!=null) galleryGold.gameObject.SetActive(false);
                Debug.LogError("[Entrance] Gallery transition ended without loading Gallery: " + fader.LastFailure,this);
            }
        }

        IEnumerator PaintingPreview()
        {
            var a=Resources.Load<EntranceTransitionAssets>("Entrance/TransitionAssets");
            if(a==null || a.wake==null || a.burst==null || a.inside==null)
            { Debug.LogError("[Entrance] Run Tools > InnerFrame > Entrance > Prepare Land Transitions."); yield break; }
            audioDirector.PlayLanding2();
            busy=true; Ready=false; Clear(); transition.color=Color.clear;
            var frames=new[]{a.wake,a.burst,a.inside};
            var durations=new[]{1.75f,1.75f,2f};
            var start=new[]{1.03f,1.03f,1.08f}; var end=new[]{1.10f,1.12f,1.03f};
            for(int i=0;i<3;i++)
            {
                transition.texture=frames[i];
                for(float t=0;t<.375f;t+=Time.unscaledDeltaTime)
                { transition.color=new Color(1,1,1,t/.375f); yield return null; }
                SetPainting(frames[i]); transition.color=Color.clear;
                if(i==0) EndVanGoghInterlude();
                IntroBeat=4+i;
                for(float t=0;t<durations[i];t+=.125f)
                { backdrop.rectTransform.localScale=Vector3.one*Mathf.Lerp(start[i],end[i],t/durations[i]); yield return new WaitForSecondsRealtime(.125f); }
            }
            transition.texture=GoldGradient(); IntroBeat=7;
            audioDirector.PlayGold();
            if(galleryGold!=null) galleryGold.gameObject.SetActive(true);
            for(float t=0;t<2.2f;t+=Time.unscaledDeltaTime)
            { float v=Mathf.FloorToInt(t*8)%4<2?1f:.9f; galleryGold.color=new Color(v,v,v,Mathf.Clamp01(t/.6f)); yield return null; }
            busy=false;
            yield return EnterGallery();
        }

        IEnumerator PlayVanGoghInterlude()
        {
            var clip=Resources.Load<VideoClip>("Entrance/VanGoghHandwriting");
            if(clip==null)
            {
                Debug.LogError("[Entrance] Van Gogh interlude video missing from Entrance Resources.",this);
                yield break;
            }
            var buffer=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32);
            buffer.Create();
            var player=gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake=false;
            player.isLooping=false;
            player.audioOutputMode=VideoAudioOutputMode.None;
            player.source=VideoSource.VideoClip;
            player.clip=clip;
            player.renderMode=VideoRenderMode.RenderTexture;
            player.targetTexture=buffer;
            player.Prepare();
            float wait=0f;
            while(!player.isPrepared && wait<8f && !skipIntro)
            { wait+=Time.unscaledDeltaTime; yield return null; }
            if(player.isPrepared && !skipIntro)
            {
                transition.texture=buffer;
                transition.color=Color.white;
                player.Play();
                audioDirector.FollowHandwriting(player);
                VanGoghInterludePlaying=true;
                float elapsed=0f;
                while(!skipIntro && elapsed<clip.length+.5f && (player.isPlaying || elapsed<.3f))
                { elapsed+=Time.unscaledDeltaTime; yield return null; }
            }
            else if(!skipIntro) Debug.LogError("[Entrance] Van Gogh video could not prepare.",this);
            audioDirector.EndHandwriting();
            VanGoghInterludePlaying=false;
            if (player.isPrepared && !skipIntro)
            {
                // Hold the final painted frame behind the dissolve into the landing.
                player.Pause();
                backdrop.texture=buffer;
                interludePlayer=player;
                interludeBuffer=buffer;
                transition.color=Color.clear;
                transition.texture=null;
            }
            else
            {
                player.Stop();
                player.targetTexture=null;
                Destroy(player);
                buffer.Release(); Destroy(buffer);
            }
        }

        void EndVanGoghInterlude()
        {
            if (audioDirector != null) audioDirector.EndHandwriting();
            if(interludePlayer!=null)
            {
                interludePlayer.Stop();
                interludePlayer.targetTexture=null;
                Destroy(interludePlayer);
                interludePlayer=null;
            }
            if(interludeBuffer!=null)
            {
                interludeBuffer.Release();
                Destroy(interludeBuffer);
                interludeBuffer=null;
            }
        }

        Texture2D GoldGradient()
        {
            if (goldGradient != null) return goldGradient;
            const int size=128;
            goldGradient=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
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

        void OnDestroy()
        {
            EndVanGoghInterlude();
            if(goldGradient!=null) Destroy(goldGradient);
            foreach(var sprite in sprites) if(sprite!=null) Destroy(sprite);
#if UNITY_EDITOR
            if(desktopNavigation!=null) desktopNavigation.enabled=desktopWasEnabled;
            if(ownsCursor)
            {
                Cursor.lockState=previousCursorLock;
                Cursor.visible=previousCursorVisible;
            }
#endif
        }

        IEnumerator OpeningReveal()
        {
            transition.texture=null;
            for (float t=0;t<.85f;t+=Time.unscaledDeltaTime)
            {
                transition.color=new Color(.02f,.02f,.08f,1f-Mathf.Clamp01(t/.85f));
                yield return null;
            }
            transition.color=Color.clear;
        }

        void SetPainting(Texture2D texture)
        {
            // URP's Unlit shader samples _BaseMap; mainTexture is not reliable
            // across shader variants. The canvas image guarantees the same
            // artwork is visible behind the editable UI in the Editor and XR.
            if (backdrop != null) backdrop.texture = texture;
        }

#if UNITY_EDITOR
        public Button ReviewButton(string objectName)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
                if (button.gameObject.name == objectName) return button;
            return null;
        }

        public void ReviewClick(string objectName)
        {
            var button=ReviewButton(objectName);
            if(button==null || !button.IsInteractable()) throw new System.InvalidOperationException("Button unavailable: "+objectName);
            Canvas.ForceUpdateCanvases();
            var point=RectTransformUtility.WorldToScreenPoint(eyes,button.transform.position);
            var hits=new List<RaycastResult>();
            uiRaycaster.Raycast(new PointerEventData(EventSystem.current){position=point},hits);
            bool found=false;
            foreach(var hit in hits)
                if(hit.gameObject.GetComponentInParent<Button>()==button) { found=true; break; }
            if(!found) throw new System.InvalidOperationException("UI ray cannot reach "+objectName+". Check Canvas orientation and camera.");
            button.onClick.Invoke();
        }

        // The project's desktop test rig deliberately disables UI on its XR ray.
        // E at the aiming dot is a reliable Editor-only way to exercise the
        // same Button callbacks without changing Quest controller input.
        void Update()
        {
            if (CaptureMode || busy || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame ||
                uiRaycaster == null || EventSystem.current == null || eyes == null) return;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(eyes.pixelWidth * .5f, eyes.pixelHeight * .5f)
            };
            var hits = new List<RaycastResult>();
            uiRaycaster.Raycast(pointer, hits);
            foreach (var hit in hits)
            {
                var button = hit.gameObject.GetComponentInParent<Button>();
                if (button == null || !button.IsActive() || !button.IsInteractable()) continue;
                button.onClick.Invoke();
                return;
            }
        }
#endif
    }
}
