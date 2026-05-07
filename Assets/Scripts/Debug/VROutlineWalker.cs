#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using Inspection.Net;
using UnityEngine;
using Inspection.UI;

namespace Inspection.Debugging
{
    /// <summary>
    /// Captures outline-panel-specific VR snapshots:
    ///   1. solar-system step 1 (panel closed)
    ///   2. solar-system with OutlinePanel open
    ///   3. After jumping to step 5 via outline
    /// Used to verify the §7.7 spec extension (clickable hierarchical index).
    /// </summary>
    public sealed class VROutlineWalker : MonoBehaviour
    {
        public string outDir = "qa";
        public float bootWait = 6.0f;
        public float perStepWait = 1.5f;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(bootWait);

            var bs = Object.FindFirstObjectByType<Inspection.App.AppBootstrapper>(FindObjectsInactive.Include);
            if (bs == null) { Debug.LogError("[OUTLINE] no AppBootstrapper"); yield break; }

            // Poll for Client property in case Awake's async chain hasn't completed.
            float waited = 0f;
            while (waited < 8f && bs.Client == null)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            if (bs.Client == null) { Debug.LogError($"[OUTLINE] Client null after {waited}s"); yield break; }
            var client = bs.Client;
            var router = bs.Router;
            if (router == null) { Debug.LogError("[OUTLINE] no router"); yield break; }
            Debug.Log($"[OUTLINE] client ready after {waited}s");

            var courseTask = client.GetCourseAsync("solar-system", CancellationToken.None);
            while (!courseTask.IsCompleted) yield return null;
            var course = courseTask.Result;
            router.ShowCourse(course);
            yield return new WaitForSeconds(perStepWait);

            var cv = Object.FindFirstObjectByType<CourseView>(FindObjectsInactive.Include);
            if (cv == null) { Debug.LogError("[OUTLINE] no CourseView"); yield break; }

            // 1. Step 1 (panel closed)
            yield return Snap("vr_outline_01_step1_closed");

            // 2. Toggle outline open
            cv.TestToggleOutline();
            yield return new WaitForSeconds(0.5f);
            yield return Snap("vr_outline_02_open");

            // 3. Jump to step 5 (望遠鏡入門) via outline + close panel
            cv.TestGoToStepOrder(5);
            yield return new WaitForSeconds(perStepWait);
            yield return Snap("vr_outline_03_jumped_to_5");

            // 4. Open again, jump to step 7 (月球延伸)
            cv.TestToggleOutline();
            yield return new WaitForSeconds(0.5f);
            yield return Snap("vr_outline_04_open_at_5");
            cv.TestGoToStepOrder(7);
            yield return new WaitForSeconds(perStepWait);
            yield return Snap("vr_outline_05_jumped_to_7");

            Debug.Log("[OUTLINE] DONE");
        }

        IEnumerator Snap(string name)
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outDir, name + ".png");
            yield return QASnapshot.CaptureCoroutine(path);
            Debug.Log($"[OUTLINE] snap {name} -> {path}");
        }
    }
}
#endif
