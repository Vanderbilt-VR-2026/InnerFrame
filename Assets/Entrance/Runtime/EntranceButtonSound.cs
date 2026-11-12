using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.Entrance
{
    /// <summary>One sound for an accepted UI click, including XR, mouse and Submit.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class EntranceButtonSound : MonoBehaviour
    {
        Button button;
        public static void Bind(Button target)
        {
            if (target != null && !target.TryGetComponent<EntranceButtonSound>(out _))
                target.gameObject.AddComponent<EntranceButtonSound>();
        }
        void Awake() { button = GetComponent<Button>(); }
        void OnEnable() { button.onClick.AddListener(Clicked); }
        void OnDisable() { if (button != null) button.onClick.RemoveListener(Clicked); }
        void Clicked()
        {
            if (button != null)
                EntranceAudioDirector.Ensure().PlayClick();
        }
    }
}
