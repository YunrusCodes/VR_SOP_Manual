using System;
using System.Threading.Tasks;
using Inspection.Domain;
using Inspection.Net;
using Inspection.UI;
using UnityEngine;

namespace Inspection.App
{
    public sealed class AppBootstrapper : MonoBehaviour
    {
        const string PrefKeyApiBaseUrl = "Inspection.ApiBaseUrl";

        [SerializeField] AppSettings settings;
        [SerializeField] AppRouter router;
        [SerializeField] ManualListView manualList;
        [SerializeField] CourseView courseView;
        [SerializeField] LoadingOverlay overlay;
        [SerializeField] SettingsPanel settingsPanel;
        [SerializeField] UnityEngine.UI.Button settingsButton;

        public ICourseClient Client { get; private set; }
        public AppRouter Router => router;

        public string EffectiveApiBaseUrl =>
            PlayerPrefs.GetString(PrefKeyApiBaseUrl, settings != null ? settings.ApiBaseUrl : "");

        async void Awake()
        {
            if (settings == null) { Debug.LogError("AppBootstrapper: settings is not assigned."); return; }
            if (router == null || manualList == null || courseView == null || overlay == null)
            {
                Debug.LogError("AppBootstrapper: one or more view references are not assigned.");
                return;
            }

            Log.Verbose = settings.VerboseLog;
            string apiUrl = EffectiveApiBaseUrl;
            Log.V($"Boot: api={apiUrl}, company={settings.Company} (override={apiUrl != settings.ApiBaseUrl})");

            if (!TryBuildClient(apiUrl, out var client)) return;
            Client = client;

            manualList.Init(client, router, overlay);
            courseView.Init(client, router, overlay);

            if (settingsPanel != null)
            {
                settingsPanel.Init(UpdateApiBaseUrlAsync);
                settingsPanel.Hide();
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(() =>
                {
                    if (settingsPanel != null) settingsPanel.Show(EffectiveApiBaseUrl);
                });
            }

            router.ShowManualList();
            overlay.Show("載入課程清單…");
            try { await manualList.RefreshAsync(); }
            finally { overlay.Hide(); }
        }

        /// <summary>
        /// Persist a new API base URL, rebuild the client, and re-init the views. Triggered by
        /// the settings panel; the manual list view should be refreshed afterwards by the caller.
        /// </summary>
        public async Task UpdateApiBaseUrlAsync(string newUrl)
        {
            if (string.IsNullOrWhiteSpace(newUrl)) return;
            newUrl = newUrl.Trim().TrimEnd('/');
            PlayerPrefs.SetString(PrefKeyApiBaseUrl, newUrl);
            PlayerPrefs.Save();
            Log.V($"ApiBaseUrl updated -> {newUrl}");

            if (!TryBuildClient(newUrl, out var client)) return;
            Client = client;
            manualList.Init(client, router, overlay);
            courseView.Init(client, router, overlay);

            overlay.Show("重新載入課程清單…");
            try { await manualList.RefreshAsync(); }
            finally { overlay.Hide(); }
        }

        bool TryBuildClient(string url, out ICourseClient client)
        {
            client = null;
            try
            {
                client = new CourseClient(url, settings.Company, new CsvParser(Log.W));
                return true;
            }
            catch (Exception ex)
            {
                Log.E($"Failed to construct CourseClient with url='{url}': {ex.Message}");
                return false;
            }
        }
    }
}
