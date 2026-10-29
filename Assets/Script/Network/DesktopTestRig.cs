#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Editor only: in Play mode without a headset, a scene that only has an XR Origin (the Gallery)
/// gets a desktop player instead: W A S D to walk, mouse to look (click to capture, Esc to release,
/// or hold the right button), Shift to go faster. It stands where the XR Origin stood, at the same
/// scale, and the XR Origin is switched off. A dot in the middle of the view is an XR ray: aim it at
/// an XR button (the pedestal) and press E, or click while the mouse is captured, to press it the
/// way a controller would. It grabs nothing (grabbables are filtered out). Scenes with their own desktop rig
/// (the Painted Bedroom) are left alone. NetworkSession joins the room with it, so the Editor
/// counts as a player without a second headset. Creates itself; nothing is added to the scene.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DesktopTestRig : MonoBehaviour
{
    const float EyeHeight = 1.6f, WalkSpeed = 1.3f, RunSpeed = 2.6f, LookSensitivity = 0.12f;
    const float Reach = 3f;   // metres the press ray reaches, at scale 1

    Transform cam;
    XRRayInteractor ray;
    CharacterController controller;
    float yaw, pitch, verticalSpeed;
    Vector3 start;
    string blocked;     // the last thing the walk bumped into (not the floor), for the Console
    float nextReport, reportStart, travelled, wanted, slowest;
    int frames;
    Vector3 lastPos;

    void ResetReport()
    {
        reportStart = Time.unscaledTime;
        nextReport = reportStart + 1f;
        travelled = wanted = slowest = 0f;
        frames = 0;
        blocked = null;
    }

    // anything steeper than a ramp that the capsule touches while walking: a wall, a step, an invisible collider
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.7f) return;   // floor
        float height = hit.point.y - transform.position.y;
        blocked = $"\"{hit.collider.name}\" (in \"{(hit.collider.transform.parent ? hit.collider.transform.parent.name : "-")}\", {height:F2} m up)";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => { if (mode == LoadSceneMode.Single) SwapIn(); };
        SwapIn();
    }

    static void SwapIn()
    {
        if (XRSettings.isDeviceActive) return;   // a headset: play in VR
        var origin = FindAnyObjectByType<XROrigin>();   // active rigs only: the Painted Bedroom has already switched to its own
        if (origin == null) return;

        float scale = origin.transform.lossyScale.y;   // the Gallery's rig is scaled down; match it
        var go = new GameObject("Desktop Test Rig (Editor only)");
        SceneManager.MoveGameObjectToScene(go, origin.gameObject.scene);
        go.transform.SetPositionAndRotation(origin.transform.position, Quaternion.Euler(0f, origin.transform.eulerAngles.y, 0f));
        go.transform.localScale = Vector3.one * scale;

        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.7f; cc.radius = 0.2f; cc.center = new Vector3(0f, 0.85f, 0f); cc.stepOffset = 0.25f;
        // the Editor can run at 1000+ fps, so a frame's step is under the default 1 mm minimum and gets dropped
        cc.minMoveDistance = 0f;

        var camGo = new GameObject("Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(go.transform, false);
        camGo.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
        var camera = camGo.AddComponent<Camera>();
        if (origin.Camera != null) camera.CopyFrom(origin.Camera);   // same clear colour, clipping and layers as the headset view
        camera.transform.SetLocalPositionAndRotation(new Vector3(0f, EyeHeight, 0f), Quaternion.identity);
        camGo.AddComponent<AudioListener>();

        // one camera, one audio listener, one player. Switched off before the ray is added, so the ray
        // doesn't join an interaction manager inside the XR Origin (XRI makes one if none is left)
        origin.gameObject.SetActive(false);

        // the press ray: straight out of the middle of the view, select driven from Update
        var ray = camGo.AddComponent<XRRayInteractor>();
        ray.lineType = XRRayInteractor.LineType.StraightLine;
        ray.maxRaycastDistance = Reach * scale;
        ray.enableUIInteraction = false;   // the menu panel takes the mouse itself
        ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
        ray.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.Unused;
        ray.selectFilters.Add(new NoGrabbing());

        go.AddComponent<DesktopTestRig>();
        Debug.Log($"[DesktopTestRig] {origin.gameObject.scene.name}: no headset, so you're a desktop player. W A S D to walk, click and move the mouse to look, Esc frees the mouse, E presses what the dot is on.");
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>().transform;
        ray = GetComponentInChildren<XRRayInteractor>();
        yaw = transform.eulerAngles.y;
        start = transform.position;
        lastPos = start;
        ResetReport();
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        // press: E, or the left button once the mouse is captured (the click that captures it doesn't press)
        bool captured = Cursor.lockState == CursorLockMode.Locked;
        bool press = !PlayerMenu.OpenOnDesktop && (kb.eKey.isPressed || (captured && mouse != null && mouse.leftButton.isPressed));
        if (ray != null) ray.selectInput.QueueManualState(press, press ? 1f : 0f);

        // the player menu is open: the mouse is for its buttons, not for looking around
        if (mouse != null && !PlayerMenu.OpenOnDesktop)
        {
            if (mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
            if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState == CursorLockMode.Locked || mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * LookSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        bool fast = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();
        verticalSpeed = controller.isGrounded ? -1f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
        controller.Move((move * (fast ? RunSpeed : WalkSpeed) * transform.lossyScale.y + Vector3.up * verticalSpeed) * Time.deltaTime);

        // while walking, once a second: how fast it meant to go, how fast it went, the frame rate and the
        // slowest frame, and what it ran into. Tells an Editor that renders slowly apart from a walk that's held back
        if (move.sqrMagnitude > 0f)
        {
            Vector3 flat = Vector3.ProjectOnPlane(transform.position - lastPos, Vector3.up);
            travelled += flat.magnitude;
            wanted += (fast ? RunSpeed : WalkSpeed) * transform.lossyScale.y * move.magnitude * Time.deltaTime;
            frames++;
            slowest = Mathf.Max(slowest, Time.unscaledDeltaTime);
            if (Time.unscaledTime > nextReport)
            {
                float secs = Time.unscaledTime - reportStart;
                Debug.Log($"[DesktopTestRig] Walking at {travelled / secs:F2} m/s of {wanted / secs:F2} m/s wanted, " +
                    $"{frames / secs:F0} fps, slowest frame {slowest * 1000f:F0} ms, grounded {controller.isGrounded}" +
                    (blocked != null ? $", ran into {blocked}" : ", ran into nothing"));
                ResetReport();
            }
        }
        else ResetReport();
        lastPos = transform.position;

        // fell through a floor without a collider: back to the start, and stop falling
        if (transform.position.y < start.y - 5f)
        {
            controller.enabled = false;
            transform.position = start;
            controller.enabled = true;
            verticalSpeed = 0f;
            Debug.LogWarning("[DesktopTestRig] Fell below the floor (it has no collider there); back to the start.");
        }
    }

    // the aiming dot: white, yellow with a hint while it's on something you can press
    void OnGUI()
    {
        if (ray == null || PlayerMenu.OpenOnDesktop) return;
        var target = ray.hasHover && ray.interactablesHovered.Count > 0 ? ray.interactablesHovered[0] as Component : null;
        bool canPress = target != null && !(target is XRGrabInteractable);
        var old = GUI.color;
        GUI.color = canPress ? Color.yellow : Color.white;
        var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
        GUI.Label(new Rect(c.x - 6f, c.y - 12f, 20f, 24f), "●");
        if (canPress) GUI.Label(new Rect(c.x + 14f, c.y - 12f, 400f, 24f), $"E: press {target.name}");
        GUI.color = old;
    }

    // The desktop player presses buttons but picks nothing up.
    class NoGrabbing : IXRSelectFilter
    {
        public bool canProcess => true;
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => !(interactable is XRGrabInteractable);
    }
}
#endif
