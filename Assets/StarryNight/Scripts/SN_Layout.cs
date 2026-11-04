using System;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>One star of the sky, placed by direction from SN_PlayerSeat (eye height): yaw from +Z (the valley),
    /// positive to the right; pitch above the horizon.</summary>
    [Serializable]
    public class SN_StarSpec
    {
        public string name;
        public float yaw, pitch;
        [Tooltip("Size multiplier.")] public float size = 1f;
        [Tooltip("Shown before the lantern goes down.")] public bool visibleAtStart;
        [Tooltip("0: not named; 1, 2, 3: the stars Vincent names, in order (N3 is the morning star).")] public int named;
    }

    /// <summary>
    /// Every number of The Starry Night blockout, in world units (metres, degrees, seconds), with the rig at the
    /// project's 0.8 scale (a standing eye at about 1.28 m). Starting values from the step-2 prompt (2026-11-04),
    /// to be tuned against the concept renders. SN_Setup builds meshes from some of them (the hill, the path, the
    /// cypress, the swirl mask): change those, then run Tools > InnerFrame > Starry Night > Build again.
    /// </summary>
    [CreateAssetMenu(menuName = "InnerFrame/Starry Night/Layout", fileName = "SN_Layout")]
    public class SN_Layout : ScriptableObject
    {
        [Header("Player")]
        public float rigScale = 0.8f;                 // XRRigSettings.RigScale, as in the Gallery and the Bedroom
        public float eyeHeight = 1.28f;               // a standing eye at the 0.8 rig; stars are placed from here
        public Vector3 playerStart = new Vector3(2.4f, 0f, -4.0f);   // y: above the ground
        public Vector3 playerSeat = new Vector3(0.65f, 0f, 0.1f);    // the player sits on Vincent's right
        public float playerSeatRadius = 0.6f;

        [Header("The rock, Vincent, his things (y absolute; the ground is 0 around the rock)")]
        public Vector2 rockFootprint = new Vector2(1.4f, 0.9f);      // x, z
        public float rockTop = 0.26f;                 // Vincent's Mixamo seat at his project scale (0.185 m x 1.404)
        public float rockBuried = 0.15f;              // how far it goes into the ground
        public Vector3 vincentSeat = new Vector3(-0.35f, 0.26f, 0f); // anchor for the rigged, seated Vincent (later step)
        public Vector3 vincentStand = new Vector3(-0.95f, 0f, 0f);   // the standing placeholder, beside the rock
        public float vincentScale = 1.60f * 0.8f / 1.55f;            // VanGoghSpawner.cs:32 VincentScale (0.826): 1.40 m
        public float vincentMouth = 1.55f;            // in the model's own metres (VanGoghAssetSetup.MouthHeight)
        public Vector3 lantern = new Vector3(0.05f, 0f, 0.6f);       // on the grass by his boots (y above the ground)
        public Vector3 sketchbook = new Vector3(0.15f, 0.27f, -0.15f);   // on the rock (top + 1 cm)
        public Vector3 page = new Vector3(0.45f, 0.265f, 0.3f);      // on the rock, in front of the player seat
        public Vector3 sketchbookSize = new Vector3(0.30f, 0.02f, 0.22f);
        public Vector2 pageSize = new Vector2(0.21f, 0.297f);

        [Header("Cypress")]
        public Vector3 cypressBase = new Vector3(-1.9f, 0f, 2.4f);   // y above the ground
        public float cypressHeight = 7f;
        public float cypressWidth = 1.6f;

        [Header("Hill")]
        public float hillFlatRadius = 1.2f;           // flat (y = 0) around the rock
        public float hillEdgeRadius = 5.5f;           // y = -hillDrop here; the teleport area ends here
        public float hillDrop = 1.0f;
        public float hillSlopePower = 1.3f;
        public float hillOuterRadius = 7.5f;          // then it falls away under the mist
        public float hillFall = 3.0f;
        public float skirtRadius = 10f;
        public float hillNoise = 0.06f;               // gentle bumps, none on the flat top
        public float edgeColliderRadius = 5.65f;      // low invisible walls just outside the teleport area
        public float edgeColliderHeight = 1.0f;       // taller than the CharacterController's step (0.5 x 0.8 = 0.4 m)
        public int edgeColliderCount = 32;
        public Vector2[] pathPoints =
        {
            new Vector2(2.4f, -4.0f), new Vector2(2.55f, -3.0f), new Vector2(2.05f, -2.1f),
            new Vector2(1.45f, -1.4f), new Vector2(1.25f, -0.75f), new Vector2(0.95f, -0.25f),
        };
        public float pathWidth = 0.6f;

        [Header("Far away")]
        public float hillRingRadiusFraction = 0.6f;   // x the sky radius R
        public float hillRingMinPitch = -1f, hillRingMaxPitch = 5f;  // degrees above the eye, from the hill's centre
        public float villageYaw = 6f, villagePitch = -10f, villageDistance = 60f;
        public float villageSpreadYaw = 5f, villageSpreadPitch = 1.2f;
        public int villageLights = 36;
        public float villageLightSize = 0.6f;
        public int mistCards = 14;
        public float mistRadius = 8.2f, mistWidth = 10f, mistHeight = 3.4f, mistY = -2.6f;

        [Header("Sky")]
        public float skyRadiusMax = 150f;
        public float skyRadiusFarClipFraction = 0.8f; // R = min(skyRadiusMax, this x the camera's far clip)
        public float starDistanceFraction = 0.9f;     // stars sit this far out, x R
        public float panelYaw = 80f;                  // the painting covers yaw -panelYaw..+panelYaw
        public float panelPitchMin = -5f, panelPitchMax = 70f;
        public float panelEdgeSoft = 8f;              // degrees of soft edge into the night gradient
        public float starCoreDeg = 1.3f, starHaloDeg = 4.5f;
        public float moonYaw = 48f, moonPitch = 48f, moonDeg = 7f;
        public float morningStarRightOfCypress = 15f; // N3: this far right of the cypress's centre line, seen from the seat
        public float morningStarPitch = 32f;
        public SN_StarSpec[] stars =
        {
            new SN_StarSpec { name = "N1", yaw = 22f, pitch = 16f, named = 1, size = 1.1f },
            new SN_StarSpec { name = "N2", yaw = 6f, pitch = 24f, named = 2, size = 1.1f },
            new SN_StarSpec { name = "N3", yaw = -33f, pitch = 32f, named = 3, size = 1.8f, visibleAtStart = true },
            new SN_StarSpec { name = "S1", yaw = -60f, pitch = 22f },
            new SN_StarSpec { name = "S2", yaw = -52f, pitch = 58f, visibleAtStart = true, size = 1.2f },
            new SN_StarSpec { name = "S3", yaw = -36f, pitch = 13f, size = 0.8f },
            new SN_StarSpec { name = "S4", yaw = -18f, pitch = 55f },
            new SN_StarSpec { name = "S5", yaw = -4f, pitch = 64f, size = 0.8f },
            new SN_StarSpec { name = "S6", yaw = 15f, pitch = 59f },
            new SN_StarSpec { name = "S7", yaw = 33f, pitch = 30f, size = 0.9f },
            new SN_StarSpec { name = "S8", yaw = 62f, pitch = 24f, visibleAtStart = true, size = 1.2f },
            new SN_StarSpec { name = "S9", yaw = 70f, pitch = 60f, size = 0.9f },
        };

        [Header("Wind (the guide path; data the art pass replaces)")]
        [Tooltip("24 directions (yaw, pitch), in the order the wind is traced: over the great swirl of the sky.")]
        public Vector2[] guidePath =
        {
            new Vector2(-25f, 38f), new Vector2(-21f, 42f), new Vector2(-16f, 45f), new Vector2(-10f, 47f),
            new Vector2(-4f, 47.5f), new Vector2(2f, 47f), new Vector2(8f, 45.5f), new Vector2(14f, 43f),
            new Vector2(19f, 39.5f), new Vector2(23f, 35f), new Vector2(25f, 30f), new Vector2(24f, 25f),
            new Vector2(20f, 21f), new Vector2(14f, 19f), new Vector2(7f, 19.5f), new Vector2(1f, 22f),
            new Vector2(-3f, 26f), new Vector2(-4f, 31f), new Vector2(-2f, 35.5f), new Vector2(2f, 38f),
            new Vector2(7f, 37.5f), new Vector2(10f, 34.5f), new Vector2(9.5f, 30.5f), new Vector2(6f, 29f),
        };
        public float swirlBandHalfWidth = 5f;         // degrees either side of the path: the band the swirl paints in
        public int swirlMaskWidth = 512, swirlMaskHeight = 256;
        public int guideDotsPerSegment = 3;
        public float guideDotDeg = 0.7f;

        [Header("Gameplay")]
        public float arriveDistance = 3f;             // from Vincent, horizontal
        public float aimAssistDeg = 4f;               // a star wakes if the aim is within this
        public float windToleranceDeg = 6f;           // the next path point counts within this
        public float lanternDimSeconds = 1.5f;
        public float lanternDimTo = 0.08f;            // fraction of the light and the flame left
        public float starsFadeInSeconds = 2.5f;
        public float starBloomSeconds = 0.8f;
        public float paintToHalfSeconds = 3f;
        public float paintToFullSeconds = 4f;
        public float dawnDelaySeconds = 10f;
        public float dawnSeconds = 20f;
        public float morningStarFadeSeconds = 8f;
        public float idleRepeatSeconds = 25f;
        public float sketchMaxDistance = 0.6f, sketchMaxViewAngle = 25f, sketchValleyYaw = 60f, sketchHoldSeconds = 1.5f;
        public float editorGrabReach = 1.5f;
        public float beamLength = 45f;

        [Header("Light")]
        public Color ambient = new Color32(0x14, 0x20, 0x3E, 0xFF);
        public Color moonLight = new Color32(0x8F, 0xA6, 0xD9, 0xFF);
        public float moonLightIntensity = 0.35f;
        public Color lanternLight = new Color32(0xFF, 0xB8, 0x5C, 0xFF);
        public float lanternIntensity = 2.2f, lanternRange = 4.5f;
        public Color fog = new Color32(0x16, 0x26, 0x4C, 0xFF);
        public float fogStart = 25f, fogEnd = 220f;
        [Tooltip("At full dawn: the ambient light, the fog, and the far hills' colour.")]
        public Color dawnAmbient = new Color32(0x5A, 0x66, 0x82, 0xFF);
        public Color dawnFog = new Color32(0x9A, 0xA6, 0xB8, 0xFF);
        public Color hillRingNight = new Color32(0x0E, 0x1F, 0x45, 0xFF);
        public Color hillRingDawn = new Color32(0x40, 0x4E, 0x72, 0xFF);

        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A direction from yaw (from +Z, right positive) and pitch (up positive), degrees.</summary>
        public static Vector3 Dir(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

        /// <summary>Yaw (from +Z, right positive) and pitch of a direction, degrees.</summary>
        public static Vector2 YawPitch(Vector3 d)
        {
            d.Normalize();
            return new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg);
        }

        public Vector3 SeatEye => new Vector3(playerSeat.x, eyeHeight, playerSeat.z);

        /// <summary>The sky's radius for a camera with this far clip.</summary>
        public float SkyRadius(float farClip) => Mathf.Min(skyRadiusMax, skyRadiusFarClipFraction * farClip);

        /// <summary>The hill's height at (x, z): flat round the rock, sloping to -hillDrop at hillEdgeRadius, then away.</summary>
        public float GroundHeight(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float h;
            if (r <= hillFlatRadius) h = 0f;
            else if (r <= hillEdgeRadius) h = -hillDrop * Mathf.Pow((r - hillFlatRadius) / (hillEdgeRadius - hillFlatRadius), hillSlopePower);
            else if (r <= hillOuterRadius)
            {
                float t = (r - hillEdgeRadius) / (hillOuterRadius - hillEdgeRadius);
                h = -hillDrop - hillFall * t * t;
            }
            else h = -hillDrop - hillFall - 2.5f * (r - hillOuterRadius);
            float bumps = hillNoise * Mathf.Sin(1.3f * x + 0.7f * z) * Mathf.Sin(0.9f * z - 1.1f * x);
            return h + bumps * Mathf.SmoothStep(0f, 1f, (r - hillFlatRadius) / 1.0f);
        }

        /// <summary>A point given as (x, height above the ground, z), on the hill.</summary>
        public Vector3 OnGround(Vector3 p) => new Vector3(p.x, GroundHeight(p.x, p.z) + p.y, p.z);

        /// <summary>The player start's yaw: facing the rock.</summary>
        public float PlayerStartYaw => Mathf.Atan2(-playerStart.x, -playerStart.z) * Mathf.Rad2Deg;

        /// <summary>The morning star's direction: morningStarRightOfCypress right of the cypress's centre line, seen from the seat eye.</summary>
        public Vector2 MorningStarYawPitch()
        {
            Vector3 toCypress = cypressBase - SeatEye;
            float cypressYaw = Mathf.Atan2(toCypress.x, toCypress.z) * Mathf.Rad2Deg;
            return new Vector2(cypressYaw + morningStarRightOfCypress, morningStarPitch);
        }

        /// <summary>A star's direction from the seat eye (N3 follows the cypress).</summary>
        public Vector2 StarYawPitch(SN_StarSpec s) => s.named == 3 ? MorningStarYawPitch() : new Vector2(s.yaw, s.pitch);
    }
}
