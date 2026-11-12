using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InnerFrame.AlmondStudio.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Almond Studio > Scatter Blossoms On Selected Tree: select the Tripo tree in the scene and run
    /// it. It adds AS_TripoTree if missing and scatters its blossoms over the outer crown (above minHeight of the tree's
    /// height, at least minReach out from the trunk, no two closer than minSpacing), in the tree's local space, so they
    /// stay on the branches if the tree is moved or scaled later. Run it again after changing the numbers on
    /// AS_TripoTree; save the scene afterwards.
    /// </summary>
    public static class AS_TripoTreeTools
    {
        [MenuItem("Tools/InnerFrame/Almond Studio/Scatter Blossoms On Selected Tree")]
        public static void Scatter()
        {
            var go = Selection.activeGameObject;
            var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null)
            {
                EditorUtility.DisplayDialog("Almond Studio", "Select the almond tree (the Tripo model) in the Hierarchy first.", "OK");
                return;
            }
            ScatterOn(go);
        }

        /// <summary>
        /// Build calls this: makes sure the almond tree in the open scene carries AS_TripoTree with its blossoms. The tree
        /// is the one already marked, else the scene's model named like "almond" or "tree" (else the biggest model) that
        /// stands outside the window wall (z beyond roomDepth). Scatters only if it has none yet, so Build keeps a
        /// scatter you tuned. Returns the tree, or null (with a Console line saying why).
        /// </summary>
        public static AS_TripoTree EnsureOnScene(UnityEngine.SceneManagement.Scene scene, AS_Layout layout)
        {
            AS_TripoTree tree = null;
            foreach (var r in scene.GetRootGameObjects()) { tree = r.GetComponentInChildren<AS_TripoTree>(true); if (tree != null) break; }
            if (tree == null)
            {
                GameObject best = null; float bestScore = 0f;
                foreach (var r in scene.GetRootGameObjects())
                {
                    if (r.name == "AS_Bootstrap" || r.name.Contains("XR Origin")) continue;
                    foreach (var mf in r.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mr = mf.GetComponent<MeshRenderer>();
                        if (mf.sharedMesh == null || mr == null) continue;
                        var b = mr.bounds;
                        if (b.center.z < layout.roomDepth + layout.wallThickness || b.size.y < 1.5f) continue;   // outside, and tree-sized
                        // the object to mark: the outermost prefab instance (the FBX) holding this mesh, else the mesh's own object
                        var holder = PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject) ?? mf.gameObject;
                        string n = holder.name.ToLowerInvariant() + " " + mf.name.ToLowerInvariant();
                        float score = b.size.y * b.size.x + ((n.Contains("almond") || n.Contains("tree")) ? 1000f : 0f);
                        if (score > bestScore) { bestScore = score; best = holder; }
                    }
                }
                if (best == null)
                {
                    Debug.LogWarning("[AS Setup] No almond tree in the scene outside the window: drag the Tripo tree (FBX) into AlmondStudio.unity outside the window and run Build again. Without it there are no blossoms to paint.");
                    return null;
                }
                tree = best.AddComponent<AS_TripoTree>();
                Debug.Log("[AS Setup] Marked \"" + best.name + "\" as the almond tree (AS_TripoTree). If that is the wrong object, remove the component, select the tree and use Scatter Blossoms On Selected Tree.");
            }
            if (tree.positions == null || tree.positions.Length == 0) ScatterOn(tree.gameObject);
            else Debug.Log($"[AS Setup] The almond tree \"{tree.name}\" has {tree.positions.Length} blossoms (kept; to redo them select it and use Scatter Blossoms On Selected Tree).");
            return tree;
        }

        /// <summary>Scatters the blossoms over this tree's outer crown (see the class summary).</summary>
        public static int ScatterOn(GameObject go)
        {
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[AS] " + go.name + " has no mesh: no blossoms."); return 0; }
            var tree = go.GetComponent<AS_TripoTree>();
            if (tree == null) tree = Undo.AddComponent<AS_TripoTree>(go);
            Undo.RecordObject(tree, "Scatter blossoms");

            var mesh = mf.sharedMesh;
            var v = mesh.vertices;
            var tris = mesh.triangles;
            var toWorld = mf.transform.localToWorldMatrix;
            var toTree = go.transform.worldToLocalMatrix;

            // the tree's extent in the world: its height, and the trunk's place (the lowest tenth of its vertices)
            float minY = float.MaxValue, maxY = float.MinValue;
            var world = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                world[i] = toWorld.MultiplyPoint3x4(v[i]);
                minY = Mathf.Min(minY, world[i].y);
                maxY = Mathf.Max(maxY, world[i].y);
            }
            float h = Mathf.Max(maxY - minY, 1e-3f);
            Vector2 trunk = Vector2.zero; int nTrunk = 0;
            for (int i = 0; i < world.Length; i++)
                if (world[i].y < minY + h * 0.1f) { trunk += new Vector2(world[i].x, world[i].z); nTrunk++; }
            trunk /= Mathf.Max(nTrunk, 1);
            float maxR = 1e-3f;
            for (int i = 0; i < world.Length; i++) maxR = Mathf.Max(maxR, Vector2.Distance(new Vector2(world[i].x, world[i].z), trunk));

            var rnd = new System.Random(tree.seed);
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var placed = new List<Vector3>();   // world positions, for the spacing check
            int triCount = tris.Length / 3, tries = 0;
            while (pos.Count < tree.count && tries++ < tree.count * 400)
            {
                int t = rnd.Next(triCount) * 3;
                Vector3 a = world[tris[t]], b = world[tris[t + 1]], c = world[tris[t + 2]];
                float r1 = (float)rnd.NextDouble(), r2 = (float)rnd.NextDouble();
                if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                Vector3 p = a + (b - a) * r1 + (c - a) * r2;
                if ((p.y - minY) / h < tree.minHeight) continue;
                if (Vector2.Distance(new Vector2(p.x, p.z), trunk) / maxR < tree.minReach) continue;
                bool near = false;
                foreach (var q in placed) if ((q - p).sqrMagnitude < tree.minSpacing * tree.minSpacing) { near = true; break; }
                if (near) continue;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (n.sqrMagnitude < 0.5f) continue;
                placed.Add(p);
                pos.Add(toTree.MultiplyPoint3x4(p));
                nrm.Add(toTree.MultiplyVector(n).normalized);
            }
            tree.positions = pos.ToArray();
            tree.normals = nrm.ToArray();
            EditorUtility.SetDirty(tree);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[AS] Scattered {pos.Count} blossoms on {go.name} (tree {h:0.0} m tall, crown {maxR:0.0} m wide). Save the scene (Cmd+S).");
            return pos.Count;
        }

        [MenuItem("Tools/InnerFrame/Almond Studio/Scatter Blossoms On Selected Tree", true)]
        static bool CanScatter() => Selection.activeGameObject != null;
    }
}
