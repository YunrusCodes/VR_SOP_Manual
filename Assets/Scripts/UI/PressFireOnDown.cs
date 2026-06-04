using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inspection.UI
{
    /// <summary>
    /// Fires <see cref="Button.onClick"/> the moment a PointerDown lands on
    /// the button, instead of waiting for the full click (down + up + same
    /// element) cycle. Important for VR poke: a poke press has to physically
    /// push through the panel and then pull back to register as a click in
    /// stock UGUI — this component cuts that 200-400 ms tail.
    /// <para/>
    /// We also flip <c>eventData.eligibleForClick</c> off so the stock
    /// <see cref="Button"/> doesn't re-invoke <c>onClick</c> on PointerUp,
    /// which would double-fire the callback.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PressFireOnDown : MonoBehaviour, IPointerDownHandler
    {
        Button _button;

        void Awake() => _button = GetComponent<Button>();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable || !_button.IsActive()) return;
            _button.onClick.Invoke();
            eventData.eligibleForClick = false;
        }
    }
}
