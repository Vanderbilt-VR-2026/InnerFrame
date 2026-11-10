using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edit-mode checks for the player menu (#160, #161): Back goes to the Gallery from every other scene and is hidden in the
/// Gallery; the panel names where you are, uses the dialogue box's font, and fits inside the controllers' reach; the
/// wrist tag builds. With -menuShots &lt;folder&gt; it also renders the panel (in a painting and in the Gallery) and the
/// wrist tag to PNGs, for eyes on the layout before a headset build.
/// Menu: InnerFrame > Menu > Run Checks. Batch: -executeMethod PlayerMenuCheck.RunBatch [-menuShots &lt;folder&gt;] -quit
/// </summary>
public static class PlayerMenuCheck
{
    static readonly List<string> failures = new List<string>();

    [MenuItem("InnerFrame/Menu/Run Checks")]
    static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run(null);
        EditorUtility.DisplayDialog("Player menu checks", failures.Count == 0 ? "All passed." : string.Join("\n", failures), "OK");
    }

    public static void RunBatch()
    {
        string shots = null;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-menuShots") shots = args[i + 1];
        Run(shots);
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    static void Check(bool ok, string what)
    {
        Debug.Log($"[PlayerMenuCheck] {(ok ? "PASS" : "FAIL")} {what}");
        if (!ok) failures.Add(what);
    }

    static void Run(string shots)
    {
        failures.Clear();

        // ---- the back rule ----
        foreach (var scene in new[] { "PaintedBedroom", "StarryNight", "Bedroom_StyleExperiment", "SomeFuturePainting" })
            Check(PlayerMenu.BackFrom(scene, PaintingNames.Gallery) == PaintingNames.Gallery, $"Back from {scene} goes to the Gallery");
        Check(PlayerMenu.BackFrom(PaintingNames.Gallery, PaintingNames.Gallery) == null, "no Back in the Gallery");

        // ---- the panel ----
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Eyes").AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 1.6f, 0f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.10f, 0.18f);
        camera.nearClipPlane = 0.05f;
        camera.fieldOfView = 60f;

        var panel = PlayerMenuPanel.Create(camera);
        panel.ShowInFrontOf(camera.transform, "PaintedBedroom", PaintingNames.Gallery);
        Check(panel.ShownBackLabel == PlayerMenuPanel.BackLabel, $"in the Bedroom the button reads '{panel.ShownBackLabel}'");
        Check(panel.ShownPlace == "The Bedroom, 1888", $"in the Bedroom 'You are in' reads '{panel.ShownPlace}'");
        Check(MenuLook.Font != null && MenuLook.Font.name.Contains("Baskerville"), $"the menu uses the dialogue font ({(MenuLook.Font != null ? MenuLook.Font.name : "none")})");
        Check(MenuLook.PlateSprite != null && MenuLook.KeySprite != null, "the plate and button-circle sprites load from VG_Caption");
        var size = panel.Size;
        Check(size.x > 0.3f && size.x < 0.5f && size.y > 0.15f && size.y < 0.35f, $"the panel is {size.x:F2} x {size.y:F2} m");
        float distance = Vector3.Distance(camera.transform.position, panel.transform.position);
        Check(distance < 2f, $"it opens {distance:F2} m from the eyes, inside the 2 m ray");
        Check(Count<Button>(panel) == 2, $"two buttons (X and Back): {Count<Button>(panel)}");
        Check(panel.GetComponentInChildren<BoxCollider>() != null, "the top bar has a collider to grab");
        Check(Overflowing(panel).Count == 0, $"no label runs past the panel's edge {string.Join(", ", Overflowing(panel))}");
        if (shots != null) Shot(camera, Path.Combine(shots, "menu-painting.png"));

        panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery, null);
        Check(panel.ShownBackLabel == null, "in the Gallery the Back button is hidden");
        Check(panel.ShownPlace == "The Gallery", $"in the Gallery 'You are in' reads '{panel.ShownPlace}'");
        if (shots != null) Shot(camera, Path.Combine(shots, "menu-gallery.png"));
        panel.Hide();

        // ---- the wrist tag ----
        var tag = new GameObject("Menu Wrist Tag").AddComponent<MenuWristTag>().Preview(camera);
        tag.transform.position = camera.transform.position + Vector3.forward * 0.25f;
        tag.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        Check(tag.GetComponentInChildren<Text>() != null && tag.GetComponentInChildren<Text>().text == "Menu", "the wrist tag reads 'Menu'");
        if (shots != null) Shot(camera, Path.Combine(shots, "wrist-tag.png"));

        Debug.Log($"[PlayerMenuCheck] {(failures.Count == 0 ? "ALL PASSED" : failures.Count + " FAILED")}");
    }

    static int Count<T>(Component root) where T : Component => root.GetComponentsInChildren<T>(true).Length;

    // Active one-line labels wider than the panel (a wrapped label is allowed to fill its box).
    static List<string> Overflowing(PlayerMenuPanel panel)
    {
        var wide = new List<string>();
        float limit = VG_UI.U(panel.Size.x);
        foreach (var t in panel.GetComponentsInChildren<Text>(false))
            if (t.horizontalOverflow == HorizontalWrapMode.Overflow && t.preferredWidth > limit) wide.Add(t.name);
        return wide;
    }

    static void Shot(Camera camera, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1200, 800, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(image);
        Debug.Log($"[PlayerMenuCheck] rendered {path}");
    }
}
