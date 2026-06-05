using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class CourseCard : MonoBehaviour
    {
        [SerializeField] TMP_Text title;

        public void Bind(string displayName, Action onEnter)
        {
            if (title != null) title.text = displayName;

            // Whole card is the button — a 160 px enter button was painful to aim
            // at with a ray, so the entire row now forwards to the handler.
            var cardBtn = GetComponent<Button>();
            if (cardBtn == null)
            {
                cardBtn = gameObject.AddComponent<Button>();
                var bg = GetComponent<Image>();
                if (bg != null) cardBtn.targetGraphic = bg;
            }
            cardBtn.onClick.RemoveAllListeners();
            cardBtn.onClick.AddListener(() => onEnter?.Invoke());
        }
    }
}
