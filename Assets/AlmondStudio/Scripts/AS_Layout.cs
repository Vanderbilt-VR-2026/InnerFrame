using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Every number of the Almond Blossom studio blockout in one place (Assets/AlmondStudio/AS_Layout.asset, made by
    /// Tools > InnerFrame > Almond Studio > Build). Metres and degrees. The room's floor is at y = 0, x runs east, z north;
    /// the window is centred on the north wall, whose inner face is at z = roomDepth.
    /// </summary>
    /// <summary>One of the pictures pinned on the north wall, cut from Window in the Studio itself: where it hangs, how
    /// tall, and its four corners in the painting (fractions of the picture's width and height, from its top-left).</summary>
    [System.Serializable]
    public class AS_WallPicture
    {
        public string name;
        [Tooltip("Its middle on the north wall: x across, y up (metres).")] public Vector2 centre;
        [Tooltip("Metres; the width follows the corners' shape in the painting.")] public float height = 0.6f;
        public Vector2 topLeft, topRight, bottomRight, bottomLeft;
    }

    /// <summary>A canvas or print on the east or west wall: a picture from Textures/Canvases on a stretched canvas with a
    /// thin frame; its width follows the picture's shape. Blank if the picture isn't there yet.</summary>
    [System.Serializable]
    public class AS_SideCanvas
    {
        public string name;
        [Tooltip("File name in Textures/Canvases, without extension (.jpg or .png).")] public string file;
        [Tooltip("+1 = east wall, -1 = west wall.")] public int wall = -1;
        [Tooltip("Its middle: z along the wall (0 = south, 4 = north) and y up, metres.")] public Vector2 centre;
        [Tooltip("Metres.")] public float height = 0.6f;
        [Tooltip("Width / height when there is no picture yet.")] public float blankAspect = 1.25f;
    }

    [System.Serializable]
    public class AS_View
    {
        public string name;
        [Tooltip("The picture's file name in Textures/Reference.")] public string file;
        public Vector3 position;
        [Tooltip("Degrees round from north (+z) toward east (+x).")] public float yaw;
        [Tooltip("Degrees; negative looks up.")] public float pitch;
        [Tooltip("Degrees round the line of sight; positive tips the picture anticlockwise.")] public float roll;
        [Tooltip("Vertical field of view, degrees.")] public float fov = 60f;
    }

    [CreateAssetMenu(menuName = "InnerFrame/Almond Studio Layout", fileName = "AS_Layout")]
    public class AS_Layout : ScriptableObject
    {
        [Tooltip("Bumped when the defaults change; AS_Setup brings an older AS_Layout.asset up to date (see AS_Setup.Migrate).")]
        public int layoutVersion;

        [Header("Room (the floor plan S10: window north, door and easel south)")]
        [Tooltip("4.6 m: the concept S03 shows a wide, low room (its back wall is about twice as wide as high, more than a real door allows); this is between the two.")]
        public float roomWidth = 4.6f;
        public float roomDepth = 4f;
        public float roomHeight = 3.2f;
        public float wallThickness = 0.35f;

        [Header("Window, centred on the north wall")]
        [Tooltip("At the glass. 1.0 x 0.9..3.03 is solved from the concept S02 (the window is half again as tall as wide below the arch).")]
        public float windowWidth = 1.0f;
        public float windowBottom = 0.9f;
        [Tooltip("The arch's crown; the arch is a half circle, so the casements end at Springing = windowTop - windowWidth / 2.")] public float windowTop = 3.03f;
        [Tooltip("How much wider the opening is at the room face than at the glass, on the left (the pale blue jamb; S02 shows almost none of it).")] public float revealSplayLeft = 0.04f;
        [Tooltip("The same on the right (the green jamb, wide in S02 and in the painting).")] public float revealSplayRight = 0.23f;
        [Tooltip("Where the glass sits in the wall's thickness (0 = room face, 1 = outer face).")] [Range(0.1f, 0.95f)] public float glassDepth = 0.6f;
        [Tooltip("How far the sill board reaches into the room.")] public float sillOverhang = 0.08f;
        public float casementOpenDegrees = 105f;
        public float casementOpenSeconds = 1.2f;

        [Header("Ceiling beams, across the room (S03)")]
        public int beamCount = 3;
        public float beamDepth = 0.2f;
        public float beamWidth = 0.22f;

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
            // S02 and S03 are solved from the concept images (glass corners, transom, sill; the back wall's corners), not nudged by eye
            new AS_View { name = "S02 arrival (concept)", file = "AS_Ref_S02.jpg", position = new Vector3(-0.31f, 1.43f, 2.52f), yaw = 15.7f, pitch = -4.9f, roll = -3.3f, fov = 80f },
            new AS_View { name = "S03 turned round (concept)", file = "AS_Ref_S03.jpg", position = new Vector3(0f, 1.15f, 3.6f), yaw = 180.3f, pitch = -2.4f, fov = 66f },
        };
        [Range(0f, 1f)] public float compareOpacity = 0.5f;

        [Header("Chairs reused from the Bedroom (Chair_Front of PaintedBedroom.prefab)")]
        [Tooltip("Turns the Bedroom chair so its seat faces the table.")] public float chairYawOffset = 0f;

        [Header("Easel (Vincent's, VG_Easel.fbx), door, paint box")]
        [Tooltip("Turns the easel; 0 = its ledge toward the room (north).")] public float easelYaw = 0f;
        [Tooltip("Shows the easel's blank canvas (VG_Canvas). Off: Act 4 has no canvas to paint.")] public bool easelCanvas = false;
        [Tooltip("The door's panels: columns x rows.")] public Vector2Int doorPanels = new Vector2Int(2, 3);
        [Tooltip("Where the paint box stands on the table (x, z), and its turn.")] public Vector2 paintBoxSpot = new Vector2(-0.3f, 1.9f);
        public float paintBoxYaw = 12f;
        [Tooltip("Where the pot of brushes stands on the table (x, z).")] public Vector2 brushPotSpot = new Vector2(0.38f, 1.65f);

        [Header("Sill still life (Tripo model Art/Props/AS_SillStillLife.fbx)")]
        [Tooltip("Turns the model so it stands up with its front toward the room (-z). Tripo FBX often needs X 270 (as the tree did) and/or Y 180.")]
        public Vector3 sillModelEuler = new Vector3(0f, 180f, 0f);
        [Tooltip("The tallest object (the lidded jar), metres: sets the model's scale.")] public float sillModelHeight = 0.32f;
        [Tooltip("Moves the model from the sill's middle (x across, y up, z toward the glass).")] public Vector3 sillModelOffset = new Vector3(-0.03f, 0f, 0.01f);
        [Tooltip("Cut the model into its objects and place each as in the painting (off: the row as generated).")] public bool sillSplit = true;
        [Tooltip("A gap across x wider than this (metres, after scaling) separates two objects.")] public float sillSplitGap = 0.008f;
        [Tooltip("Each object's spot, left to right as seen from the room: x across (0 = the glass's middle), y = how far from the room face toward the glass (the sill runs from -sillOverhang to about 0.21). After Window in the Studio: the jar alone at the front left; the others grouped right, staggered.")]
        public Vector2[] sillItemSpots = { new Vector2(-0.36f, 0.01f), new Vector2(-0.02f, 0.08f), new Vector2(0.12f, 0.14f), new Vector2(0.25f, 0.05f), new Vector2(0.37f, 0.14f) };
        [Tooltip("Each object's turn, degrees.")] public float[] sillItemYaw = { 0f, 20f, -10f, 15f, 0f };
        [Tooltip("The brush stands in this sill object (0-based, left to right; 3 = the glass). -1: it lies on the sill. Only when the model is cut.")]
        public int brushStandsIn = 3;

        [Header("East and west walls: his Saint-Rémy works and a Japanese print (the map S10 and S03)")]
        public AS_SideCanvas[] sideCanvases =
        {
            new AS_SideCanvas { name = "The Large Plane Trees (Cleveland 1947.209, CC0)", file = "AS_Canvas_PlaneTrees", wall = -1, centre = new Vector2(3.0f, 1.75f), height = 0.62f, blankAspect = 1.25f },
            new AS_SideCanvas { name = "Keisai Eisen, Courtesan with a Letter (RISD, CC0)", file = "AS_Canvas_Print", wall = -1, centre = new Vector2(2.2f, 1.6f), height = 0.62f, blankAspect = 0.34f },
            new AS_SideCanvas { name = "Wheat Field with Cypresses (Met, Open Access)", file = "AS_Canvas_WheatField", wall = -1, centre = new Vector2(1.2f, 1.55f), height = 0.6f, blankAspect = 1.26f },
            new AS_SideCanvas { name = "Cypresses (Met, Open Access)", file = "AS_Canvas_Cypresses", wall = 1, centre = new Vector2(2.35f, 1.75f), height = 0.75f, blankAspect = 0.79f },
            new AS_SideCanvas { name = "First Steps, after Millet (Met 64.165.2, Open Access)", file = "AS_Canvas_FirstSteps", wall = 1, centre = new Vector2(1.2f, 1.6f), height = 0.58f, blankAspect = 1.26f },
        };

        [Header("Theo's letter and the birth card on the table (Textures/Table)")]
        [Tooltip("The letter's middle on the table (x, z), and its turn.")] public Vector2 letterSpot = new Vector2(0.1f, 1.95f);
        public float letterYaw = -8f;
        [Tooltip("The birth card's middle on the table (x, z), and its turn.")] public Vector2 cardSpot = new Vector2(0.36f, 2.0f);
        public float cardYaw = 12f;

        [Header("The pictures on the north wall, cut from Window in the Studio (Textures/Reference/AS_WindowInTheStudio.jpg)")]
        public AS_WallPicture[] wallPictures =
        {
            // corners measured on the 614 x 800 copy, 11 Nov; the hanging spots are the S02 ones
            new AS_WallPicture { name = "Left, upper (reddish study)", centre = new Vector2(-1.0f, 1.9f), height = 0.67f,
                topLeft = new Vector2(0.003f, 0.240f), topRight = new Vector2(0.176f, 0.250f), bottomRight = new Vector2(0.173f, 0.473f), bottomLeft = new Vector2(0.003f, 0.465f) },
            new AS_WallPicture { name = "Left, lower (green study)", centre = new Vector2(-0.95f, 1.12f), height = 0.56f,
                topLeft = new Vector2(0.003f, 0.525f), topRight = new Vector2(0.137f, 0.531f), bottomRight = new Vector2(0.137f, 0.738f), bottomLeft = new Vector2(0.003f, 0.731f) },
            new AS_WallPicture { name = "Right, upper (framed drawing)", centre = new Vector2(1.52f, 2.08f), height = 0.64f,
                topLeft = new Vector2(0.834f, 0.268f), topRight = new Vector2(0.961f, 0.265f), bottomRight = new Vector2(0.967f, 0.416f), bottomLeft = new Vector2(0.840f, 0.414f) },
            new AS_WallPicture { name = "Right, lower (drawing of a figure)", centre = new Vector2(1.27f, 1.19f), height = 0.76f,
                topLeft = new Vector2(0.754f, 0.475f), topRight = new Vector2(0.974f, 0.466f), bottomRight = new Vector2(0.974f, 0.631f), bottomLeft = new Vector2(0.765f, 0.638f) },
        };

        [Header("Window frame (after the painting)")]
        [Tooltip("The broad frame round the opening at the glass, and the fanlight's arc.")] public float windowFrameWidth = 0.065f;
        [Tooltip("The transom on the springing line.")] public float windowTransom = 0.075f;
        [Tooltip("The casements' stiles and rails.")] public float windowStile = 0.04f;
        [Tooltip("The glazing bars between the small panes.")] public float windowBar = 0.022f;
        [Tooltip("The half-round hub the fanlight's bars radiate from.")] public float windowHub = 0.075f;
        [Tooltip("Use the painted window (Textures/Window: AS_CasementL.png, AS_CasementR.png, AS_Fanlight.png, panes cut out by alpha) when the three pictures are there; else the built one.")]
        public bool paintedWindow = true;
        [Tooltip("Use the Tripo casement (Art/Props/AS_Casement.fbx, the left leaf; the right one is its mirror) when it is there.")]
        public bool casementFromModel = true;
        [Tooltip("Turns the Tripo casement upright with its front toward the room, as for the other Tripo props.")] public Vector3 casementModelEuler = new Vector3(270f, 180f, 0f);
        [Tooltip("The Tripo casement's thickness after fitting, metres.")] public float casementModelDepth = 0.05f;

        [Header("Chest with its boxes (Tripo model Art/Props/AS_ChestBoxes.fbx)")]
        [Tooltip("Turns the model upright with its front toward the room, as for the sill still life.")] public Vector3 chestModelEuler = new Vector3(270f, 180f, 0f);
        [Tooltip("Floor to the top box, metres: sets the model's scale.")] public float chestModelHeight = 0.85f;
        [Tooltip("Where it stands (x, z): the north-east corner, under the right-hand pictures, as at the painting's lower right.")] public Vector2 chestSpot = new Vector2(1.8f, 3.68f);

        [Header("Blossoms on the Tripo tree (AS_Blossoms)")]
        [Tooltip("A blossom's petal length, metres.")] public float blossomSize = 0.07f;
        [Tooltip("Share of the buds that never open, as in the painting.")] [Range(0f, 1f)] public float blossomStayBud = 0.2f;
        [Tooltip("Seconds for one bud to open.")] public float blossomOpenSeconds = 0.6f;
        [Tooltip("Buds within this angle of the beam open.")] public float blossomBeamDegrees = 3f;
        [Tooltip("...or within this distance of the beam's line, metres.")] public float blossomBeamMetres = 0.12f;
        [Tooltip("Once this share has opened, the rest open in a wave and the act ends.")] [Range(0.1f, 1f)] public float blossomFinish = 0.75f;
        [Tooltip("How long the wave from the trunk out takes, seconds.")] public float blossomWaveSeconds = 2.5f;

        [Header("People")]
        [Tooltip("Where the player arrives: the S02 viewpoint, a little left of the window, looking at it.")] public Vector3 playerStart = new Vector3(-0.31f, 0f, 2.52f);
        public float playerStartYaw = 15.7f;
        public Vector3 vincentSpot = new Vector3(0.95f, 0f, 3.2f);
        public float vincentYaw = 200f;

        [Header("Light")]
        public Color ambient = new Color(0.56f, 0.5f, 0.45f);
        public Color sunColour = new Color(1f, 0.93f, 0.8f);
        public float sunIntensity = 1.1f;

        [Header("Flat painted shading (as in Window in the Studio)")]
        [Tooltip("On: the room's surfaces use AS_Painted (unlit, one shade per face, no shadows); the sun lights only the Tripo props, softly. Off: the old URP Lit look. Run Build after changing it.")]
        public bool flatShading = true;
        [Tooltip("Toward the light: faces turned this way are the full colour. Default: from above and from the window (north).")]
        public Vector3 shadeDirection = new Vector3(0.25f, 0.8f, 0.55f);
        [Tooltip("What a face turned right away from the light keeps (1 = no shade at all).")] [Range(0.4f, 1f)] public float shadeMin = 0.78f;
        [Tooltip("With flat shading: the ambient light on the Tripo chairs and easel (URP Lit), bright so they match the room.")]
        public Color flatAmbient = new Color(0.82f, 0.78f, 0.72f);
        [Tooltip("With flat shading: the sun on the Tripo chairs and easel, without shadows.")] public float flatSunIntensity = 0.45f;

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
