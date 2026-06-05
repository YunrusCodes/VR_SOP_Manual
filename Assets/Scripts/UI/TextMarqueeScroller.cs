using TMPro;
using UnityEngine;

namespace Inspection.UI
{
    /// <summary>
    /// Scrolls a TMP_Text leftward when its preferred width overflows its parent
    /// rect. The parent should clip overflow (e.g. has a RectMask2D), otherwise
    /// the text just visually spills past the card. Text shorter than the rect
    /// is left untouched.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TextMarqueeScroller : MonoBehaviour
    {
        [SerializeField] float speed = 60f;
        [SerializeField] float gap = 40f;
        [SerializeField] float pauseAtStart = 1.2f;

        TMP_Text _tmp;
        RectTransform _container;
        float _offset;
        float _pause;
        bool _scrolling;
        float _lastTextLen = -1f;

        void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            _container = transform.parent as RectTransform;
        }

        void OnEnable() => ResetScroll();

        void ResetScroll()
        {
            _offset = 0f;
            _pause = pauseAtStart;
            _scrolling = false;
            if (_tmp != null)
            {
                var rt = _tmp.rectTransform;
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }
        }

        void LateUpdate()
        {
            if (_tmp == null || _container == null) return;

            // Re-evaluate every frame so changes to text (Bind) or container size
            // (layout rebuild) reset the scroll cleanly.
            float textLen = _tmp.preferredWidth;
            if (!Mathf.Approximately(textLen, _lastTextLen))
            {
                _lastTextLen = textLen;
                ResetScroll();
            }

            float containerW = _container.rect.width;
            if (textLen <= containerW + 1f)
            {
                if (_scrolling) ResetScroll();
                return;
            }

            _scrolling = true;
            _tmp.textWrappingMode = TextWrappingModes.NoWrap;
            _tmp.overflowMode = TextOverflowModes.Overflow;

            if (_pause > 0f)
            {
                _pause -= Time.unscaledDeltaTime;
                return;
            }

            float total = textLen + gap;
            _offset += speed * Time.unscaledDeltaTime;
            if (_offset >= total)
            {
                _offset = 0f;
                _pause = pauseAtStart;
            }
            var rt2 = _tmp.rectTransform;
            rt2.anchoredPosition = new Vector2(-_offset, rt2.anchoredPosition.y);
        }
    }
}
