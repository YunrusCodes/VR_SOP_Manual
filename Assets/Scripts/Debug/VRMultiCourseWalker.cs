#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Inspection.Domain;
using Inspection.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Inspection.UI;
namespace Inspection.Debugging
{
    /// <summary>
    /// Walks every course in the manual list, snapshotting each step in VR (WorldSpace) view.
    /// Bypasses the click handler and calls AppRouter.ShowCourse directly via reflection so the
    /// walker isn't sensitive to UnityEvent listener wiring quirks.
    /// </summary>
    public sealed class VRMultiCourseWalker : MonoBehaviour
    {
        public string outDir = "qa";
        public float bootWait = 3.5f;
        public float perStepWait = 2.0f;
        public float backToListWait = 1.0f;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(bootWait);

            var ml = Object.FindFirstObjectByType<ManualListView>(FindObjectsInactive.Include);
            if (ml == null) { Debug.LogError("[VRWALK] no ManualListView"); yield break; }
            var router = Object.FindFirstObjectByType<AppRouter>(FindObjectsInactive.Include);
            if (router == null) { Debug.LogError("[VRWALK] no AppRouter"); yield break; }

            // Pull ManualListView._client via reflection — already wired by AppBootstrapper
            var clientField = typeof(ManualListView).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
            var client = clientField?.GetValue(ml) as ICourseClient;
            if (client == null) { Debug.LogError("[VRWALK] no _client on ManualListView (bootstrap not complete?)"); yield break; }

            // Discover course list straight from API (skips dependency on rendered card titles)
            var listTask = client.ListCoursesAsync(CancellationToken.None);
            while (!listTask.IsCompleted) yield return null;
            if (listTask.IsFaulted) { Debug.LogError($"[VRWALK] list failed: {listTask.Exception}"); yield break; }
            var courses = listTask.Result;
            Debug.Log($"[VRWALK] discovered {courses.Count} courses");

            yield return new WaitForSeconds(0.5f);
            yield return Snap("vr_manual_list", 0, "ManualListView");

            for (int i = 0; i < courses.Count; i++)
            {
                var summary = courses[i];
                string slug = SlugFor(summary.Name, summary.DisplayName);
                Debug.Log($"[VRWALK] loading course '{summary.Name}' (display='{summary.DisplayName}') as slug '{slug}'");

                var courseTask = client.GetCourseAsync(summary.Name, CancellationToken.None);
                while (!courseTask.IsCompleted) yield return null;
                if (courseTask.IsFaulted)
                {
                    Debug.LogError($"[VRWALK] get course failed: {courseTask.Exception}");
                    continue;
                }
                var course = courseTask.Result;
                router.ShowCourse(course);
                yield return new WaitForSeconds(0.5f);

                var cv = Object.FindFirstObjectByType<CourseView>(FindObjectsInactive.Include);
                if (cv == null) { Debug.LogError("[VRWALK] no CourseView after ShowCourse"); yield break; }

                int total = course.Steps?.Count ?? 0;
                Debug.Log($"[VRWALK] course '{slug}' total steps={total}");

                yield return new WaitForSeconds(perStepWait);
                yield return Snap("vr_" + slug, 1, "step1");

                for (int s = 2; s <= total; s++)
                {
                    cv.TestNext();
                    yield return new WaitForSeconds(perStepWait);
                    yield return Snap("vr_" + slug, s, $"step{s}");
                }

                cv.TestBackToList();
                yield return new WaitForSeconds(backToListWait);
            }

            yield return new WaitForSeconds(0.5f);
            yield return Snap("vr_manual_list_final", 0, "ManualListView final");
            Debug.Log("[VRWALK] DONE");
        }

        IEnumerator Snap(string slug, int idx, string note)
        {
            string fn = idx > 0 ? $"{slug}_step{idx:D2}.png" : $"{slug}.png";
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outDir, fn);
            yield return QASnapshot.CaptureCoroutine(path);
            Debug.Log($"[VRWALK] snap '{slug}' #{idx} ({note}) -> {path}");
        }

        static string SlugFor(string courseName, string display)
        {
            // Prefer course folder name, since that aligns with API paths and CSV folder layout
            if (!string.IsNullOrEmpty(courseName))
            {
                if (courseName.Contains("solar")) return "solar";
                if (courseName.Contains("volcano")) return "volcano";
                if (courseName.Contains("mitosis")) return "mitosis";
            }
            if (!string.IsNullOrEmpty(display))
            {
                if (display.Contains("太陽")) return "solar";
                if (display.Contains("火山")) return "volcano";
                if (display.Contains("細胞") || display.Contains("分裂")) return "mitosis";
            }
            return "course_" + System.Math.Abs((courseName ?? display ?? "x").GetHashCode());
        }
    }
}
#endif
