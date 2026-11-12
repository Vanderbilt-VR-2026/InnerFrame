using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edit-mode checks for the player menu (#160): Back goes to the Gallery from every other scene and is hidden in the
/// Gallery; the panel opens inside the controllers' reach; the wrist tag builds with the dialogue box's font and sprites.
/// (The panel's own look is the plain one until the menu style, #161, is redone.) With -menuShots &lt;folder&gt; it also
/// renders the panel and the wrist tag to PNGs.
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
        panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery);
        Check(panel.ShownBackLabel == PlayerMenuPanel.BackLabel, $"in a painting the button reads '{panel.ShownBackLabel}'");
        float distance = Vector3.Distance(camera.transform.position, panel.transform.position);
        Check(distance < 2f, $"it opens {distance:F2} m from the eyes, inside the 2 m ray");
        Check(Count<Button>(panel) == 2, $"two buttons (X and Back): {Count<Button>(panel)}");
        Check(panel.GetComponentInChildren<BoxCollider>() != null, "the title bar has a collider to grab");
        if (shots != null) Shot(camera, Path.Combine(shots, "menu-painting.png"));

        panel.ShowInFrontOf(camera.transform, null);
        Check(panel.ShownBackLabel == null, "in the Gallery the Back button is hidden");
        panel.Hide();

        // ---- the wrist tag ----
        Check(MenuLook.Font != null && MenuLook.Font.name.Contains("Baskerville"), $"the wrist tag and hints use the dialogue font ({(MenuLook.Font != null ? MenuLook.Font.name : "none")})");
        Check(MenuLook.PlateSprite != null && MenuLook.KeySprite != null, "the plate and button-circle sprites load from VG_Caption");
        var tag = new GameObject("Menu Wrist Tag").AddComponent<MenuWristTag>().Preview(camera);
        tag.transform.position = camera.transform.position + Vector3.forward * 0.25f;
        tag.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        Check(tag.GetComponentInChildren<Text>() != null && tag.GetComponentInChildren<Text>().text == "Menu", "the wrist tag reads 'Menu'");
        Check(tag.transform.Find("Key Menu") != null, "the wrist tag shows the Menu button's icon");
        if (shots != null) Shot(camera, Path.Combine(shots, "wrist-tag.png"));

        // ---- the button icons, close up ----
        tag.SetActive(false);
        var sheet = new GameObject("Icons", typeof(RectTransform));
        var sheetCanvas = sheet.AddComponent<Canvas>();
        sheetCanvas.renderMode = RenderMode.WorldSpace;
        sheet.transform.localScale = Vector3.one / VG_UI.UnitsPerMetre;
        sheet.transform.SetPositionAndRotation(camera.transform.position + Vector3.forward * 0.3f, Quaternion.identity);
        MenuLook.KeyIcon(sheet.transform, MenuLook.Icon.Menu, new Vector2(-0.05f, 0f), 0.08f);
        MenuLook.KeyIcon(sheet.transform, MenuLook.Icon.Grip, new Vector2(0.05f, 0f), 0.08f);
        Check(MenuLook.GripSprite != null && MenuLook.GripSprite.texture.width == 64, "the grip icon is drawn");
        if (shots != null) Shot(camera, Path.Combine(shots, "icons.png"));

        Debug.Log($"[PlayerMenuCheck] {(failures.Count == 0 ? "ALL PASSED" : failures.Count + " FAILED")}");
    }

    static int Count<T>(Component root) where T : Component => root.GetComponentsInChildren<T>(true).Length;

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
