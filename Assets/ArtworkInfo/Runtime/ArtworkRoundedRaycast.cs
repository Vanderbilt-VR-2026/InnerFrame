using UnityEngine;

namespace InnerFrame.ArtworkInfo
{
    /// <summary>Invisible UI hit shield follows the glass silhouette, leaving rounded cutouts clickable.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ArtworkRoundedRaycast : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] float radius = 45;
        public void Configure(float value) => radius = value;
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            var rect=(RectTransform)transform;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screenPoint,eventCamera,out var local)) return false;
            var bounds=rect.rect;
            if(!bounds.Contains(local)) return false;
            float r=Mathf.Clamp(radius,0,Mathf.Min(bounds.width,bounds.height)/2);
            Vector2 q=new Vector2(Mathf.Abs(local.x-bounds.center.x)-bounds.width/2+r,
                Mathf.Abs(local.y-bounds.center.y)-bounds.height/2+r);
            return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).sqrMagnitude<=r*r;
        }
    }
}
