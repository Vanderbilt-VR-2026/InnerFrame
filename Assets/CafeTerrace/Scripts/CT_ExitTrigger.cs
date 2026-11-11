using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.CafeTerrace
{
    /// <summary>
    /// The end of the walkable street: a step or two past it, the journey goes on to The Starry Night. A hook only:
    /// it raises Reached and logs; it loads nextScene itself only if loadScene is ticked (off until the journey's
    /// scene changes are wired by their owners).
    /// </summary>
    public class CT_ExitTrigger : MonoBehaviour
    {
        public string nextScene = "StarryNight";
        public bool loadScene;

        public static event Action<CT_ExitTrigger> Reached;
        bool fired;

        void OnTriggerEnter(Collider other)
        {
            if (fired || other.GetComponentInParent<CharacterController>() == null) return;
            fired = true;
            Debug.Log($"[CT] The visitor reached the end of the street (next: {nextScene}).");
            Reached?.Invoke(this);
            if (loadScene) SceneManager.LoadScene(nextScene);
        }
    }
}
