using UnityEngine;

/// <summary>
/// Vincent without a rig: he breathes, turns a little toward you (or toward the mirror while he paints), and leans in
/// while he speaks. It moves only his Figure (his mesh, palette, brush and voice; its pivot is between his feet). The
/// root and the easel stay put, so MirrorScene's Head and Target, computed from the root, don't move.
/// On the VG_Vincent prefab root (VanGoghAssetSetup adds it). All amplitudes are guesses until tried on a Quest.
/// </summary>
public class VincentAlive : MonoBehaviour
{
    const float BreathHz = 0.25f, BreathDepth = 0.004f;   // the Figure's height: 1 ± 0.4 %, every 4 s
    const float NoticeDistance = 3.5f;                     // he turns toward you within this (world metres)
    const float TurnLimit = 12f, TurnSeconds = 0.3f;       // degrees either way; SmoothDamp time: most of the way in 0.6 s
    const float DriftDegrees = 1f, DriftHz = 0.07f;        // a slow idle sway
    const float LeanDegrees = 2f, LoudRms = 0.2f;          // forward lean at full voice (this RMS or louder)
    const float Attack = 0.05f, Release = 0.25f;           // how fast the lean follows his voice (seconds)
    const int Samples = 256;
    const string FigureName = "Figure";

    [Tooltip("Off: he stands still.")] public bool alive = true;

    /// <summary>Where the mirror is while he paints, or null. Set by MirrorScene.</summary>
    public Vector3? PaintingFrom { get; set; }

    public float Yaw { get; private set; }
    public float Lean { get; private set; }

    Transform figure;
    AudioSource voice;
    readonly float[] samples = new float[Samples];
    float clock, turn, turnSpeed, loudness;

    void Start()
    {
        figure = transform.Find(FigureName);
        if (figure == null) { Debug.LogWarning("[VanGogh] No Figure under Vincent, so he stands still."); enabled = false; return; }
        var talker = GetComponent<SpeechToProxy>();
        voice = talker != null ? talker.voice : null;
    }

    void Update()
    {
        float rms = 0f;
        if (voice != null && voice.isPlaying)
        {
            voice.GetOutputData(samples, 0);
            float sum = 0f;
            foreach (float s in samples) sum += s * s;
            rms = Mathf.Sqrt(sum / Samples);
        }
        var eye = Camera.main;
        Step(Time.deltaTime, eye != null ? eye.transform.position : (Vector3?)null, rms);
    }

    /// <summary>One frame, from plain inputs so tests can drive it: the player's head (if any) and his voice's RMS.</summary>
    public void Step(float dt, Vector3? viewer, float rms)
    {
        clock += dt;
        if (!alive)
        {
            Yaw = Lean = turn = turnSpeed = loudness = 0f;
            figure.localRotation = Quaternion.identity;
            figure.localScale = Vector3.one;
            return;
        }
        Vector3? toward = PaintingFrom;
        if (toward == null && viewer.HasValue && Flat(viewer.Value - transform.position).magnitude <= NoticeDistance) toward = viewer;
        float wanted = toward.HasValue
            ? Mathf.Clamp(Vector3.SignedAngle(Flat(transform.forward), Flat(toward.Value - transform.position), Vector3.up), -TurnLimit, TurnLimit)
            : 0f;
        turn = Mathf.SmoothDamp(turn, wanted, ref turnSpeed, TurnSeconds, Mathf.Infinity, dt);
        Yaw = Mathf.Clamp(turn + DriftDegrees * Mathf.Sin(2f * Mathf.PI * DriftHz * clock), -TurnLimit, TurnLimit);

        loudness = Mathf.Lerp(loudness, rms, 1f - Mathf.Exp(-dt / (rms > loudness ? Attack : Release)));
        Lean = LeanDegrees * Mathf.Clamp01(loudness / LoudRms);
        // positive x tips his head forward; both turns are about the Figure's pivot, between his feet
        figure.localRotation = Quaternion.Euler(0f, Yaw, 0f) * Quaternion.Euler(Lean, 0f, 0f);
        figure.localScale = new Vector3(1f, 1f + BreathDepth * Mathf.Sin(2f * Mathf.PI * BreathHz * clock), 1f);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
