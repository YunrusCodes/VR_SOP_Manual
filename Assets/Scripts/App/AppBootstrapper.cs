using System;
using Inspection.Domain;
using Inspection.Net;
using Inspection.UI;
using UnityEngine;

namespace Inspection.App
{
    public sealed class AppBootstrapper : MonoBehaviour
    {
        [SerializeField] AppSettings settings;
        [SerializeField] AppRouter router;
        [SerializeField] ManualListView manualList;
        [SerializeField] CourseView courseView;
        [SerializeField] LoadingOverlay overlay;

        async void Awake()
        {
            if (settings == null) { Debug.LogError("AppBootstrapper: settings is not assigned."); return; }
            if (router == null || manualList == null || courseView == null || overlay == null)
            {
                Debug.LogError("AppBootstrapper: one or more view references are not assigned.");
                return;
            }

            Log.Verbose = settings.VerboseLog;
            Log.V($"Boot: api={settings.ApiBaseUrl}, company={settings.Company}");

            ICourseClient client;
            try
            {
                client = new CourseClient(settings.ApiBaseUrl, settings.Company, new CsvParser(Log.W));
            }
            catch (Exception ex)
            {
                Log.E($"Failed to construct CourseClient: {ex.Message}");
                return;
            }

            manualList.Init(client, router, overlay);
            courseView.Init(client, router, overlay);

            router.ShowManualList();
            overlay.Show("載入課程清單…");
            try { await manualList.RefreshAsync(); }
            finally { overlay.Hide(); }
        }
    }
}
