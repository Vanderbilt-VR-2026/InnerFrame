using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace InnerFrame.ArtworkInfo
{
    // A deliberate integration seam; the standalone prefab has no navigation destination.
    [DisallowMultipleComponent]
    public sealed class ArtworkEnterAction : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField, Tooltip("Unconnected in the standalone design. Bind the approved existing Enter action later.")]
        UnityEvent enterRequested=new UnityEvent();
        public UnityEvent EnterRequested => enterRequested;
        public void Configure(Button value) => button=value;
        void Awake() { if(button!=null) button.onClick.AddListener(Enter); }
        void Enter()
        {
            if(enterRequested.GetPersistentEventCount()==0)
                Debug.Log("[ArtworkInfo] ENTER has no persistent navigation binding in this standalone design.",this);
            enterRequested.Invoke();
        }
        void OnDestroy() { if(button!=null) button.onClick.RemoveListener(Enter); }
    }
}
