using Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Push-to-talk voice chat between players, on the networked avatar (NetworkPlayer prefab).
///
/// Hold X on the left controller (V in the Editor) to talk to everyone in the room.
/// Others hear you from your avatar's head (see the Speaker on Head/Voice).
///
/// The microphone only records while X is held. Asking Vincent (hold B, or T in the Editor)
/// also uses the microphone, so while B is held voice chat stays off: your question to
/// Vincent is not broadcast to the other players, and the two never fight over the mic.
///
/// Sits on the prefab root next to VoiceNetworkObject; the Recorder is assigned by
/// NetworkPlayerVoiceBuilder. See docs/voice/README.md.
/// </summary>
public class PlayerVoice : NetworkBehaviour
{
    [Tooltip("This player's microphone stream. Set by InnerFrame > Multiplayer > Rebuild Network Player Prefab.")]
    public Recorder recorder;

    InputAction talk;
    InputAction askVincent;
    bool talking;

    /// <summary>True while this (local) player is transmitting.</summary>
    public bool IsTalking => talking;

    void Awake()
    {
        if (recorder == null) recorder = GetComponentInChildren<Recorder>(true);
        if (recorder == null) return;
        // Nothing records until the button is held, on every copy of the avatar.
        recorder.RecordWhenJoined = false;
        recorder.RecordingEnabled = false;
        recorder.TransmitEnabled = false;
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return; // only your own avatar listens to your controllers

        if (recorder == null)
            Debug.LogWarning("[PlayerVoice] No Recorder on the player prefab. Run InnerFrame > Multiplayer > Rebuild Network Player Prefab.");

        talk = new InputAction("Talk to players", InputActionType.Button);
        talk.AddBinding("<XRController>{LeftHand}/primaryButton"); // X
        talk.AddBinding("<Keyboard>/v");

        // Same bindings as Ask Vincent (Assets/VanGogh/Scripts/SpeechToProxy.cs on main).
        askVincent = new InputAction("Ask Vincent (voice chat pauses)", InputActionType.Button);
        askVincent.AddBinding("<XRController>{RightHand}/secondaryButton"); // B
        askVincent.AddBinding("<Keyboard>/t");

        talk.Enable();
        askVincent.Enable();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        SetTalking(false);
        DisposeInput();
    }

    void OnDestroy() => DisposeInput();

    void Update()
    {
        if (talk == null) return;

        if (talk.WasPressedThisFrame() && !HasMicPermission())
        {
            RequestMicPermission(); // first press asks; press again after allowing
            return;
        }

        SetTalking(talk.IsPressed() && !askVincent.IsPressed() && HasMicPermission());
    }

    // Applied every frame, not only on change: Photon may switch the Recorder on when it
    // joins the voice room, and the mic must stay off unless X is held.
    void SetTalking(bool on)
    {
        if (on != talking) Debug.Log(on ? "[PlayerVoice] Talking" : "[PlayerVoice] Stopped talking");
        talking = on;
        if (recorder == null) return;
        if (recorder.RecordingEnabled != on) recorder.RecordingEnabled = on;
        if (recorder.TransmitEnabled != on) recorder.TransmitEnabled = on;
    }

    void DisposeInput()
    {
        talk?.Dispose();
        askVincent?.Dispose();
        talk = askVincent = null;
    }

    static bool HasMicPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
        return true;
#endif
    }

    static void RequestMicPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
    }
}
