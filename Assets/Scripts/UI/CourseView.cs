using System;
using System.Threading;
using System.Threading.Tasks;
using Inspection.App;
using Inspection.Domain;
using Inspection.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Inspection.UI
{
    public sealed class CourseView : MonoBehaviour
    {
        [SerializeField] TMP_Text breadcrumb;
        [SerializeField] TMP_Text stepCounter;
        [SerializeField] TMP_Text stepName;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_Text nextIndication;

        [SerializeField] GameObject mediaPanel;
        [SerializeField] RectTransform leftColumn;
        [SerializeField] RawImage imageView;
        [SerializeField] RawImage videoView;
        [SerializeField] VideoPlayer videoPlayer;

        [SerializeField] Transform exceptionLayer;
        [SerializeField] ExceptionButton exceptionButtonPrefab;

        [SerializeField] Button prevStepButton;
        [SerializeField] Button nextStepButton;
        [SerializeField] Button backToListButton;

        ICourseClient _client;
        AppRouter _router;
        LoadingOverlay _overlay;
        Course _course;
        int _currentIndex;
        Texture2D _currentTexture;
        CancellationTokenSource _mediaCts;

        public void Init(ICourseClient client, AppRouter router, LoadingOverlay overlay)
        {
            _client = client;
            _router = router;
            _overlay = overlay;

            if (prevStepButton != null) prevStepButton.onClick.AddListener(OnPrev);
            if (nextStepButton != null) nextStepButton.onClick.AddListener(OnNext);
            if (backToListButton != null) backToListButton.onClick.AddListener(OnBackToList);
        }

        public void Bind(Course course)
        {
            _course = course;
            _currentIndex = 0;
            if (course == null || course.Steps == null || course.Steps.Count == 0)
            {
                Log.W("CourseView.Bind called with empty course");
                return;
            }
            ShowStepAt(0);
        }

        void ShowStepAt(int index)
        {
            if (_course == null || _course.Steps == null || _course.Steps.Count == 0) return;
            index = Mathf.Clamp(index, 0, _course.Steps.Count - 1);
            _currentIndex = index;
            var step = _course.Steps[index];

            if (breadcrumb != null)
                breadcrumb.text = string.IsNullOrEmpty(step.SubTitle)
                    ? $"{step.MainTitle}　／　{step.Name}"
                    : $"{step.MainTitle}　／　{step.SubTitle}　／　{step.Name}";

            if (stepCounter != null) stepCounter.text = $"{index + 1} / {_course.Steps.Count}";
            if (stepName != null) stepName.text = step.Name;
            if (description != null) description.text = step.Description ?? string.Empty;

            if (nextIndication != null)
            {
                nextIndication.text = step.NextStepIndication ?? string.Empty;
                nextIndication.gameObject.SetActive(!string.IsNullOrEmpty(step.NextStepIndication));
            }

            UpdateMedia(step.Media);
            UpdateExceptions(step);

            if (prevStepButton != null) prevStepButton.interactable = index > 0;
            if (nextStepButton != null) nextStepButton.interactable = index < _course.Steps.Count - 1;
        }

        void UpdateMedia(Media media)
        {
            // Cancel any in-flight image load.
            _mediaCts?.Cancel();
            _mediaCts = new CancellationTokenSource();

            // Stop any prior video.
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();

            // Drop prior texture.
            if (_currentTexture != null)
            {
                if (imageView != null && imageView.texture == _currentTexture) imageView.texture = null;
                Destroy(_currentTexture);
                _currentTexture = null;
            }

            // When a step has no media, expand the text column to fill the panel; otherwise
            // restore the 60/40 split. This avoids the empty-right-column dead-zone we saw in
            // VR walkthroughs of solar step 5 (望遠鏡入門) and step 7 (月球延伸).
            if (leftColumn != null)
            {
                var max = leftColumn.anchorMax;
                max.x = media is Media.None ? 1f : 0.6f;
                leftColumn.anchorMax = max;
            }

            switch (media)
            {
                case Media.None:
                    if (mediaPanel != null) mediaPanel.SetActive(false);
                    break;

                case Media.Image img:
                    if (mediaPanel != null) mediaPanel.SetActive(true);
                    if (imageView != null) imageView.gameObject.SetActive(true);
                    if (videoView != null) videoView.gameObject.SetActive(false);
                    _ = LoadImageAsync(img.FileName, _mediaCts.Token);
                    break;

                case Media.Video vid:
                    if (mediaPanel != null) mediaPanel.SetActive(true);
                    if (imageView != null) imageView.gameObject.SetActive(false);
                    if (videoView != null) videoView.gameObject.SetActive(true);
                    if (videoPlayer != null && _client != null)
                    {
                        videoPlayer.source = VideoSource.Url;
                        videoPlayer.url = _client.GetVideoUrl(_course.Name, vid.FileName);
                        videoPlayer.Play();
                    }
                    break;
            }
        }

        async Task LoadImageAsync(string fileName, CancellationToken ct)
        {
            if (_client == null || imageView == null) return;
            try
            {
                var url = _client.GetImageUrl(_course.Name, fileName);
                var tex = await ImageLoader.LoadAsync(url, ct);
                if (ct.IsCancellationRequested) { Destroy(tex); return; }
                _currentTexture = tex;
                imageView.texture = tex;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.E($"LoadImage failed: {ex}");
            }
        }

        void UpdateExceptions(Step step)
        {
            if (exceptionLayer == null || exceptionButtonPrefab == null) return;
            for (int i = exceptionLayer.childCount - 1; i >= 0; i--)
                Destroy(exceptionLayer.GetChild(i).gameObject);

            if (step.Exceptions == null) return;
            foreach (var ex in step.Exceptions)
            {
                var btn = Instantiate(exceptionButtonPrefab, exceptionLayer);
                var captured = ex;
                btn.Bind(captured.Label, () => OnExceptionPressed(captured));
            }
        }

        void OnExceptionPressed(ExceptionOption opt)
        {
            switch (opt.Action)
            {
                case ExceptionAction.GoToStep gs:
                    GoToStepOrder(gs.Step);
                    break;
                case ExceptionAction.ShowMessage sm:
                    if (_overlay != null) _overlay.ShowMessage(sm.Text ?? string.Empty);
                    break;
            }
        }

        void GoToStepOrder(int order)
        {
            if (_course == null) return;
            for (int i = 0; i < _course.Steps.Count; i++)
            {
                if (_course.Steps[i].Order == order) { ShowStepAt(i); return; }
            }
            Log.W($"Exception goto: step order {order} not found.");
        }

        void OnPrev() => ShowStepAt(_currentIndex - 1);
        void OnNext() => ShowStepAt(_currentIndex + 1);

        // Public helpers for editor QA automation. Mirrors what the buttons do.
        public void TestNext() => OnNext();
        public void TestPrev() => OnPrev();
        public void TestBackToList() => OnBackToList();
        public void TestGoToStepOrder(int order) => GoToStepOrder(order);
        public void TestPressException(int index)
        {
            if (_course == null) return;
            var step = _course.Steps[_currentIndex];
            if (index < 0 || index >= step.Exceptions.Count) return;
            OnExceptionPressed(step.Exceptions[index]);
        }
        public int TestCurrentIndex => _currentIndex;

        void OnBackToList()
        {
            CleanupMedia();
            if (_router != null) _router.ShowManualList();
        }

        void CleanupMedia()
        {
            _mediaCts?.Cancel();
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            if (_currentTexture != null)
            {
                if (imageView != null && imageView.texture == _currentTexture) imageView.texture = null;
                Destroy(_currentTexture);
                _currentTexture = null;
            }
        }

        void OnDisable() => CleanupMedia();
    }
}
