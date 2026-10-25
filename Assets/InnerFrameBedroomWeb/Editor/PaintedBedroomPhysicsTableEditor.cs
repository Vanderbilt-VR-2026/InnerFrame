using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Two buttons under the physics table: fill it with every movable object in the open scene, and
    /// copy the current Rigidbody, rig scale and ray values back into it (for tuning on the components in Play mode).
    /// </summary>
    [CustomEditor(typeof(PaintedBedroomPhysicsTable))]
    public class PaintedBedroomPhysicsTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var table = (PaintedBedroomPhysicsTable)target;
            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Add Rows for Movable Objects in Open Scene",
                    "Adds a row for each movable object that has none, filled with its current settings. Existing rows are left alone.")))
            {
                Undo.RecordObject(table, "Add physics table rows");
                int added = 0;
                foreach (var m in Movables())
                {
                    if (table.Find(m.name) != null) continue;
                    var row = new PaintedBedroomPhysicsTable.Row { objectName = m.name };
                    CopyFrom(m, row);
                    table.rows.Add(row);
                    added++;
                }
                EditorUtility.SetDirty(table);
                Debug.Log($"[InnerFrame] Physics table: added {added} rows.");
            }

            if (GUILayout.Button(new GUIContent("Copy Current Values from Scene",
                    "Overwrites each existing row with its object's current Rigidbody and grab settings, and, in Play mode, " +
                    "the rig scale and ray length. Use it in Play mode after tuning on the components themselves.")))
            {
                Undo.RecordObject(table, "Copy physics values from scene");
                int copied = 0;
                foreach (var m in Movables())
                {
                    var row = table.Find(m.name);
                    if (row == null) continue;
                    CopyFrom(m, row);
                    copied++;
                }
                // outside Play mode the rig still has the builder's scale, not the table's
                if (Application.isPlaying)
                {
                    var origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
                    if (origin != null)
                    {
                        table.rigScale = origin.transform.localScale.x;
                        var caster = origin.GetComponentInChildren<CurveInteractionCaster>(true);
                        if (caster != null) table.rayDistance = caster.castDistance;
                    }
                    var grabber = Object.FindFirstObjectByType<PaintedBedroomDesktopGrabber>(FindObjectsInactive.Include);
                    if (grabber != null) table.desktopReach = grabber.reach;
                }
                EditorUtility.SetDirty(table);
                Debug.Log($"[InnerFrame] Physics table: copied {copied} rows from the scene" +
                          (Application.isPlaying ? ", and the rig scale and ray length." : " (rig values only copy in Play mode)."));
            }
        }

        static PaintedBedroomMovable[] Movables() =>
            Object.FindObjectsByType<PaintedBedroomMovable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        static void CopyFrom(PaintedBedroomMovable m, PaintedBedroomPhysicsTable.Row row)
        {
            var rb = m.GetComponent<Rigidbody>();
            if (rb != null)
            {
                row.hasPhysics = !rb.isKinematic;
                row.useGravity = rb.useGravity;
                row.mass = rb.mass;
            }
            var grab = m.GetComponent<XRGrabInteractable>();
            row.liftable = m.liftable && (grab == null || grab.enabled);
        }
    }
}
