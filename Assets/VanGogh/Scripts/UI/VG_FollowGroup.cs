using UnityEngine;

/// <summary>
/// Keeps its object low in the player's view (Cici's concepts V1-V3): the settings' distance ahead of the eye and
/// belowGaze under the gaze, upright and facing the eye. It follows the gaze's yaw and pitch lazily: it stays put until
/// the gaze is deadZone away, then eases there. When it's switched on, it starts in place. Sizes are world units; the
/// angle is what matters.
/// </summary>
public class VG_FollowGroup : MonoBehaviour
{
    const float Settled = 1f;   // degrees: once following, it stops within this

    public VG_DialogueSettings settings;
    /// <summary>The head to follow; null: the main camera.</summary>
    public Transform head;

    float yaw, pitch;
    bool placed, moving;

    void OnEnable() => placed = false;

    void LateUpdate() => Step(Time.deltaTime);

    /// <summary>Straight into place (tests, renders, a teleport).</summary>
    public void Snap() { placed = false; Step(0f); }

    /// <summary>One frame, so tests can drive it.</summary>
    public void Step(float dt)
    {
        var eye = head != null ? head : Camera.main != null ? Camera.main.transform : null;
        if (eye == null || settings == null) return;
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

    static Vector3 Direction(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
}
