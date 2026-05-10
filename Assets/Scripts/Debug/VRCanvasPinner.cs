using UnityEngine;

namespace Inspection.Debugging
{
    /// <summary>
    /// Defensive runtime guard: re-applies the WorldSpace canvas's position + local scale every
    /// LateUpdate so that a stray <see cref="UnityEngine.UI.CanvasScaler"/> (or another component
    /// added by package upgrades) cannot zero the transform on play start. The original symptom
    /// without this guard was the canvas resetting to scale=(0,0,0) at pos=(960,540,2) — a
    /// CanvasScaler artifact even when the scaler was disabled.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class VRCanvasPinner : MonoBehaviour
    {
        public Vector3 worldPosition = new Vector3(0f, 1.6f, 2.0f);
        public Vector3 localScale = new Vector3(0.0018f, 0.0018f, 0.0018f);
        public Quaternion rotation = Quaternion.identity;
        public Vector2 sizeDelta = new Vector2(1920, 1080);

        RectTransform _rt;

        void Awake() => _rt = (RectTransform)transform;

        void LateUpdate()
        {
            if (_rt == null) return;
            _rt.sizeDelta = sizeDelta;
            _rt.localScale = localScale;
            _rt.position = worldPosition;
            _rt.rotation = rotation;
        }
    }
}
