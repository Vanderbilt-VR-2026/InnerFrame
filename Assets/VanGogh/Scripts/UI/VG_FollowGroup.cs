using UnityEngine;

/// <summary>
/// Keeps its object low in the player's view (Cici's concepts V1-V3): Distance ahead of the main camera, Below the gaze,
/// upright and facing the eye. It follows the gaze's yaw and pitch lazily: it stays put until the gaze is DeadZone
/// away, then eases there. When it's switched on, it starts in place. Sizes are world units; the angle is what matters.
/// </summary>
public class VG_FollowGroup : MonoBehaviour
{
    const float Distance = 1.3f;                  // metres ahead of the eye
    const float Below = 15f;                      // degrees under the gaze
    const float LowestGaze = -30f, HighestGaze = 45f;   // the gaze's pitch it follows, up positive
    const float DeadZone = 20f, Settled = 1f;     // degrees: start moving beyond, stop within
    const float EaseSeconds = 0.5f;               // most of the way (98 %) in this long

    float yaw, pitch;
    bool placed, moving;

    void OnEnable() => placed = false;

    void LateUpdate() => Step(Time.deltaTime);

    /// <summary>One frame, so tests can drive it.</summary>
    public void Step(float dt)
    {
        var eye = Camera.main;
        if (eye == null) return;
        var gaze = eye.transform.forward;
        float wantYaw = Mathf.Atan2(gaze.x, gaze.z) * Mathf.Rad2Deg;
        float wantPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(gaze.y, -1f, 1f)) * Mathf.Rad2Deg, LowestGaze, HighestGaze) - Below;
        if (!placed) { yaw = wantYaw; pitch = wantPitch; placed = true; }
        float off = Vector3.Angle(Direction(yaw, pitch), Direction(wantYaw, wantPitch));
        if (off > DeadZone) moving = true;
        if (moving)
        {
            float ease = 1f - Mathf.Exp(-4f * dt / EaseSeconds);
            yaw = Mathf.LerpAngle(yaw, wantYaw, ease);
            pitch = Mathf.LerpAngle(pitch, wantPitch, ease);
            moving = off > Settled;
        }
        var at = eye.transform.position + Direction(yaw, pitch) * Distance;
        transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - eye.transform.position, Vector3.up));
    }

    static Vector3 Direction(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
}
