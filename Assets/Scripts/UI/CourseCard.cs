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
        [SerializeField] Button enterButton;

        public void Bind(string displayName, Action onEnter)
        {
            if (title != null) title.text = displayName;

            // Ensure the whole card is clickable, not just the small Enter button — VR ray
            // aiming at a 160px button is hard. Add a Button component on the card root that
            // forwards to the same handler.
            var cardBtn = GetComponent<Button>();
            if (cardBtn == null)
            {
                cardBtn = gameObject.AddComponent<Button>();
                var bg = GetComponent<Image>();
                if (bg != null) cardBtn.targetGraphic = bg;
            }
            cardBtn.onClick.RemoveAllListeners();
            cardBtn.onClick.AddListener(() => onEnter?.Invoke());

            if (enterButton != null)
            {
                enterButton.onClick.RemoveAllListeners();
                enterButton.onClick.AddListener(() => onEnter?.Invoke());
            }
        }
    }
}
