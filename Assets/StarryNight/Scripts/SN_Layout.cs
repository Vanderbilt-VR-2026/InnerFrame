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
        [Tooltip("Size multiplier of the core.")] public float size = 1f;
        [Tooltip("The woken halo's visible diameter in degrees; 0: SN_Layout.starHaloDeg.")] public float haloDeg;
        [Tooltip("Shown before the lantern goes down.")] public bool visibleAtStart;
        [Tooltip("0: not named; 1, 2, 3: the stars Vincent names, in order (N3 is the morning star).")] public int named;
    }

    /// <summary>
    /// Every number of The Starry Night, in world units (metres, degrees, seconds), with the rig at the project's 0.8
    /// scale (a standing eye at about 1.28 m). Blockout values from the step-2 prompt; the art pass (step 4,
    /// 2026-11-04) moved the seat and re-aimed the sky from it (version 2); the shared dialogue box (version 3) took
    /// over uiBelowGaze (now VG_Dialogue_StarryNight.belowGaze); the look pass (version 4) added the Tripo bushes and
    /// clouds, a fuller mist, warmer light, one far horizon line and the colour grading switch. SN_Setup brings an older
    /// asset up once, step by step, and logs every value it changes. SN_Setup builds meshes from some of them (the hill, the path, the far hills, the
    /// horizon, the mist, the flowers, the swirl mask): change those, then run Tools > InnerFrame > Starry Night > Build again.
    /// </summary>
    [CreateAssetMenu(menuName = "InnerFrame/Starry Night/Layout", fileName = "SN_Layout")]
    public class SN_Layout : ScriptableObject
    {
        /// <summary>2: the art pass's values; 3: the dialogue box moved to VG_DialogueUI (uiBelowGaze went to its
        /// settings); 4: the look pass. An asset below it is brought up to it once by SN_Setup.</summary>
        public const int CurrentVersion = 4;

        [Tooltip("0: the blockout's values. SN_Setup brings an older asset up to CurrentVersion once, step by step.")]
        public int version;

        [Header("Player")]
        public float rigScale = 0.8f;                 // XRRigSettings.RigScale, as in the Gallery and the Bedroom
        public float eyeHeight = 1.28f;               // a standing eye at the 0.8 rig; stars are placed from here
        public Vector3 playerStart = new Vector3(2.4f, 0f, -4.0f);   // y: above the ground
        public Vector3 playerSeat = new Vector3(0.45f, 0f, -0.9f);   // Vincent's seat 42 degrees to the left, 1.2 m (concept G2)
        public float playerSeatRadius = 0.6f;

        [Header("The rock, Vincent, his things (y absolute; the ground is 0 around the rock)")]
        [Tooltip("SN_Rock.fbx's footprint (x, z), as built by Art/Source/StarryNight/make_tripo.py; keeps the flowers off it.")]
        public Vector2 rockFootprint = new Vector2(1.456f, 0.816f);
        [Tooltip("The area-weighted mean height of the rock's upward-facing top (make_tripo.py scales it to this).")]
        public float rockTop = 0.26f;                 // Vincent's Mixamo seat at his project scale (0.185 m x 1.404)
        [Tooltip("How far the rock model is sunk into the ground (its lowest point is at 0 in the model).")]
        public float rockBuried = 0f;
        public Vector3 vincentSeat = new Vector3(-0.35f, 0.26f, 0f); // anchor for the rigged, seated Vincent (later step)
        public Vector3 vincentStand = new Vector3(-0.80f, 0f, 0.50f); // the standing placeholder, past the rock's left end
        public float vincentScale = 1.60f * 0.8f / 1.55f;            // VanGoghSpawner.cs:32 VincentScale (0.826): 1.40 m
        public float vincentMouth = 1.55f;            // in the model's own metres (VanGoghAssetSetup.MouthHeight)
        public Vector3 lantern = new Vector3(0.85f, 0f, -0.55f);     // on the grass by the seat, valley side (y above the ground)
        // on the rock (each dropped from about 1 cm above it, so it settles): the nearest spots to the blockout's where
        // the whole footprint is over the Tripo rock and the rock under it varies by 2.5 cm or less (the page: 4 cm)
        // (the look pass: the page moved within reach of the seat, 0.73 m; no spot that flat within 0.75 m for the
        // sketchbook at its yaw, so it stays)
        public Vector3 sketchbook = new Vector3(-0.07f, 0.285f, -0.09f);
        public Vector3 page = new Vector3(0.22f, 0.296f, -0.21f);
        public Vector3 charcoal = new Vector3(-0.10f, 0.288f, 0.12f); // by Vincent's seat
        public float charcoalYaw = 70f;
        public Vector3 sketchbookSize = new Vector3(0.30f, 0.02f, 0.22f);
        public Vector2 pageSize = new Vector2(0.21f, 0.297f);

        [Header("Cypress")]
        public Vector3 cypressBase = new Vector3(-1.9f, 0f, 2.4f);   // y above the ground
        public float cypressHeight = 7f;              // SN_Cypress.fbx is scaled to these (make_tripo.py); the collider uses them
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
            new Vector2(2.4f, -4.0f), new Vector2(2.55f, -3.0f), new Vector2(2.1f, -2.15f),
            new Vector2(1.5f, -1.55f), new Vector2(0.95f, -1.15f), new Vector2(0.45f, -0.9f),
        };
        public float pathWidth = 0.6f;
        [Tooltip("The grass texture repeats every this many metres; the path's dirt every pathTileMetres along it.")]
        public float grassTileMetres = 1.6f, pathTileMetres = 2.4f;

        [Header("Bushes (the look pass: Tripo, each kind one combined mesh, SN_Scatter; placements by SN_Setup)")]
        public int bushSeed = 1888;
        public int bushACount = 50, bushBCount = 25;
        public Vector2 bushScale = new Vector2(0.7f, 1.3f);
        [Tooltip("A bush's footprint stays this far from: the path's centre line, the seat, the rock's footprint, the lantern, Vincent's spot, the start, the cypress's trunk.")]
        public float bushClearPath = 0.4f, bushClearSeat = 0.6f, bushClearRock = 0.2f, bushClearLantern = 0.3f, bushClearVincent = 0.45f, bushClearStart = 0.8f, bushClearCypress = 0.3f;
        [Tooltip("A bush's footprint doesn't straddle the teleport walls' line (edgeColliderRadius) by less than this.")]
        public float bushClearWalls = 0.05f;
        [Tooltip("Within this of the seat (a footprint's nearest edge) nothing is taller than openViewHeight: the view to the valley stays open.")]
        public float openViewRadius = 1.5f, openViewHeight = 0.6f;
        [Tooltip("Bushes out to this radius: past the walls onto the rim's slope, where the mist begins.")]
        public float bushAreaRadius = 6.6f;
        [Tooltip("How much a bush leans with the slope under it (0: upright, 1: square to the ground).")]
        public float bushSlopeFollow = 0.6f;
        [Tooltip("SN_Scatter.normalBend: the leaves' normals bent toward up (lit from above, softer).")]
        public float bushNormalBend = 0.5f;
        [Tooltip("The materials' tints (URP Lit, over the Tripo textures; above 1 brightens).")]
        public Color bushATint = new Color(1.0f, 1.1f, 0.78f, 1f), bushBTint = new Color(1.05f, 1.12f, 0.85f, 1f);

        [Header("Ground tints (the look pass: lighter and more saturated through the materials, not the textures)")]
        public Color grassTint = new Color(1.32f, 1.24f, 0.6f, 1f);
        public Color pathTint = new Color(1.18f, 1.02f, 0.8f, 1f);

        [Header("Flowers (one mesh of crossed cards, alpha-clipped)")]
        public int flowerClusters = 80;
        public int flowerSeed = 1890;
        public Vector2 flowerWidth = new Vector2(0.30f, 0.46f);
        public float flowerAreaRadius = 5.2f;

        [Header("Far away")]
        public float hillRingRadiusFraction = 0.6f;   // x the sky radius R
        [Tooltip("The far hills' paint (SN_FarHills.png) runs (max - min) degrees down from the ridge; behind the painted horizon the ridge sinks by (max - under), out of sight. The ridge's own height: ridgeMinPitch..ridgeMaxPitch.")]
        public float hillRingMinPitch = -6f, hillRingMaxPitch = 4f;
        public float hillRingUnderHorizonPitch = -9f;
        [Tooltip("The look pass: the far hills' ridge, one generated, non-repeating silhouette round the horizon (pitch from the hill's centre at eye height): between these, from this seed; within the painted strip's span it sinks out of sight; where they meet it stays ridgeJunctionMargin under the strip's own ridge (horizon_ridge.json).")]
        public float ridgeMinPitch = -1.2f, ridgeMaxPitch = 2.4f;
        public int ridgeSeed = 1853;
        public float ridgeJunctionMargin = 0.6f, ridgeJunctionDeg = 12f;
        [Tooltip("The ridge's paint (SN_FarHills.png, seamless across) repeats about every this many degrees (its u is warped a little).")]
        public float ridgeTileDeg = 90f;
        [Tooltip("The painted horizon (horizon.png's band): this wide, its top this high, seen from the seat, at the far hills' radius less horizonInset.")]
        public float horizonWidthDeg = 96f, horizonTopPitch = 4f, horizonInset = 2.5f;
        [Tooltip("The band's width / height (Art/Source/StarryNight/horizon_glow.json, band_aspect).")]
        public float horizonAspect = 5.585f;
        [Tooltip("The spire's tip sits at this yaw: the village's direction, recomputed from the new seat (it was yaw 6, pitch -10 from the blockout's seat).")]
        public float horizonSpireYaw = 6.1f;
        public float horizonSpireU = 0.6807f;         // in the band (horizon_glow.json)
        public float horizonEndFadeDeg = 8f;
        public Color horizonNight = new Color(0.62f, 0.66f, 0.80f, 1f), horizonDawn = new Color(0.92f, 0.92f, 0.96f, 1f);
        [Tooltip("The lit windows' extra glow (SN_HorizonGlow.png) at night; it fades with dawn.")]
        public Color horizonGlowColour = new Color(1f, 0.72f, 0.32f, 1f);
        public float horizonGlow = 0.9f;
        [Tooltip("A few soft glow sprites over the brightest windows: (u, v from the top) in the band, and a strength.")]
        public Vector3[] villageGlowSprites = { new Vector3(0.6813f, 0.6091f, 0.522f), new Vector3(0.7256f, 0.8236f, 0.243f), new Vector3(0.5889f, 0.7836f, 0.2f) };
        public float villageGlowSpriteDeg = 4.5f;
        [Tooltip("The blockout's lit-window dots, now over the painted village's brightest windows. Off by default.")]
        public bool villageDots;
        public float villageYaw = 6.15f, villagePitch = -6.4f, villageDistance = 85f;
        public float villageSpreadYaw = 5f, villageSpreadPitch = 1.2f;
        public int villageLights = 36;
        public float villageLightSize = 0.6f;

        [Header("Mist (a few drifting, near-horizontal noise layers and a handful of puffs)")]
        public float[] mistLayerHeights = { -1.9f, -2.5f, -3.3f, -4.3f };
        [Tooltip("Each layer's size against the shared annulus (inner and outer radius): the upper ones end nearer, so from the seat they stay under the village.")]
        public float[] mistLayerScale = { 0.62f, 0.85f, 1f, 1.15f };
        [Tooltip("The layers stop short of the painted horizon: seen from the seat their far edge is below its village (about -10 degrees).")]
        public float mistInnerRadius = 5.6f, mistOuterRadius = 22f;
        [Tooltip("Each layer rises this much per metre outward.")]
        public float mistRise = 0f;
        public float mistTileMetres = 12f;
        public Vector2[] mistDrift = { new Vector2(0.0035f, -0.0015f), new Vector2(0.0040f, 0.0012f), new Vector2(-0.0028f, 0.0020f), new Vector2(0.0018f, -0.0026f) };
        public Color mistColour = new Color(0.54f, 0.61f, 0.88f, 0.39f);
        public int mistPuffs = 6;
        public float mistPuffRadius = 8.6f, mistPuffY = -2.2f, mistPuffSpreadYaw = 140f;
        public Vector2 mistPuffSize = new Vector2(6.5f, 2.6f);

        [Header("Clouds (the look pass: the Tripo cloud, one combined mesh, a bank round the rim)")]
        public int cloudSeed = 1853, cloudCount = 25;
        public Vector2 cloudScale = new Vector2(0.8f, 2.5f);
        [Tooltip("Cloud centres between these radii from the hill's centre.")]
        public float cloudInnerRadius = 6.3f, cloudOuterRadius = 11f;
        [Tooltip("No part of a cloud comes nearer the hill's centre than this (pushed outward until it doesn't).")]
        public float cloudClearRadius = 6f;
        [Tooltip("Seen from the seat eye, every cloud's top stays under this pitch (under the painted horizon's own mist); and under the hilltop.")]
        public float cloudTopMaxPitch = -12.5f, cloudTopMaxY = -0.6f;
        [Tooltip("Clusters round the ring, so the clouds read as one bank; each cloud's broad side turns to the hill, +- this.")]
        public int cloudClusters = 5;
        public float cloudFaceJitterDeg = 35f;
        public Color cloudTint = new Color(1.1f, 1.12f, 1.22f, 1f);
        [Tooltip("SN_Scatter.normalBend for the clouds: lit softly from above, so no side reads heavy against the mist.")]
        public float cloudNormalBend = 1.6f;

        [Header("Sky")]
        public float skyRadiusMax = 150f;
        public float skyRadiusFarClipFraction = 0.8f; // R = min(skyRadiusMax, this x the camera's far clip)
        public float starDistanceFraction = 0.9f;     // stars sit this far out, x R
        [Tooltip("The painting (sky-painted.png) covers yaw panelYawCentre +- panelYaw and pitch panelPitchMin..Max, seen from the seat.")]
        public float panelYawCentre = 28f;
        public float panelYaw = 56.25f;               // half the yaw span (112.5 degrees: 1.5 x the pitch span)
        public float panelPitchMin = -8f, panelPitchMax = 67f;
        public float panelEdgeSoft = 8f;              // degrees of soft edge into the night
        public float starCoreDeg = 1.3f;
        [Tooltip("A woken star's halo, its visible diameter in degrees (the painted halo sprite fills SN_Star.HaloFill of its quad).")]
        public float starHaloDeg = 3.5f;
        public float moonYaw = 55f, moonPitch = 49.5f;
        [Tooltip("The painted moon's quad (the crescent is about 0.3 of it).")]
        public float moonDeg = 13f;
        public float morningStarRightOfCypress = 10.5f; // N3: this far right of the cypress's base line, seen from the seat
        public float morningStarPitch = 32f;
        public SN_StarSpec[] stars =
        {
            new SN_StarSpec { name = "N1", yaw = 22f, pitch = 16f, named = 1, size = 1.1f },
            new SN_StarSpec { name = "N2", yaw = 6f, pitch = 24f, named = 2, size = 1.1f },
            new SN_StarSpec { name = "N3", yaw = -33f, pitch = 32f, named = 3, size = 1.8f, haloDeg = 5f, visibleAtStart = true },
            new SN_StarSpec { name = "S1", yaw = -60f, pitch = 22f },
            new SN_StarSpec { name = "S2", yaw = -52f, pitch = 58f, visibleAtStart = true, size = 1.2f },
            new SN_StarSpec { name = "S3", yaw = -26.5f, pitch = 15.5f, size = 0.8f },
            new SN_StarSpec { name = "S4", yaw = -18f, pitch = 55f },
            new SN_StarSpec { name = "S5", yaw = -4f, pitch = 64f, size = 0.8f },
            new SN_StarSpec { name = "S6", yaw = 15f, pitch = 59f },
            new SN_StarSpec { name = "S7", yaw = 31f, pitch = 27f, size = 0.9f },
            new SN_StarSpec { name = "S8", yaw = 62f, pitch = 24f, visibleAtStart = true, size = 1.2f },
            new SN_StarSpec { name = "S9", yaw = 70f, pitch = 60f, size = 0.9f },
        };

        [Header("Star fields (two meshes, one draw call each)")]
        public int smallStars = 150;
        public Vector2 smallStarDeg = new Vector2(0.15f, 0.35f);
        [Tooltip("This share of the small stars shows before the lantern, at earlyAlpha; the rest come with the lantern.")]
        public float smallEarlyShare = 0.33f, earlyAlpha = 0.35f;
        public float twinkle = 0.45f;
        public int mediumStars = 25;
        public Vector2 mediumStarDeg = new Vector2(0.4f, 0.6f);
        [Tooltip("Medium stars' halos: this diameter once a star is woken, the full one when the sky is painted.")]
        public float mediumHaloSmallDeg = 1.2f, mediumHaloFullDeg = 2.6f;
        public float fieldMinPitch = 6f;
        public int starFieldSeed = 1889;

        [Header("Wind (the guide path, from Cici's red line)")]
        [Tooltip("Directions (yaw, pitch), in the order the wind is traced: Cici's red line (Art/Source/StarryNight/swirl_line.json, 24 points), converted with the panel's mapping; the 3 at its outer end are dropped (inside the panel's soft edge).")]
        public Vector2[] guidePath =
        {
            new Vector2(-13.97f, 39.86f), new Vector2(-8.59f, 40.45f), new Vector2(-3.76f, 42.33f), new Vector2(0.81f, 44.85f),
            new Vector2(5.22f, 47.77f), new Vector2(9.77f, 50.34f), new Vector2(14.53f, 52.4f), new Vector2(19.57f, 53.78f),
            new Vector2(25.01f, 54.22f), new Vector2(30.29f, 53.41f), new Vector2(35.07f, 51.4f), new Vector2(39.32f, 48.11f),
            new Vector2(42.22f, 43.7f), new Vector2(43.39f, 38.57f), new Vector2(41.82f, 33.61f), new Vector2(37.47f, 30.78f),
            new Vector2(32.57f, 32.37f), new Vector2(33.94f, 36.44f), new Vector2(29.18f, 38.33f), new Vector2(24.78f, 35.62f),
            new Vector2(23.16f, 30.86f),
        };
        public float swirlBandHalfWidth = 8f;         // degrees either side of the path: the band the swirl paints in
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
        public Color ambient = new Color32(0x28, 0x33, 0x55, 0xFF);
        public Color moonLight = new Color32(0x86, 0xA8, 0xEC, 0xFF);
        public float moonLightIntensity = 0.55f;
        public Color lanternLight = new Color32(0xFF, 0xA8, 0x48, 0xFF);
        public float lanternIntensity = 6f, lanternRange = 6.5f;
        public Color fog = new Color32(0x16, 0x26, 0x4C, 0xFF);
        public float fogStart = 25f, fogEnd = 220f;
        [Tooltip("At full dawn: the ambient light, the fog, and the far hills' colour.")]
        public Color dawnAmbient = new Color32(0x5A, 0x66, 0x82, 0xFF);
        public Color dawnFog = new Color32(0x9A, 0xA6, 0xB8, 0xFF);
        [Tooltip("The far hills' tint (as horizonNight / horizonDawn, a little darker: farther away).")]
        public Color hillRingNight = new Color(0.56f, 0.60f, 0.74f, 1f);
        public Color hillRingDawn = new Color(0.84f, 0.85f, 0.90f, 1f);

        [Header("Colour grading (the look pass; off by default: its cost on a Quest is not measured yet)")]
        [Tooltip("On: SN_Bootstrap adds a global Volume with SN_Grading.asset (Color Adjustments) and turns post-processing on for the scene's camera.")]
        public bool colourGrading;
        public float gradingPostExposure = 0.35f, gradingContrast = 14f, gradingSaturation = 20f;
        public Color gradingFilter = new Color(1f, 0.97f, 0.92f, 1f);

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

        /// <summary>The morning star's direction: morningStarRightOfCypress right of the cypress's base line, seen from the seat eye.</summary>
        public Vector2 MorningStarYawPitch()
        {
            Vector3 toCypress = cypressBase - SeatEye;
            float cypressYaw = Mathf.Atan2(toCypress.x, toCypress.z) * Mathf.Rad2Deg;
            return new Vector2(cypressYaw + morningStarRightOfCypress, morningStarPitch);
        }

        /// <summary>A star's direction from the seat eye (N3 follows the cypress).</summary>
        public Vector2 StarYawPitch(SN_StarSpec s) => s.named == 3 ? MorningStarYawPitch() : new Vector2(s.yaw, s.pitch);

        /// <summary>The far hills' radius (the painted horizon sits horizonInset in front of it).</summary>
        public float HillRingRadius(float skyRadius) => skyRadius * hillRingRadiusFraction;

        /// <summary>The painted horizon's yaw range, from the seat: the spire at horizonSpireYaw.</summary>
        public float HorizonYawMin => horizonSpireYaw - horizonSpireU * horizonWidthDeg;
        public float HorizonYawMax => HorizonYawMin + horizonWidthDeg;
    }
}
