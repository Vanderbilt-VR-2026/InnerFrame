using System;
using System.IO;
using UnityEngine;

/// <summary>Compatibility notice for the retired head-locked HUD diagnostic. Do not report
/// its old pixel-lock assertions as evidence for world-anchored panels.</summary>
[Obsolete("The guide is world-anchored. Use GalleryWelcomeChecks and WorldPanelChecks.")]
public static class VG_CompositorCaptureCheck
{
    public static bool Run(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "compositor-checks.txt"),
            "NOT RUN: head-locked HUD retired. Run GalleryWelcomeChecks for the physical sign and manual overview.\n");
        Debug.LogWarning("Head-locked HUD check retired; use the world-panel checks.");
        return false;
    }
}
