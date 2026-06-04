using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    /// <summary>
    /// Bottom-of-view pagination control: shows "← 2 / 5 →" and emits an
    /// event when the user clicks an arrow. The left arrow hides on the first
    /// page and the right arrow hides on the last, so the visible affordance
    /// matches what's actually possible. Replaces drag-to-scroll across the
    /// VR UI because finger-drift was triggering ScrollRects accidentally.
    /// </summary>
    public sealed class PaginationBar : MonoBehaviour
    {
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] TMP_Text pageLabel;

        int _page;
        int _pageCount;

        public int CurrentPage => _page;
        public int PageCount => _pageCount;
        public event Action<int> PageChanged;

        void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => SetCurrentPage(_page - 1, true));
            if (nextButton != null) nextButton.onClick.AddListener(() => SetCurrentPage(_page + 1, true));
            Refresh();
        }

        public void SetPageCount(int count)
        {
            _pageCount = Mathf.Max(1, count);
            if (_page >= _pageCount) _page = _pageCount - 1;
            Refresh();
        }

        public void SetCurrentPage(int page, bool notify)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, _pageCount - 1));
            if (page == _page) return;
            _page = page;
            Refresh();
            if (notify) PageChanged?.Invoke(_page);
        }

        void Refresh()
        {
            if (prevButton != null) prevButton.gameObject.SetActive(_page > 0);
            if (nextButton != null) nextButton.gameObject.SetActive(_page < _pageCount - 1);
            if (pageLabel != null) pageLabel.text = $"{_page + 1} / {_pageCount}";
        }
    }
}
