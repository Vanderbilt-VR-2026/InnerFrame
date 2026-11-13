using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.Gallery.EditorTools.Tests
{
    /// <summary>
    /// What stands on the Gallery's floor, and which of it is a stand (Gallery Part B, 2026-11-12: Cici asked for every
    /// pedestal, lectern-style placard stand and card stand to go; the bench, the rug, the statue, the flower stands and the
    /// lamp tables stay). Self-contained (UnityEngine only), so the same code runs on main's scene as the check's
    /// positive control: there it must find the three pedestals.
    /// A floor object is a renderer from within 5 cm of the floor, taller than 20 cm, centred inside the walls, not part of
    /// the player's rig. It is allowed when its name or mesh is one of Allowed; anything else standing there is a stand.
    /// </summary>
    public static class GalleryStands
    {
        /// <summary>Names or meshes (prefix match) of what may stand on the floor; the doorway's arch (GP_Arch, the Tripo model since
        /// 2026-11-12; Arch_Moulding before, on main) comes down to it too.</summary>
        public static readonly string[] Allowed = { "Bench_", "Rug", "Arch_Moulding", "GP_Arch", "GP_Plant", "Plant_", "GP_Plinth", "GP_FlowerStand", "GP_Statue", "Statue_", "GP_Vase", "GP_LampTable" };

        public struct Item { public string path, mesh; public Bounds bounds; public bool allowed; }

        public static List<Item> OnTheFloor(Scene scene)
        {
            var renderers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).ToList();
            var floor = renderers.FirstOrDefault(r => r.name == "Floor");
            if (floor == null) return new List<Item>();
            var f = floor.bounds;
            var inside = new Bounds(f.center, new Vector3(f.size.x - 0.4f, 100f, f.size.z - 0.4f));   // the floor runs under the 0.2 m walls
            var items = new List<Item>();
            foreach (var r in renderers)
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.min.y > 0.05f || b.size.y < 0.2f || b.size.x > inside.size.x * 0.8f || b.size.z > inside.size.z * 0.8f) continue;
                if (!inside.Contains(new Vector3(b.center.x, inside.center.y, b.center.z))) continue;
                if (InRig(r.transform)) continue;
                var mf = r.GetComponent<MeshFilter>();
                string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : r.GetType().Name;
                string path = PathOf(r.transform);
                bool allowed = Allowed.Any(a => r.name.StartsWith(a) || mesh.StartsWith(a) || (r.transform.parent != null && r.transform.parent.name.StartsWith(a)));
                items.Add(new Item { path = path, mesh = mesh, bounds = b, allowed = allowed });
            }
            return items;
        }

        public static List<Item> Stands(Scene scene) => OnTheFloor(scene).Where(i => !i.allowed).ToList();

        static bool InRig(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name.StartsWith("XR Origin") || t.name.StartsWith("Desktop Test Rig")) return true;
            return false;
        }

        public static string PathOf(Transform t) { var s = t.name; for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s; return s; }
    }
}
