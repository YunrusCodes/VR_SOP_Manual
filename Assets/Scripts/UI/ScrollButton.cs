using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    /// <summary>
    /// Scrolls a target ScrollRect by a fixed normalized step when clicked.
    /// Lives on a Button next to the scroll area — drives the scroll position
    /// programmatically since we disabled drag-to-scroll on UGUI ScrollRects
    /// (drag was triggering unwanted scrolling whenever a user reached toward
    /// a button in VR).
    /// </summary>
    public sealed class ScrollButton : MonoBehaviour
    {
        [SerializeField] ScrollRect target;
        [SerializeField] bool scrollUp = true;
        [SerializeField] float step = 0.25f;

        void Start()
        {
            var button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            if (target == null) return;
            float delta = scrollUp ? step : -step;
            float current = target.verticalNormalizedPosition;
            target.verticalNormalizedPosition = Mathf.Clamp01(current + delta);
        }
    }
}
