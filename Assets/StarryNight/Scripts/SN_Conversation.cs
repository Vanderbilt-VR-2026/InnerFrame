using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>Shares the Starry Night dialogue box with hold-B conversation, pausing and resuming its story.</summary>
    public class SN_Conversation : MonoBehaviour
    {
        SN_Game game;
        SpeechToProxy talker;
        string previousHint;
        string previousTitle;

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
                previousTitle = game.ui.HintTitle;
                previousHint = game.ui.HintLine;
                game.voice.PauseForConversation(true);
                game.ui.Voice = talker.voice;
            }
            else
            {
                game.voice.PauseForConversation(false);
                game.ui.SetHint(previousTitle, previousHint);
            }
        }

        void LateUpdate()
        {
            if (talker == null || game == null) return;
            talker.SceneContext = "Inside The Starry Night, on the hill with Vincent. Current activity: " + game.State;
            // Keep the current action instruction; conversation is an additional choice.
            if (!talker.ConversationActive && !game.voice.Busy && !PlayerMenu.MenuOpen)
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
