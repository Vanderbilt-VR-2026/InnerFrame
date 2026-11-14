using UnityEngine;

/// <summary>
/// Keeps hints and dialogue at a constant position low in the player's view, without a dead zone or easing.
/// Anchors a world-space HUD in the camera viewport, keeping its artwork and buttons in the same plane.
/// A caller can explicitly select the older eased placement for world-facing content.
/// </summary>
[DefaultExecutionOrder(10000)]
public class VG_FollowGroup : MonoBehaviour
{
    const float Settled = 1f;   // degrees: once following, it stops within this

    public VG_DialogueSettings settings;
    /// <summary>The head to follow; null: the main camera.</summary>
    public Transform head;
    /// <summary>Keep a constant lower-view position, including pitch and roll. Default for hints in every scene.</summary>
    public bool LockToView { get; set; } = true;
    /// <summary>Height of the anchor measured from the bottom of the game view.</summary>
    public float ViewportY { get; set; } = .32f;
    public bool FitDialogueLayout { get; set; }

    float yaw, pitch;
    bool placed, moving;
    Transform cachedHead;
    Camera headCamera;

    void OnEnable()
    {
        placed = moving = false;
        Application.onBeforeRender += BeforeRender;
    }
    void OnDisable() => Application.onBeforeRender -= BeforeRender;
    // XR may update the head pose after LateUpdate. Refresh only the locked mode before rendering.
    [BeforeRenderOrder(1000)]
    void BeforeRender() { if (LockToView) Step(0f); }

    void LateUpdate() => Step(Time.deltaTime);

    /// <summary>Straight into place (tests, renders, a teleport).</summary>
    public void Snap() { placed = false; Step(0f); }

    /// <summary>One frame, so tests can drive it.</summary>
    public void Step(float dt)
    {
        var camera = head == null ? Camera.main : null;
        var eye = head != null ? head : camera != null ? camera.transform : null;
        if (eye == null || settings == null) return;
        if (LockToView)
        {
            if (head != null)
            {
                if (cachedHead != head) { cachedHead = head; headCamera = head.GetComponent<Camera>(); }
                camera = headCamera;
            }
            if (camera != null) PlaceInViewport(camera);
            else
            {
                // A transform-only head (e.g. a simulated rig) has no camera projection.
                var facing = eye.rotation * Quaternion.Euler(settings.belowGaze, 0f, 0f);
                transform.SetPositionAndRotation(eye.position + facing * Vector3.forward * settings.distance, facing);
            }
            placed = moving = false;
            return;
        }
        var gaze = eye.forward;
        float wantYaw = Mathf.Atan2(gaze.x, gaze.z) * Mathf.Rad2Deg;
        float wantPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(gaze.y, -1f, 1f)) * Mathf.Rad2Deg, settings.lowestGaze, settings.highestGaze) - settings.belowGaze;
        if (!placed) { yaw = wantYaw; pitch = wantPitch; placed = true; }
        float off = Vector3.Angle(Direction(yaw, pitch), Direction(wantYaw, wantPitch));
        if (off > settings.deadZone) moving = true;
        if (moving)
        {
            float ease = 1f - Mathf.Exp(-4f * dt / settings.easeSeconds);
            yaw = Mathf.LerpAngle(yaw, wantYaw, ease);
            pitch = Mathf.LerpAngle(pitch, wantPitch, ease);
            moving = off > Settled;
        }
        var at = eye.position + Direction(yaw, pitch) * settings.distance;
        transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - eye.position, Vector3.up));
    }

    void PlaceInViewport(Camera camera)
    {
        float depth = settings.distance;
        var anchor = camera.ViewportToWorldPoint(new Vector3(.5f, ViewportY, depth));
        transform.SetPositionAndRotation(anchor, camera.transform.rotation);
        if (!FitDialogueLayout) return;

        // Preserve the authored angular size unless the game view would clip the box or its navigation.
        var extent = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth)) -
            camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        float width = Vector3.Dot(extent, camera.transform.right);
        float height = Vector3.Dot(extent, camera.transform.up);
        const float bottomOfNavigation = .34f; // layout metres from the dialogue top to the lowest button edge
        float widthFit = .84f * width / VG_DialogueBox.Width;
        float heightFit = (ViewportY - .08f) * height / bottomOfNavigation;
        float scale = Mathf.Min(settings.Scale, Mathf.Min(widthFit, heightFit));
        transform.localScale = Vector3.one * scale;
    }

    static Vector3 Direction(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
}
