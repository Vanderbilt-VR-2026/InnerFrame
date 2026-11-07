#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.Entrance
{
    // Screens are captured from Unity's actual Game view. No image composition,
    // alternate menu implementation, or concept art substitutes are used.
    public sealed class EntranceCaptureRunner : MonoBehaviour
    {
        EntranceMenu menu;
        string directory;
        readonly List<string> shots=new List<string>();
        readonly List<string> console=new List<string>();
        int errorCount;

        void Start()
        {
            DontDestroyOnLoad(gameObject);
            directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "InnerFrame-Actual-Screens-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(directory);
            Application.logMessageReceived+=RecordLog;
            StartCoroutine(Guard(Run()));
        }

        void RecordLog(string message,string trace,LogType type)
        {
            console.Add(type+": "+message+"\n"+trace);
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errorCount++;
        }

        void OnDestroy()
        {
            Application.logMessageReceived-=RecordLog;
            if(!string.IsNullOrEmpty(directory)) File.WriteAllText(Path.Combine(directory,"CONSOLE.txt"),string.Join("\n\n",console));
        }

        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();
            stack.Push(work);
            while(stack.Count>0)
            {
                object next=null;
                Exception failure=null;
                bool more=false;
                try { more=stack.Peek().MoveNext(); if(more) next=stack.Peek().Current; }
                catch(Exception error) { failure=error; }
                if(failure!=null)
                {
                    File.WriteAllText(Path.Combine(directory,"FAILED.txt"),failure.ToString());
                    Debug.LogException(failure);
                    EditorUtility.RevealInFinder(directory);
                    EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
                    yield break;
                }
                if(!more) { stack.Pop(); continue; }
                if(next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
        }

        IEnumerator Run()
        {
            float deadline=Time.realtimeSinceStartup+15f;
            while(menu==null || !menu.Ready)
            {
                menu=FindAnyObjectByType<EntranceMenu>();
                if(Time.realtimeSinceStartup>deadline) throw new InvalidOperationException("Entrance did not initialize. Check the first Console error.");
                yield return null;
            }
            menu.CaptureMode=true;
            yield return Shot("01-M1-Begin");
            menu.ReviewClick("Begin sign");
            RequirePage(1);
            if(menu.ReviewButton("Enter the Story sign").interactable)
                throw new InvalidOperationException("Continue must be disabled until a character is selected.");
            yield return Shot("02-M2-Choose-Character");
            menu.ReviewClick("Boy portrait");
            if(EntranceChoice.Role!=VisitorRole.Boy) throw new InvalidOperationException("Boy selection did not persist.");
            yield return Shot("03-M2-Boy-Selected");
            menu.ReviewClick("Girl portrait");
            if(EntranceChoice.Role!=VisitorRole.LadyInRed) throw new InvalidOperationException("Lady selection did not persist.");
            yield return Shot("04-M2-Lady-Selected");
            menu.ReviewClick("Enter the Story sign");
            RequirePage(2);
            yield return Shot("05-M3-New-Or-Join");
            menu.ReviewClick("New Room sign");
            RequirePage(3);
            if(menu.EnteredCode.Length!=4) throw new InvalidOperationException("New room code must have four digits.");
            yield return Shot("06-M4-New-Room-Code");
            menu.ReviewClick("Back sign");
            yield return null;
            yield return new WaitForEndOfFrame();
            menu.ReviewClick("Join Room sign");
            RequirePage(4);
            if(menu.ReviewButton("Join sign").interactable) throw new InvalidOperationException("Join must be disabled for an empty code.");
            yield return Shot("07-M5-Empty-Keypad");
            menu.ReviewClick("Key 4"); menu.ReviewClick("Key 7");
            yield return Shot("08-M5-Partial-Code");
            menu.ReviewClick("Key DEL");
            if(menu.EnteredCode!="4") throw new InvalidOperationException("Delete key failed.");
            menu.ReviewClick("Key 7"); menu.ReviewClick("Key 1"); menu.ReviewClick("Key 2");
            if(menu.EnteredCode!="4712" || !menu.ReviewButton("Join sign").interactable)
                throw new InvalidOperationException("Four-digit code entry failed.");
            yield return Shot("09-M5-Ready-To-Join");
            menu.ReviewClick("Join sign");
            for(int beat=0;beat<4;beat++)
            {
                float limit=Time.realtimeSinceStartup+15f;
                while(menu.IntroBeat<beat)
                {
                    if(Time.realtimeSinceStartup>limit) throw new InvalidOperationException("Intro did not reach beat "+beat);
                    yield return null;
                }
                if(menu.IntroBeat!=beat) throw new InvalidOperationException("Capture missed intro beat "+beat+". Retry with the Game tab visible.");
                yield return Shot((10+beat).ToString("00")+"-C"+(beat+1)+"-Intro");
            }
            yield return WaitForLand("Gallery");
            yield return Shot("14-Land-Actual-Gallery");
            SceneChangeButton entry = null;
            foreach (var button in FindObjectsByType<SceneChangeButton>(FindObjectsSortMode.None))
                if (button.gameObject.scene.name == "Gallery" && button.targetScene == "PaintedBedroom")
                { entry = button; break; }
            if (entry == null) throw new InvalidOperationException("Gallery has no active PaintedBedroom SceneChangeButton.");
            // Exercise the existing entry action; physical controller activation is a separate headset test.
            entry.Activate();
            for (int beat = 0; beat < 4; beat++)
            {
                float limit = Time.realtimeSinceStartup + 20f;
                while (PaintingEntryTransition.Beat < beat)
                {
                    if (Time.realtimeSinceStartup > limit || SceneFader.Instance.LastFailure != null)
                        throw new InvalidOperationException("Painting entry failed before P" + (beat + 1) + ": " + SceneFader.Instance.LastFailure);
                    yield return null;
                }
                if (PaintingEntryTransition.Beat != beat) throw new InvalidOperationException("Missed P beat " + beat);
                if (beat == 3)
                {
                    while (SceneFader.Instance.Coverage < .99f)
                    {
                        if (Time.realtimeSinceStartup > limit) throw new InvalidOperationException("P4 gold did not appear.");
                        yield return null;
                    }
                }
                yield return Shot((15 + beat).ToString("00") + "-P" + (beat + 1));
            }
            yield return WaitForLand("PaintedBedroom");
            yield return Shot("19-Land-Actual-PaintedBedroom");
            File.WriteAllText(Path.Combine(directory,"RESULTS.txt"),
                "Unity Game View capture completed.\n"+
                "Console errors/exceptions recorded during capture: "+errorCount+"\n"+
                "Checked: Canvas graphic-ray hits, Begin, both selections, Continue gate, New/Join navigation, four-digit entry, Delete, Join gate, intro beats, actual Gallery load/fade, existing painting-entry action, P1-P4, actual PaintedBedroom load/fade.\n"+
                "Not verified: physical Quest controllers, stereo rendering, two-player multiplayer, avatar selection/spawn, physical painting-entry ray hit, VR comfort.\n"+
                "These are screenshots of the actual EntranceMenu runtime. They are not concept mockups.\n"+
                string.Join("\n",shots));
            Debug.Log("[Entrance] Actual UI capture and desktop navigation checks completed: "+directory+". Quest and networking remain pending.");
            EditorUtility.RevealInFinder(directory);
            EditorApplication.delayCall+=()=> EditorApplication.isPlaying=false;
        }

        IEnumerator WaitForLand(string scene)
        {
            float deadline = Time.realtimeSinceStartup + 100f;
            while (SceneFader.Instance.LastCompletedScene != scene || SceneFader.Instance.IsTransitioning)
            {
                if (SceneFader.Instance.LastFailure != null) throw new InvalidOperationException(SceneFader.Instance.LastFailure);
                if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("Timed out waiting for real Land: " + scene);
                yield return null;
            }
            if (SceneManager.GetActiveScene().name != scene || Camera.main == null)
                throw new InvalidOperationException("Land finished without destination scene/camera: " + scene);
            yield return null;
        }

        void RequirePage(int expected)
        {
            if(menu.CurrentPage!=expected) throw new InvalidOperationException("Navigation failed. Expected page "+expected+", got "+menu.CurrentPage);
        }

        IEnumerator Shot(string name)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
            if(screenshot==null) throw new InvalidOperationException("Game view capture returned no image.");
            try { File.WriteAllBytes(Path.Combine(directory,name+".png"),screenshot.EncodeToPNG()); }
            finally { Destroy(screenshot); }
            shots.Add(name+".png");
        }
    }
}
#endif
