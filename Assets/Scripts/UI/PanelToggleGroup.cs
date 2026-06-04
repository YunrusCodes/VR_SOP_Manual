using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Inspection.UI
{
    /// <summary>
    /// At most one panel in this group is active at any time. Clicking a button
    /// either opens its panel (closing whichever other panel was open) or — if
    /// its own panel is already open — closes it. Each entry's icon label flips
    /// between a "visible" and "hidden" glyph to mirror the panel's state.
    /// </summary>
    public sealed class PanelToggleGroup : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public GameObject panel;
            public TMP_Text iconLabel;
            public string visibleIcon = "✕";
            public string hiddenIcon = "≡";
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        void Start() => RefreshIcons();
        void OnEnable() => RefreshIcons();

        public void Toggle(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            var clicked = entries[index];
            if (clicked.panel == null) return;

            bool wasActive = clicked.panel.activeSelf;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].panel != null) entries[i].panel.SetActive(false);
            if (!wasActive) clicked.panel.SetActive(true);

            RefreshIcons();
        }

        void RefreshIcons()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.iconLabel == null) continue;
                bool active = e.panel != null && e.panel.activeSelf;
                e.iconLabel.text = active ? e.visibleIcon : e.hiddenIcon;
            }
        }
    }
}
