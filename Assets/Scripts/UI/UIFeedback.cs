using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    /// <summary>
    /// At Start, walks every <see cref="Button"/> under this root and gives it
    /// a richer hover/pressed colour scheme so VR ray pointing has obvious
    /// feedback. Idempotent — runs once, then leaves the buttons alone.
    /// </summary>
    public sealed class UIFeedback : MonoBehaviour
    {
        [SerializeField] Color highlightedColor = new Color(0.30f, 0.65f, 1f, 1f);
        [SerializeField] Color pressedColor = new Color(0.15f, 0.50f, 0.95f, 1f);
        [SerializeField] Color selectedColor = new Color(0.30f, 0.65f, 1f, 1f);

        void Start()
        {
            foreach (var b in GetComponentsInChildren<Button>(includeInactive: true))
            {
                var cb = b.colors;
                cb.highlightedColor = highlightedColor;
                cb.pressedColor = pressedColor;
                cb.selectedColor = selectedColor;
                cb.colorMultiplier = 1f;
                cb.fadeDuration = 0.08f;
                b.colors = cb;
                b.transition = Selectable.Transition.ColorTint;
            }
        }
    }
}
