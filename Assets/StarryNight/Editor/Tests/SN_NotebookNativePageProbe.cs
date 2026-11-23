using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>Extension for the existing native Play runner. Its delegates operate the simulated controller;
    /// this helper never calls the page API, invokes a UI click or claims native Grip for its lantern fixture.</summary>
    public static class SN_NotebookNativePageProbe
    {
        public static IEnumerator CheckPinnedBook(SN_Game game, Func<Vector3, IEnumerator> aim,
            Action<float> setTrigger, Action<bool, string> check)
        {
            bool ready = game != null && game.Input is SN_InputXR && game.notebook != null &&
                game.sketchbook.PinnedForReading && !game.sketchbook.IsHeld;
            check(ready, "Native page fixture starts with actual XR input and a released, pinned reading book");
            if (!ready) yield break;
            check(!game.lantern.IsDown && !game.lantern.Dimming && !game.lanternGrab.IsHeld,
                "Lantern click-through control starts available and unheld");
            if (game.lantern.IsDown || game.lantern.Dimming || game.lanternGrab.IsHeld) yield break;
            var guide = game.notebook;
            int before = guide.CurrentPage, expected = before < SN_NotebookGuide.PageCount - 1 ? before + 1 : before - 1;
            var target = before < SN_NotebookGuide.PageCount - 1 ? guide.NextTarget : guide.PreviousTarget;
            var pose = new Pose(game.sketchbook.transform.position, game.sketchbook.transform.rotation);
            var state = game.State;
            var progress = game.Progress;
            // Explicit fixture: retained subtitles are dismissed, and a held lantern sits away from the ray.
            // Any leaked scene Trigger would dim it regardless of aim. A long dim duration prevents a failing
            // check from advancing story state; restore its exact prior light state afterward, even on failure.
            game.ui.DismissWords();
            var values = typeof(SN_Lantern).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType.IsValueType).Select(f => (field: f, value: f.GetValue(game.lantern))).ToArray();
            float dimSeconds = game.layout.lanternDimSeconds;
            float intensity = game.lantern.lamp != null ? game.lantern.lamp.intensity : 0;
            var flame = game.lantern.flame != null ? game.lantern.flame.localScale : Vector3.one;
            var knob = game.lantern.knob != null ? game.lantern.knob.localRotation : Quaternion.identity;
            var holder = new GameObject("Notebook click-through held-lantern fixture").transform;
            SceneManager.MoveGameObjectToScene(holder.gameObject, game.gameObject.scene);
            holder.position = game.transform.position + Vector3.up * 20f;
            game.layout.lanternDimSeconds = 60f;
            game.lanternGrab.HoldFallback(holder, Vector3.zero, Quaternion.identity);
            try
            {
                setTrigger(0); yield return Wait(.2f);
                yield return aim(target);
                setTrigger(1); yield return Wait(.2f);
                check(game.Input.TriggerHeld(1), "Native right Trigger reaches the scene's actual input action");
                check(guide.CurrentPage == expected, "Native Trigger turns the printed page exactly once");
                check(!game.lantern.IsDown && !game.lantern.Dimming,
                    "The page's same Trigger cannot dim the held-lantern target fixture");
                yield return aim(holder.position);
                yield return Wait(.2f);
                check(guide.CurrentPage == expected && !game.lantern.IsDown && !game.lantern.Dimming,
                    "Held Trigger leaving the paper neither repeats the page nor leaks into the scene");
                setTrigger(0); yield return Wait(.2f);
                check(game.State == state && game.Progress.stars == progress.stars && game.Progress.wind == progress.wind &&
                    game.LetterFound == progress.letterFound && game.Progress.pageTaken == progress.pageTaken,
                    "Native manual browsing invents no scene completion, awakened star, wind segment or hidden letter");
                check(game.sketchbook.PinnedForReading && Vector3.Distance(game.sketchbook.transform.position, pose.position) < .001f &&
                    Quaternion.Angle(game.sketchbook.transform.rotation, pose.rotation) < .1f,
                    "Printed navigation preserves the pinned book's released world pose");
            }
            finally
            {
                setTrigger(0);
                game.lanternGrab.ReturnHomeNow();
                game.layout.lanternDimSeconds = dimSeconds;
                foreach (var value in values) value.field.SetValue(game.lantern, value.value);
                if (game.lantern.lamp != null) game.lantern.lamp.intensity = intensity;
                if (game.lantern.flame != null) game.lantern.flame.localScale = flame;
                if (game.lantern.knob != null) game.lantern.knob.localRotation = knob;
                Object.Destroy(holder.gameObject);
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            do { frames++; yield return null; } while (frames < 4 || Time.realtimeSinceStartup < until);
        }
    }
}
