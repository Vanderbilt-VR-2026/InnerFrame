using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Every number of the Almond Blossom studio blockout in one place (Assets/AlmondStudio/AS_Layout.asset, made by
    /// Tools > InnerFrame > Almond Studio > Build). Metres and degrees. The room's floor is at y = 0, x runs east, z north;
    /// the window is centred on the north wall, whose inner face is at z = roomDepth.
    /// </summary>
    [CreateAssetMenu(menuName = "InnerFrame/Almond Studio Layout", fileName = "AS_Layout")]
    public class AS_Layout : ScriptableObject
    {
        [Header("Room (the floor plan S10: window north, door and easel south)")]
        public float roomWidth = 3.5f;
        public float roomDepth = 4f;
        public float roomHeight = 3.2f;
        public float wallThickness = 0.35f;

        [Header("Window, centred on the north wall")]
        public float windowWidth = 1.2f;
        public float windowBottom = 0.95f;
        public float windowTop = 2.75f;
        [Tooltip("How far the sill board reaches into the room.")] public float sillOverhang = 0.08f;
        public float casementOpenDegrees = 105f;
        public float casementOpenSeconds = 1.2f;

        [Header("The painting outside (Almond Blossom, bare and original)")]
        [Tooltip("From the wall's outer face to the painting.")] public float paintingDistance = 1.2f;
        public float paintingWidth = 6.2f;
        public float paintingCentreY = 2.2f;
        [Tooltip("Width / height of the painting image (2576 x 2035).")] public float paintingAspect = 1.2658f;
        [Tooltip("The plain sky colour far behind the painting, for views past its edges.")] public Color skyBehind = new Color(0.486f, 0.682f, 0.686f);

        [Header("Following a branch (painting UV units, width scaled by the aspect)")]
        [Tooltip("How close the beam must come to a branch's next point to start on it.")] public float startTolerance = 0.08f;
        [Tooltip("How far from the branch the beam may stray while following it.")] public float followTolerance = 0.09f;
        [Tooltip("How far ahead along the branch one frame may jump (0..1 of its length).")] public float lookAhead = 0.15f;
        [Tooltip("Seconds off the branch before it is let go (the progress stays).")] public float loseSeconds = 0.6f;
        [Tooltip("Past this, the branch counts as done and blooms to its tip.")] public float finishAt = 0.95f;
        [Tooltip("Softness of the bloom's edge behind the stroke (0..1 of the branch).")] public float revealSoftness = 0.06f;
        [Tooltip("Size of the reveal map built from the branch paths (the painting's height follows the aspect).")] public int revealMapWidth = 512;

        [Header("People")]
        [Tooltip("Where the player arrives (step 2 will move this to his viewpoint for Window in the Studio).")] public Vector3 playerStart = new Vector3(-0.5f, 0f, 1.3f);
        public float playerStartYaw = 12f;
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
        public string lineArrive = "A letter from Theo. A boy, and they have named him Vincent.";
        public string linePaint = "Slowly. Let each one open.";
        public string lineDone = "Now I know what to paint for his room.";
        public string hintOpen = "Open the window";
        public string hintBrush = "Take a brush";
        public string hintFollow = "Hold the trigger and follow the branch";
        public string hintOthers = "Wake the other branches";

        public Vector3 WindowCentreInner => new Vector3(0f, (windowBottom + windowTop) * 0.5f, roomDepth);
        public float PaintingHeight => paintingWidth / paintingAspect;
        public Vector3 PaintingCentre => new Vector3(0f, paintingCentreY, roomDepth + wallThickness + paintingDistance);
    }
}
