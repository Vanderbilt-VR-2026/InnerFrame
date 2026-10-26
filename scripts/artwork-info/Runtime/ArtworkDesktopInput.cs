using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace InnerFrame.ArtworkInfo
{
    /// <summary>Test scene only. Recreate default mouse actions after every scene load.</summary>
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class ArtworkDesktopInput : MonoBehaviour
    {
        void Awake()
        {
            // Do not depend on editor-created transient action references surviving scene serialization.
            GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
