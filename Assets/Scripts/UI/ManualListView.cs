using System;
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

        ICourseClient _client;
        AppRouter _router;
        LoadingOverlay _overlay;
        CancellationTokenSource _cts;

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
            ClearChildren(contentRoot);
            SetError(null);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                var courses = await _client.ListCoursesAsync(_cts.Token);
                if (emptyState != null) emptyState.SetActive(courses.Count == 0);

                foreach (var c in courses)
                {
                    var card = Instantiate(courseCardPrefab, contentRoot);
                    var summary = c;
                    card.Bind(summary.DisplayName, () => _ = OnEnterAsync(summary));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.E($"ListCourses failed: {ex}");
                SetError("無法載入課程清單，請確認後端是否啟動。");
            }
        }

        async Task OnEnterAsync(CourseSummary summary)
        {
            if (_overlay == null || _client == null || _router == null) return;
            _overlay.Show($"載入課程：{summary.DisplayName}…");
            try
            {
                var course = await _client.GetCourseAsync(summary.Name, CancellationToken.None);
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
