using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    /// <summary>
    /// Modal popup that lets the user override the API base URL at runtime. Saved value is
    /// persisted to PlayerPrefs so users don't have to rebuild the APK every time they change WiFi.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] TMP_InputField apiUrlInput;
        [SerializeField] Button saveButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text feedbackLabel;

        Func<string, System.Threading.Tasks.Task> _onSave;

        public void Init(Func<string, System.Threading.Tasks.Task> onSave)
        {
            _onSave = onSave;
            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(OnSaveClicked);
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(Hide);
            }
        }

        public void Show(string currentUrl)
        {
            if (apiUrlInput != null) apiUrlInput.text = currentUrl ?? string.Empty;
            if (feedbackLabel != null) feedbackLabel.text = string.Empty;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        async void OnSaveClicked()
        {
            string url = apiUrlInput != null ? apiUrlInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                if (feedbackLabel != null) feedbackLabel.text = "URL 不能空白";
                return;
            }
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
                apiUrlInput.text = url;
            }
            if (feedbackLabel != null) feedbackLabel.text = "套用中…";
            if (_onSave != null) await _onSave(url);
            Hide();
        }
    }
}
