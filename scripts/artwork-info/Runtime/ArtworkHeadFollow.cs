using UnityEngine;
using UnityEngine.XR;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;

namespace InnerFrame.ArtworkInfo
{
    /// <summary>Camera-relative UI with conservative full-panel clearance. Does not move the placard.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class ArtworkHeadFollow : MonoBehaviour
    {
        const float Skin = .025f;
        const float MinimumDistance = .35f;
        const float RecoverySpeed = .35f;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[64];
        Camera eyes;
        Transform rig;
        Bounds localBounds;
        Canvas[] canvases;
        BaseRaycaster[] raycasters;
        bool[] raycasterEnabled;
        Renderer[] renderers;
        bool[] canvasEnabled, rendererEnabled;
        bool prepared, visible = true, hasWall;
        Vector3 wallPoint, wallNormal;
        float distance, currentDistance, uprightBlend;
        public bool HasSafePose { get; private set; }
        public float CurrentDistance => currentDistance;

        static bool Usable(Camera camera) => camera != null && camera.isActiveAndEnabled;

        public static Camera Resolve(Camera preferred)
        {
            if (Usable(preferred))
            {
                var origin = preferred.GetComponentInParent<XROrigin>();
                if (origin != null && origin.isActiveAndEnabled && origin.Camera == preferred) return preferred;
            }
            Camera xrCamera = null;
            foreach (var origin in Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
            {
                if (!origin.isActiveAndEnabled || !Usable(origin.Camera)) continue;
                if (xrCamera != null && xrCamera != origin.Camera)
                {
                    Debug.LogError("[ArtworkInfo] Multiple active XR cameras. Assign Viewing Camera.");
                    return null;
                }
                xrCamera = origin.Camera;
            }
            if (xrCamera != null) return xrCamera;
            if (XRSettings.enabled) return null;
            return Usable(preferred) ? preferred : (Usable(Camera.main) ? Camera.main : null);
        }

        // The mount plane is a fail-safe for galleries with missing/nonconvex imported wall colliders.
        // Keep the opening side: never flip the plane merely because the user crossed it.
        public void Begin(Camera camera, float meters, Transform mount = null)
        {
            eyes = camera;
            var origin = camera != null ? camera.GetComponentInParent<XROrigin>() : null;
            rig = origin != null ? origin.transform : (camera != null ? camera.transform : null);
            distance = Mathf.Clamp(meters, 1.2f, 1.5f);
            hasWall = mount != null && camera != null;
            if (hasWall)
            {
                wallPoint = mount.position;
                wallNormal = -mount.forward;
                if (Vector3.Dot(camera.transform.position - wallPoint, wallNormal) < 0) wallNormal = -wallNormal;
            }
            PrepareBounds();
            RefreshPose(true);
        }

        void PrepareBounds()
        {
            if (prepared) return;
            canvases = GetComponentsInChildren<Canvas>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            raycasters = GetComponentsInChildren<BaseRaycaster>(true);
            raycasterEnabled = new bool[raycasters.Length];
            for (int i = 0; i < raycasters.Length; i++) raycasterEnabled[i] = raycasters[i].enabled;
            canvasEnabled = new bool[canvases.Length];
            rendererEnabled = new bool[renderers.Length];
            for (int i = 0; i < canvases.Length; i++) canvasEnabled[i] = canvases[i].enabled;
            for (int i = 0; i < renderers.Length; i++) rendererEnabled[i] = renderers[i].enabled;
            bool any = false;
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Include(transform.InverseTransformPoint(filter.transform.TransformPoint(corner)), ref any);
                }
            }
            var corners = new Vector3[4];
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            {
                rect.GetWorldCorners(corners);
                foreach (var corner in corners) Include(transform.InverseTransformPoint(corner), ref any);
            }
            if (!any) localBounds = new Bounds(Vector3.zero, new Vector3(1.428f, 1.248f, .012f));
            prepared = true;
        }

        void Include(Vector3 point, ref bool any)
        {
            if (!any) { localBounds = new Bounds(point, Vector3.zero); any = true; }
            else localBounds.Encapsulate(point);
        }

        void LateUpdate() { if (eyes != null) RefreshPose(false); }
        public void RefreshPose() => RefreshPose(true);

        void RefreshPose(bool immediate)
        {
            if (!Usable(eyes)) { SetVisible(false); HasSafePose = false; return; }
            var head = eyes.transform;
            // Prefer exact head orientation. Near a wall, reduce pitch/roll toward upright
            // only as far as necessary to fit the unchanged full-size panel. Position still follows gaze.
            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            Quaternion upright = flat.sqrMagnitude > .0001f
                ? Quaternion.LookRotation(flat.normalized, Vector3.up) : head.rotation;
            float blend = 0;
            bool safe = TryPlacement(head, head.rotation, out float minimum, out float limit);
            if (!safe && TryPlacement(head, upright, out minimum, out limit))
            {
                // Find the least tilt constraint that fits; fixed iteration count, no allocations.
                float low = 0, high = 1;
                for (int i = 0; i < 5; i++)
                {
                    float mid = (low + high) * .5f;
                    if (TryPlacement(head, Quaternion.Slerp(head.rotation, upright, mid), out _, out _)) high = mid;
                    else low = mid;
                }
                blend = high;
                safe = true;
            }
            Quaternion rotation = head.rotation;
            if (safe)
            {
                // Safety corrections are immediate; relax back to full head tilt gradually.
                uprightBlend = immediate || !HasSafePose ? blend : Mathf.Max(blend,
                    Mathf.MoveTowards(uprightBlend, blend, .8f * Time.unscaledDeltaTime));
                rotation = Quaternion.Slerp(head.rotation, upright, uprightBlend);
                safe = TryPlacement(head, rotation, out minimum, out limit);
                if (!safe) // Collision constraints need not be monotonic across the eased rotation.
                {
                    uprightBlend = blend;
                    rotation = Quaternion.Slerp(head.rotation, upright, blend);
                    safe = TryPlacement(head, rotation, out minimum, out limit);
                }
            }
            if (safe)
            {
                float next = immediate || !HasSafePose ? limit : Mathf.Clamp(
                    Mathf.MoveTowards(currentDistance, limit, RecoverySpeed * Time.unscaledDeltaTime), minimum, limit);
                // TryPlacement proved this entire interval clear at the actual displayed rotation.
                currentDistance = next;
                transform.SetPositionAndRotation(head.position + head.forward * next, rotation);
            }
            if (!safe && (HasSafePose || immediate))
                Debug.LogWarning("[ArtworkInfo] No safe space for the full-size detail panel. Step back or look away from the wall; the open panel will reappear. No scale or layout was changed.", this);
            HasSafePose = safe;
            SetVisible(safe);
        }

        bool TryPlacement(Transform head, Quaternion rotation, out float minimum, out float limit)
        {
            var scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var extents = Vector3.Scale(localBounds.extents, scale) + Vector3.one * Skin;
            var offset = rotation * Vector3.Scale(localBounds.center, scale);
            // The constrained panel may be tilted relative to the camera: protect ALL corners from near clipping.
            float eyeDepth = Support(rotation, extents, head.forward);
            minimum = Mathf.Max(MinimumDistance, eyes.nearClipPlane + eyeDepth - Vector3.Dot(offset, head.forward));
            limit = distance;
            if (hasWall)
            {
                float available = Vector3.Dot(head.position + offset - wallPoint, wallNormal)
                    - Support(rotation, extents, wallNormal);
                float approach = -Vector3.Dot(head.forward, wallNormal);
                if (approach > .0001f) limit = Mathf.Min(limit, available / approach);
                else if (approach < -.0001f) minimum = Mathf.Max(minimum, -available / -approach + .001f);
                else if (available < 0) return false;
            }
            int count = Physics.RaycastNonAlloc(head.position, head.forward, hits, distance + extents.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(hits[i].collider))
                {
                    var normal = hits[i].normal;
                    float approach = -Vector3.Dot(head.forward, normal);
                    if (approach > .0001f)
                        limit = Mathf.Min(limit, (Vector3.Dot(head.position + offset - hits[i].point, normal)
                            - Support(rotation, extents, normal)) / approach);
                    else return false;
                }
            if (limit < minimum) return false;
            var start = head.position + head.forward * minimum + offset;
            if (Occupied(start, extents, rotation)) return false;
            count = Physics.BoxCastNonAlloc(start, extents, head.forward, hits, rotation,
                Mathf.Max(0, limit - minimum), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(hits[i].collider)) limit = Mathf.Min(limit, minimum + hits[i].distance - Skin);
            return limit >= minimum && !Occupied(head.position + head.forward * limit + offset, extents, rotation);
        }

        static float Support(Quaternion rotation, Vector3 extents, Vector3 normal) =>
            Mathf.Abs(Vector3.Dot(rotation * Vector3.right, normal)) * extents.x
            + Mathf.Abs(Vector3.Dot(rotation * Vector3.up, normal)) * extents.y
            + Mathf.Abs(Vector3.Dot(rotation * Vector3.forward, normal)) * extents.z;

        bool Blocks(Collider collider) => collider != null && !collider.transform.IsChildOf(transform)
            && (rig == null || !collider.transform.IsChildOf(rig));

        bool Occupied(Vector3 center, Vector3 extents, Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(center, extents, overlaps, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return true;
            for (int i = 0; i < count; i++) if (Blocks(overlaps[i])) return true;
            return false;
        }

        void SetVisible(bool show)
        {
            if (!prepared || visible == show) return;
            visible = show;
            // Gate both input systems explicitly; never leave an invisible UI target behind a wall.
            for (int i = 0; i < raycasters.Length; i++)
                if (raycasters[i] != null) raycasters[i].enabled = show && raycasterEnabled[i];
            for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = show && canvasEnabled[i];
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = show && rendererEnabled[i];
        }

        void OnDisable()
        {
            eyes = null;
            HasSafePose = false;
            SetVisible(true); // Restore visual components for the next opening, without updating a closed panel.
        }
    }
}
