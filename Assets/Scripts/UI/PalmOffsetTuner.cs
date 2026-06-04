using TMPro;
using UnityEngine;

namespace Inspection.UI
{
    /// <summary>
    /// Tiny in-headset tuner for the palm toggle's X/Y/Z offset (cm). Wire the
    /// three TMP labels and call XMinus/XPlus/etc from buttons. Each press
    /// nudges by 1cm and re-renders the label so the user can iterate without
    /// rebuilding.
    /// </summary>
    public sealed class PalmOffsetTuner : MonoBehaviour
    {
        [SerializeField] PalmMenuVisibility target;
        [SerializeField] TMP_Text xValue;
        [SerializeField] TMP_Text yValue;
        [SerializeField] TMP_Text zValue;

        void Start() => Refresh();
        void OnEnable() => Refresh();

        public void XMinus() { if (target != null) { target.sideCm--; Refresh(); } }
        public void XPlus()  { if (target != null) { target.sideCm++; Refresh(); } }
        public void YMinus() { if (target != null) { target.upCm--;   Refresh(); } }
        public void YPlus()  { if (target != null) { target.upCm++;   Refresh(); } }
        public void ZMinus() { if (target != null) { target.backCm--; Refresh(); } }
        public void ZPlus()  { if (target != null) { target.backCm++; Refresh(); } }

        void Refresh()
        {
            if (target == null) return;
            if (xValue != null) xValue.text = $"{target.sideCm} cm";
            if (yValue != null) yValue.text = $"{target.upCm} cm";
            if (zValue != null) zValue.text = $"{target.backCm} cm";
        }
    }
}
