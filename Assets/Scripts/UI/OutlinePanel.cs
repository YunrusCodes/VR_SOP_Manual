using System;
using Inspection.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    /// <summary>
    /// Hierarchical outline / index drawer for the current course. Groups steps by MainTitle and
    /// SubTitle and lets the user jump straight to any step.
    /// </summary>
    public sealed class OutlinePanel : MonoBehaviour
    {
        [SerializeField] Transform contentRoot;
        [SerializeField] Button closeButton;

        Action<int> _onJump;

        public void Init(Action<int> onJumpToStepOrder)
        {
            _onJump = onJumpToStepOrder;
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => Hide());
            }
        }

        public void Bind(Course course, int currentStepOrder)
        {
            ClearChildren(contentRoot);
            if (course == null || course.Steps == null) return;

            string lastMain = null;
            string lastSub = null;
            foreach (var step in course.Steps)
            {
                if (step.MainTitle != lastMain)
                {
                    SpawnHeader(step.MainTitle, isMain: true);
                    lastMain = step.MainTitle;
                    lastSub = null;
                }
                if (!string.IsNullOrEmpty(step.SubTitle) && step.SubTitle != lastSub)
                {
                    SpawnHeader(step.SubTitle, isMain: false);
                    lastSub = step.SubTitle;
                }
                SpawnStepRow(step, currentStepOrder);
            }

            // Force layout refresh — VerticalLayoutGroup sometimes doesn't realize new children
            // exist until the end of the frame, which leaves the panel looking empty.
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot as RectTransform);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        void SpawnHeader(string text, bool isMain)
        {
            var go = new GameObject(isMain ? "MainTitle" : "SubTitle",
                typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(contentRoot, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = isMain ? 56 : 44;

            if (isMain)
            {
                var bg = go.AddComponent<Image>();
                bg.color = new Color(0.20f, 0.30f, 0.50f, 1f);
            }

            var t = NewText(go.transform, text, isMain ? 36 : 30,
                isMain ? Color.white : new Color(0.7f, 0.85f, 1f));
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(isMain ? 16 : 32, 0);
            rt.offsetMax = new Vector2(-8, 0);
        }

        void SpawnStepRow(Step step, int currentStepOrder)
        {
            var go = new GameObject("StepRow",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(contentRoot, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 56;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.16f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;

            bool isCurrent = step.Order == currentStepOrder;
            var labelColor = isCurrent ? new Color(1f, 0.85f, 0.4f) : Color.white;
            var t = NewText(go.transform, $"{step.Order}. {step.Name}", 30, labelColor);
            if (isCurrent) t.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(48, 0);
            rt.offsetMax = new Vector2(-8, 0);

            int captured = step.Order;
            btn.onClick.AddListener(() =>
            {
                _onJump?.Invoke(captured);
                Hide();
            });
        }

        static GameObject NewText(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = color;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return go;
        }

        static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
