using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inspection.App;
using Inspection.Domain;
using Inspection.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    public sealed class ManualListView : MonoBehaviour
    {
        [SerializeField] Transform contentRoot;
        [SerializeField] CourseCard courseCardPrefab;
        [SerializeField] GameObject emptyState;
        [SerializeField] TMP_Text errorLabel;
        [SerializeField] Button refreshButton;
        [SerializeField] PaginationBar paginationBar;
        [SerializeField] int pageSize = 6;

        ICourseClient _client;
        AppRouter _router;
        LoadingOverlay _overlay;
        CancellationTokenSource _cts;
        List<CourseSummary> _allCourses = new List<CourseSummary>();
        int _currentPage;

        public void Init(ICourseClient client, AppRouter router, LoadingOverlay overlay)
        {
            _client = client;
            _router = router;
            _overlay = overlay;
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveAllListeners();
                refreshButton.onClick.AddListener(() => _ = RefreshWithOverlayAsync());
            }
            if (paginationBar != null)
            {
                paginationBar.PageChanged -= OnPageChanged;
                paginationBar.PageChanged += OnPageChanged;
            }
        }

        async Task RefreshWithOverlayAsync()
        {
            if (_overlay != null) _overlay.Show("重新載入課程清單…");
            try { await RefreshAsync(); }
            finally { if (_overlay != null) _overlay.Hide(); }
        }

        public async Task RefreshAsync()
        {
            if (_client == null) { Log.E("ManualListView.Init not called"); return; }
            SetError(null);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                var raw = await _client.ListCoursesAsync(_cts.Token);
                _allCourses = new List<CourseSummary>(raw);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                Log.W($"ListCourses failed: {ex.Message}");
                _allCourses = new List<CourseSummary>();
                SetError("後端無法連線，請確認 API 是否啟動或網路設定。");
            }

            _currentPage = 0;
            RebuildPage();
        }

        void InjectTestCourses()
        {
            for (int i = 1; i <= 15; i++)
                _allCourses.Add(new CourseSummary($"test-course-{i:00}", TestUtil.FakeCourseTitle(i)));
        }

        void OnPageChanged(int page) { _currentPage = page; RebuildPage(); }

        void RebuildPage()
        {
            ClearChildren(contentRoot);
            if (emptyState != null) emptyState.SetActive(_allCourses.Count == 0);
            if (paginationBar != null)
            {
                int pages = Mathf.Max(1, (_allCourses.Count + pageSize - 1) / pageSize);
                paginationBar.SetPageCount(pages);
                paginationBar.gameObject.SetActive(pages > 1);
                paginationBar.SetCurrentPage(_currentPage, false);
            }

            int start = _currentPage * pageSize;
            int end = Mathf.Min(start + pageSize, _allCourses.Count);
            for (int i = start; i < end; i++)
            {
                var summary = _allCourses[i];
                var card = Instantiate(courseCardPrefab, contentRoot);
                card.Bind(summary.DisplayName, () => _ = OnEnterAsync(summary));
            }
        }

        async Task OnEnterAsync(CourseSummary summary)
        {
            if (_overlay == null || _router == null) return;
            _overlay.Show($"載入課程：{summary.DisplayName}…");
            try
            {
                Course course;
                if (summary.Name.StartsWith("test-course-"))
                    course = TestUtil.FakeCourse(summary);
                else
                    course = await _client.GetCourseAsync(summary.Name, CancellationToken.None);
                _router.ShowCourse(course);
            }
            catch (Exception ex)
            {
                Log.E($"GetCourse failed: {ex}");
                SetError($"無法載入課程「{summary.DisplayName}」。");
            }
            finally
            {
                _overlay.Hide();
            }
        }

        void SetError(string text)
        {
            if (errorLabel == null) return;
            errorLabel.text = text ?? string.Empty;
            errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        void OnDisable() => _cts?.Cancel();
    }
}
