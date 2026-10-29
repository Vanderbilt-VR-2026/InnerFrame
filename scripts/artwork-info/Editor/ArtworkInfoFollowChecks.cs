using System;
using System.Collections;
using InnerFrame.ArtworkInfo;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace InnerFrame.EditorTools
{
    // Frame-driven checks: unlike a synchronous pose assertion, these exercise LateUpdate.
    public static class ArtworkInfoFollowChecks
    {
        static IEnumerator routine;
        static int frame;
        static GameObject fixture, panel;
        static RenderTexture target;

        [MenuItem("Tools/InnerFrame/Artwork Info/8. Test Follow and Wall Clearance in Play Mode")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "ArtworkInfoTestScene")
                throw new InvalidOperationException("Run only in ArtworkInfoTestScene Play Mode, not Gallery.");
            if (routine != null) throw new InvalidOperationException("Follow checks already running.");
            routine = Cases(); frame = -1;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Cleanup(); return; }
            if (Time.frameCount == frame) return;
            frame = Time.frameCount;
            try { if (!routine.MoveNext()) Cleanup(); }
            catch (Exception error) { Debug.LogException(error); Cleanup(); }
        }

        static void Cleanup()
        {
            EditorApplication.update -= Tick;
            (routine as IDisposable)?.Dispose(); routine = null;
            if (panel != null) Object.Destroy(panel);
            if (fixture != null) Object.Destroy(fixture);
            if (target != null) { target.Release(); Object.Destroy(target); }
        }

        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("[ArtworkInfo follow test] " + message); }

        static IEnumerator Cases()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtworkInfoReviewMigration.Generated + "/ArtworkDetailPanel.prefab");
            Check(asset != null, "Run migration first.");
            fixture = new GameObject("Temporary Issue14 collision checks");
            Vector3 origin = new Vector3(1000, 1000, 1000);
            var mount = new GameObject("Mount").transform; mount.SetParent(fixture.transform);
            mount.SetPositionAndRotation(origin + Vector3.up * .87f, Quaternion.Euler(0, 180, 0));
            var wall = new GameObject("Test wall", typeof(BoxCollider)); wall.transform.SetParent(fixture.transform);
            wall.transform.position = origin + new Vector3(0, 1.5f, -.1f);
            wall.GetComponent<BoxCollider>().size = new Vector3(10, 4, .2f);
            var cameraObject = new GameObject("Test eyes", typeof(Camera)); cameraObject.transform.SetParent(fixture.transform);
            var camera = cameraObject.GetComponent<Camera>(); camera.nearClipPlane = .03f;
            target = new RenderTexture(32, 32, 16); camera.targetTexture = target;
            panel = Object.Instantiate(asset); panel.SetActive(true);
            var detail = panel.GetComponent<ArtworkDetailPanel>(); detail.SetCamera(camera);
            var follow = panel.AddComponent<ArtworkHeadFollow>();
            Physics.SyncTransforms();
            foreach (float d in new[] { .5f, .8f, 1.2f, 1.8f })
            {
                var position = origin + new Vector3(0, 1.6f, d);
                camera.transform.SetPositionAndRotation(position,
                    Quaternion.LookRotation(origin + new Vector3(0, .765f, 0) - position));
                follow.Begin(camera, 1.35f, mount);
                yield return null; yield return null;
                Check(follow.HasSafePose, "No visible placement at " + d + "m looking at actual low More height.");
                CheckClear(panel, camera, origin.z);
                Check(panel.transform.localScale == Vector3.one, "Panel scaled.");
            }
            // Actual frame updates, no RefreshPose calls, prove follow instead of position-once behavior.
            var previous = panel.transform.position;
            camera.transform.position += Vector3.right * .3f;
            camera.transform.rotation = Quaternion.Euler(-15, 145, 8);
            yield return null; yield return null;
            Check(follow.HasSafePose && Vector3.Distance(previous, panel.transform.position) > .1f, "LateUpdate did not follow.");
            Check(Quaternion.Angle(panel.transform.rotation, camera.transform.rotation) < .1f, "Rotation did not follow in clear space.");
            Check(Vector3.Dot((panel.transform.position-camera.transform.position).normalized,camera.transform.forward) > .999f, "Panel left gaze center.");
            CheckClear(panel, camera, origin.z);
            // Abrupt approach must clamp inward without waiting for distance smoothing.
            camera.transform.SetPositionAndRotation(origin + new Vector3(0, 1.6f, .5f), Quaternion.Euler(0, 180, 0));
            yield return null; yield return null;
            Check(follow.HasSafePose, "Close, level view failed."); CheckClear(panel, camera, origin.z);
            // Sideways gaze: center ray misses wall; the full panel's corner still needs protection.
            camera.transform.rotation = Quaternion.Euler(0, 90, 0);
            yield return null; yield return null;
            Check(!follow.HasSafePose, "Unsafe perpendicular full-size panel should be gated.");
            foreach (var canvas in panel.GetComponentsInChildren<Canvas>(true))
                Check(!canvas.enabled, "Hidden Canvas still visible.");
            foreach(var raycaster in panel.GetComponentsInChildren<BaseRaycaster>(true))
                Check(!raycaster.enabled, "Hidden UI still interactive.");
            camera.transform.rotation = Quaternion.identity; // Look away, recover safely from the wall.
            yield return null; yield return null;
            Check(follow.HasSafePose, "Look-away recovery failed."); CheckClear(panel, camera, origin.z);
            detail.CloseButton.onClick.Invoke();
            Check(!panel.activeSelf, "X did not close.");
            previous = panel.transform.position;
            camera.transform.position += Vector3.right;
            yield return null; yield return null;
            Check(panel.transform.position == previous, "Closed panel kept tracking.");
            panel.SetActive(true); follow.Begin(camera, 1.35f, mount);
            yield return null; yield return null;
            Check(follow.HasSafePose && panel.transform.position != previous, "Reopen did not use current eyes.");
            Debug.Log("[ArtworkInfo] Multi-frame follow, 0.5/0.8/1.2/1.8m low-More view, wall corners, hidden raycast gate, recovery and X/reopen checks passed. Real mouse/Quest and visual comfort remain manual.");
        }

        static void CheckClear(GameObject root, Camera camera, float wallZ)
        {
            var corners = new Vector3[4];
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                ((RectTransform)canvas.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Check(corner.z >= wallZ + .02f, "Canvas corner crossed wall clearance.");
                    Check(Vector3.Dot(corner - camera.transform.position, camera.transform.forward) >= camera.nearClipPlane,
                        "Canvas corner crossed near plane.");
                }
            }
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null) continue;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var corner=filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    Check(corner.z>=wallZ+.02f,"Shell corner crossed wall clearance.");
                    Check(Vector3.Dot(corner-camera.transform.position,camera.transform.forward)>=camera.nearClipPlane,
                        "Shell crossed near plane.");
                }
            }
            Check(Vector3.Dot(root.transform.forward, root.transform.position - camera.transform.position) > 0,
                "Panel front is backwards.");
        }
    }
}
