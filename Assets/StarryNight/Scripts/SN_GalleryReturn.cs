using System.Collections;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The way back to the Gallery once the night is painted. When SN_Game enters Done, this waits openAfterSeconds (the
    /// morning star's fade, then 2 s; set by SN_Bootstrap) and opens the player menu (Assets/Script/Menu/PlayerMenu.cs) in
    /// front of the player, unless it is open already. Its "Back to Gallery" button fades back to the Gallery's start, as
    /// from the Bedroom; X closes it and the player can stay. Any time before that, the menu (left Menu button, or M)
    /// does the same. Added by SN_Bootstrap at Start; it changes nothing in SN_Game's states, hints or log.
    /// </summary>
    public class SN_GalleryReturn : MonoBehaviour
    {
        [Tooltip("Seconds from Done to the menu opening by itself.")]
        public float openAfterSeconds = 10f;

        /// <summary>True once the menu has been opened (or found open) after Done; for the checks.</summary>
        public bool Opened { get; private set; }

        Coroutine waiting;

        void OnEnable() => SN_Game.StateEntered += OnStateEntered;
        void OnDisable() => SN_Game.StateEntered -= OnStateEntered;

        void OnStateEntered(SN_State state, float clock)
        {
            if (state != SN_State.Done || waiting != null) return;
            waiting = StartCoroutine(OpenMenuLater());
        }

        IEnumerator OpenMenuLater()
        {
            yield return new WaitForSeconds(openAfterSeconds);
            var menu = PlayerMenu.Instance;
            if (menu == null) { Debug.LogWarning("[SN] Done, but there is no player menu to open."); yield break; }
            if (FindAnyObjectByType<PlayerMenuPanel>() == null) menu.Toggle();   // active panels only: an open menu stays as it is
            Opened = true;
            Debug.Log($"[SN] The night is painted: the player menu is open (Back to {menu.BackScene}).");
        }
    }
}
