using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Every number of the Almond Blossom studio blockout in one place (Assets/AlmondStudio/AS_Layout.asset, made by
    /// Tools > InnerFrame > Almond Studio > Build). Metres and degrees. The room's floor is at y = 0, x runs east, z north;
    /// the window is centred on the north wall, whose inner face is at z = roomDepth.
    /// </summary>
    [System.Serializable]
    public class AS_View
    {
        public string name;
        [Tooltip("The picture's file name in Textures/Reference.")] public string file;
        public Vector3 position;
        [Tooltip("Degrees round from north (+z) toward east (+x).")] public float yaw;
        [Tooltip("Degrees; negative looks up.")] public float pitch;
        [Tooltip("Vertical field of view, degrees.")] public float fov = 60f;
    }

    [CreateAssetMenu(menuName = "InnerFrame/Almond Studio Layout", fileName = "AS_Layout")]
    public class AS_Layout : ScriptableObject
    {
        [Tooltip("Bumped when the defaults change; AS_Setup brings an older AS_Layout.asset up to date (see AS_Setup.Migrate).")]
        public int layoutVersion;

        [Header("Room (the floor plan S10: window north, door and easel south)")]
        public float roomWidth = 3.5f;
        public float roomDepth = 4f;
        public float roomHeight = 3.2f;
        public float wallThickness = 0.35f;

        [Header("Window, centred on the north wall")]
        public float windowWidth = 1.2f;
        public float windowBottom = 0.95f;
        [Tooltip("The arch's crown; the arch is a half circle, so the casements end at Springing = windowTop - windowWidth / 2.")] public float windowTop = 3.0f;
        [Tooltip("How much wider the opening is at the room face than at the glass, each side, metres.")] public float revealSplay = 0.18f;
        [Tooltip("Where the glass sits in the wall's thickness (0 = room face, 1 = outer face).")] [Range(0.1f, 0.95f)] public float glassDepth = 0.6f;
        [Tooltip("How far the sill board reaches into the room.")] public float sillOverhang = 0.08f;
        public float casementOpenDegrees = 105f;
        public float casementOpenSeconds = 1.2f;

        [Header("The tree outside, fitted to Almond Blossom from the paint viewpoint")]
        [Tooltip("Off while the Tripo tree is being placed by hand in the scene: the generated tree and its guides are not built.")]
        public bool showGeneratedTree = false;
        [Tooltip("The eye the tree is fitted from: standing about 1 m in front of the window. From here the branches fall where the painting has them.")]
        public Vector3 paintEye = new Vector3(0f, 1.6f, 3f);
        [Tooltip("The reference plane: the painting as it would hang this far outside the wall's outer face, so that from paintEye the whole tree fits the window. Each branch then sits at its own depth along the eye's rays (AS_Branches.json).")]
        public float paintingDistance = 0.4f;
        public float paintingWidth = 1.5f;
        [Tooltip("On the line from a standing eye (1.6 m) through the window's centre.")] public float paintingCentreY = 1.95f;
        [Tooltip("Width / height of Almond Blossom (2576 x 2035).")] public float paintingAspect = 1.2658f;
        [Tooltip("No branch comes nearer the wall's outer face than this.")] public float treeClearance = 0.15f;
        [Tooltip("The dark outline round the branches, metres.")] public float inkWidth = 0.006f;
        [Tooltip("The sky's radius round the window.")] public float skyRadius = 40f;
        [Tooltip("The camera's clear colour (only seen if the sky is missing).")] public Color skyBehind = new Color(0.486f, 0.682f, 0.686f);
        public Color groundColour = new Color(0.55f, 0.6f, 0.38f);

        [Header("Following a branch with the brush's beam (degrees from the beam)")]
        [Tooltip("How close the beam must come to a branch's glowing dot to start on it.")] public float startToleranceDeg = 4f;
        [Tooltip("How far from the branch the beam may stray while following it.")] public float followToleranceDeg = 5f;
        [Tooltip("How far ahead along the branch one frame may jump (0..1 of its length).")] public float lookAhead = 0.15f;
        [Tooltip("Seconds off the branch before it is let go (the progress stays).")] public float loseSeconds = 0.6f;
        [Tooltip("Past this, the branch counts as done.")] public float finishAt = 0.95f;
        [Tooltip("How far the beam reaches when it points at no branch.")] public float beamLength = 6f;

        [Header("Comparison views (P: the view; 1, 2, 3: which; C: its picture over it; [ ]: how much)")]
        [Tooltip("Each view: a fixed camera and the picture it is compared with (Textures/Reference/<file>). Change the numbers in Play mode and watch the picture line up; they stay.")]
        public AS_View[] compareViews =
        {
            new AS_View { name = "Window in the Studio (his view)", file = "AS_WindowInTheStudio.jpg", position = new Vector3(-1.0f, 1.35f, 2.4f), yaw = 22f, pitch = -10f, fov = 62f },
            new AS_View { name = "S02 arrival (concept)", file = "AS_Ref_S02.jpg", position = new Vector3(-0.8f, 1.45f, 2.3f), yaw = 15f, pitch = -8f, fov = 60f },
            new AS_View { name = "S03 turned round (concept)", file = "AS_Ref_S03.jpg", position = new Vector3(0f, 1.6f, 3.5f), yaw = 180f, pitch = 3f, fov = 55f },
        };
        [Range(0f, 1f)] public float compareOpacity = 0.5f;

        [Header("Chairs reused from the Bedroom (Chair_Front of PaintedBedroom.prefab)")]
        [Tooltip("Turns the Bedroom chair so its seat faces the table.")] public float chairYawOffset = 0f;

        [Header("Blossoms on the Tripo tree (AS_Blossoms)")]
        [Tooltip("A blossom's petal length, metres.")] public float blossomSize = 0.07f;
        [Tooltip("Share of the buds that never open, as in the painting.")] [Range(0f, 1f)] public float blossomStayBud = 0.2f;
        [Tooltip("Seconds for one bud to open.")] public float blossomOpenSeconds = 0.6f;
        [Tooltip("Buds within this angle of the beam open.")] public float blossomBeamDegrees = 3f;
        [Tooltip("...or within this distance of the beam's line, metres.")] public float blossomBeamMetres = 0.12f;
        [Tooltip("Once this share has opened, the rest open in a wave and the act ends.")] [Range(0.1f, 1f)] public float blossomFinish = 0.75f;
        [Tooltip("How long the wave from the trunk out takes, seconds.")] public float blossomWaveSeconds = 2.5f;

        [Header("People")]
        [Tooltip("Where the player arrives (step 2 will move this to his viewpoint for Window in the Studio).")] public Vector3 playerStart = new Vector3(0.3f, 0f, 1.1f);
        public float playerStartYaw = -5f;
        public Vector3 vincentSpot = new Vector3(0.95f, 0f, 3.2f);
        public float vincentYaw = 200f;

        [Header("Light")]
        public Color ambient = new Color(0.56f, 0.5f, 0.45f);
        public Color sunColour = new Color(1f, 0.93f, 0.8f);
        public float sunIntensity = 1.1f;

        [Header("Editor fallback (no headset)")]
        public float editorGrabReach = 3f;
        public Vector3 editorHoldOffset = new Vector3(0.18f, -0.22f, 0.35f);

        [Header("Lines (ours, placeholders) and hints")]
        public string hintTitle = "Saint-Rémy, February 1890";
        public string lineArrive = "A letter from Theo. A boy, and they have named him Vincent.";
        public string linePaint = "Slowly. Let each one open.";
        public string lineDone = "Now I know what to paint for his room.";
        public string hintOpen = "Open the window";
        public string hintBrush = "Take a brush";
        public string hintFollow = "Hold the trigger and follow the branch";
        public string hintSweep = "Hold the trigger and brush over the branches";
        public string hintOthers = "Wake the other branches";

        public float Springing => windowTop - windowWidth / 2f;
        public Vector3 WindowCentreInner => new Vector3(0f, (windowBottom + windowTop) * 0.5f, roomDepth);
        public Vector3 WindowCentreOuter => new Vector3(0f, (windowBottom + windowTop) * 0.5f, roomDepth + wallThickness);
        public float PaintingHeight => paintingWidth / paintingAspect;
        public Vector3 PaintingCentre => new Vector3(0f, paintingCentreY, roomDepth + wallThickness + paintingDistance);
    }
}
