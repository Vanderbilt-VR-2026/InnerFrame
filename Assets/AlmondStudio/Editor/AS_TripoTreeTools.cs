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
        }

        [MenuItem("Tools/InnerFrame/Almond Studio/Scatter Blossoms On Selected Tree", true)]
        static bool CanScatter() => Selection.activeGameObject != null;
    }
}
