using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>Shares the Starry Night dialogue box with hold-B conversation, pausing and resuming its story.</summary>
    public class SN_Conversation : MonoBehaviour
    {
        SN_Game game;
        SpeechToProxy talker;
        public bool PhysicalInstructions => game != null && game.PhysicalInstructions;

        public void Connect(SN_Game story)
        {
            game = story;
            talker = gameObject.AddComponent<SpeechToProxy>();
            talker.voice = VanGoghSpawner.AddVoice(gameObject);
            talker.dialogueUI = game.ui;
            talker.ConversationChanged += OnConversation;
        }

        void OnConversation(bool active)
        {
            if (active)
            {
                game.voice.PauseForConversation(true);
                game.ui.Voice = talker.voice;
            }
            else
            {
                game.voice.PauseForConversation(false);
                game.RefreshActivityHint();
            }
        }

        void LateUpdate()
        {
            if (talker == null || game == null) return;
            talker.SceneContext = "Inside The Starry Night, on the hill with Vincent. Current activity: " + game.State;
            // Keep the current action instruction; conversation is an additional choice.
            if (!PhysicalInstructions && !talker.ConversationActive && !game.voice.Busy && !PlayerMenu.MenuOpen)
            {
                string line = game.ui.HintLine;
                if (!string.IsNullOrEmpty(line) && !line.Contains("{B}"))
                    game.ui.SetHint(game.ui.HintTitle, line + "\nHold {B} to ask Vincent");
            }
        }

        void OnDestroy()
        {
            if (talker != null) talker.ConversationChanged -= OnConversation;
        }
    }
}
