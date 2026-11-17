using UnityEditor;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Play mode only: jumps a running Starry Night to the exit (Vincent's line, the walk to the light) without playing
    /// the night. It hands SN_Game a finished night as the room's progress (the path a late joiner takes) and steps
    /// the game's clock forward until Done; the exit then starts by itself exitDelaySeconds later.
    /// Menu: Tools > InnerFrame > Starry Night > Jump to the exit.
    /// </summary>
    public static class SN_ExitJump
    {
        [MenuItem("Tools/InnerFrame/Starry Night/Jump to the exit")]
        public static void Jump()
        {
            var game = Object.FindAnyObjectByType<SN_Game>();
            if (!Application.isPlaying || game == null) { Debug.LogWarning("[SN] Jump to the exit: enter Play mode in StarryNight.unity first."); return; }
            Debug.Log(ToDone(game) ? $"[SN] Jumped to Done at t={game.Clock:F1}; the exit starts in {game.layout.exitDelaySeconds:F0} s." : $"[SN] Jump to the exit: still {game.State} after 300 s of the game's clock.");
        }

        [MenuItem("Tools/InnerFrame/Starry Night/Jump to the exit", true)]
        static bool CanJump() => Application.isPlaying;

        /// <summary>A finished night, as another player in the room would report it.</summary>
        public static SN_Shared FinishedNight(SN_Game game) => new SN_Shared
        {
            arrived = true, seated = true, lanternDown = true, pageTaken = true,
            stars = game.sky.Stars.Length >= 31 ? int.MaxValue : (1 << game.sky.Stars.Length) - 1,
            wind = game.sky.PathLength,
        };

        /// <summary>Steps the game (0.1 s steps, up to 300 s of its clock) with a finished night shared, until Done. Also used by SN_ExitCheck.</summary>
        public static bool ToDone(SN_Game game)
        {
            for (int i = 0; i < 3000 && game.State != SN_State.Done; i++)
            {
                game.ShareRoom(FinishedNight(game));
                game.Step(0.1f);
            }
            if (game.voice != null) game.voice.DropQueued();   // the night's lines that piled up on the way
            return game.State == SN_State.Done;
        }
    }
}
