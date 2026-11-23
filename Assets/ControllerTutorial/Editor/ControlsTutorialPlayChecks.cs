using System;
using System.IO;
using System.Text;
using InnerFrame.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Play Mode input and scene transitions in editor-created scenes, with networking disabled by the fixture.</summary>
[InitializeOnLoad]
public static class ControlsTutorialPlayChecks
{
    const string Pending = "InnerFrame.Controls.PlayChecks";
    const string Folder = "Assets/ControllerTutorial/Editor/TemporaryCheckScenes";
    static readonly StringBuilder report = new StringBuilder();
    static int phase, frames, failures;
    static double started;
    static TutorialManager original;
    static AsyncOperation loading;

    static ControlsTutorialPlayChecks() => EditorApplication.playModeStateChanged += Changed;

    public static void RunBatch()
    {
        AssetDatabase.CreateFolder("Assets/ControllerTutorial/Editor", "TemporaryCheckScenes");
        foreach (string name in new[] { "ControlsSessionA", "ControlsSessionB" })
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var eye = new GameObject("Eyes").AddComponent<Camera>();
            eye.tag = "MainCamera"; eye.transform.position = new Vector3(0, 1.6f, 0);
            EditorSceneManager.SaveScene(scene, Folder + "/" + name + ".unity");
        }
        EditorSceneManager.OpenScene(Folder + "/ControlsSessionA.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // NetworkSession delays its join by a frame. Remove it before that frame so these
            // local input/lifetime checks cannot join a real multiplayer room.
            if (NetworkSession.Instance != null)
            {
                NetworkSession.Instance.StopAllCoroutines();
                UnityEngine.Object.DestroyImmediate(NetworkSession.Instance.gameObject);
            }
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Pending, false);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
            EditorApplication.Exit(SessionState.GetInt(Pending + ".failures", 1) == 0 ? 0 : 1);
        }
    }

    static void Check(bool ok, string what)
    {
        report.AppendLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok) failures++;
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 60) throw new TimeoutException("Play Mode check timed out.");
            if (++frames < 10) return;
            if (phase == 0)
            {
                Check(ControlsTutorialChecks.RunInputInPlayMode(), "synthetic A press positive control and disabled Jump in the live Input System");
                original = TutorialManager.Instance;
                Check(original != null && PlayerMenu.Instance != null, "automatic tutorial/menu bootstrap in Play Mode");
                original.Complete(TutorialStep.GalleryMove);
                original.Offer(TutorialStep.Vincent, original, new Vector3(0, 1.4f, 1.3f));
                original.Present(1f);
                Check(original.VisibleStep == TutorialStep.Vincent, "unfinished prompt appears before scene transition");
                loading = EditorSceneManager.LoadSceneAsyncInPlayMode(Folder + "/ControlsSessionB.unity", new LoadSceneParameters(LoadSceneMode.Single));
                phase = 1; frames = 0;
            }
            else if (phase == 1 && loading.isDone)
            {
                Check(TutorialManager.Instance == original && original.Session.IsComplete(TutorialStep.GalleryMove), "same manager and completion survive actual Single scene load");
                Check(original.VisibleStep == null, "scene load clears old contextual prompt");
                original.Offer(TutorialStep.GalleryMove, original, new Vector3(0, 1.4f, 1.3f));
                original.Present(.3f);
                Check(original.VisibleStep == null, "completed lesson cannot reappear in the new scene");
                PlayerMenu.Instance.OpenControls();
                Check(EntranceControllerGuide.IsOpen && PlayerMenu.MenuOpen, "Controls opens in Play Mode after transition");
                phase = 2; frames = 0;
            }
            else if (phase == 2)
            {
                PlayerMenu.Instance.Toggle();
                Check(!EntranceControllerGuide.IsOpen && SceneManager.GetActiveScene().name == "ControlsSessionB", "Menu closes Controls in place in Play Mode");
                Finish();
            }
        }
        catch (Exception ex) { failures++; report.AppendLine("EXCEPTION " + ex); Finish(); }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        report.AppendLine("RESULT: " + failures + " failure(s). Synthetic input; no physical controller or AI request.");
        Directory.CreateDirectory("Logs/Controls");
        File.WriteAllText("Logs/Controls/play-mode.txt", report.ToString());
        Debug.Log(report.ToString());
        SessionState.SetInt(Pending + ".failures", failures);
        EditorApplication.ExitPlaymode();
    }
}
