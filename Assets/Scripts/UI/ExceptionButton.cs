using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    public sealed class ExceptionButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Button button;

        public void Bind(string text, Action onClick)
        {
            if (label != null) label.text = text;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
