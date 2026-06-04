using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    /// <summary>
    /// Lives on a small head-locked toggle button so the user can hide the
    /// SOP manual when it's in the way and bring it back instantly. The main
    /// RootCanvas gets activated/deactivated wholesale; this button stays put
    /// on its own canvas so it remains reachable while the manual is hidden.
    /// </summary>
    public sealed class ManualToggle : MonoBehaviour
    {
        [SerializeField] GameObject manualRoot;
        [SerializeField] TMP_Text iconLabel;
        [SerializeField] bool visibleByDefault = true;
        [SerializeField] string visibleIcon = "✕";
        [SerializeField] string hiddenIcon = "≡";

        bool _visible;

        void Start()
        {
            _visible = visibleByDefault;
            Apply();
        }

        public void Toggle()
        {
            _visible = !_visible;
            Apply();
        }

        void Apply()
        {
            if (manualRoot != null) manualRoot.SetActive(_visible);
            if (iconLabel != null) iconLabel.text = _visible ? visibleIcon : hiddenIcon;
        }
    }
}
