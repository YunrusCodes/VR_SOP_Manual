#if UNITY_EDITOR
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Inspection.UI;
namespace Inspection.Debugging
{
    /// <summary>
    /// Auto-walks every step of a chosen course, taking PNG snapshots after each step has had
    /// time to load its media. Spawn at runtime via:
    ///   var go = new GameObject("__Walker");
    ///   var w = go.AddComponent<QACourseWalker>();
    ///   w.cardIndex = 1; w.outDir = "qa"; w.prefix = "21";
    /// Logs `[QA] WALKER DONE name=<course>` when finished. The launcher should poll
    /// for that log line to know the walk is complete.
    /// </summary>
    public sealed class QACourseWalker : MonoBehaviour
    {
        public int cardIndex = 1;
        public string outDir = "qa";
        public string prefix = "20";
        public float bootWait = 3.0f;
        public float perStepWait = 2.0f;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(bootWait);

            var ml = Object.FindFirstObjectByType<ManualListView>(FindObjectsInactive.Include);
            if (ml == null) { Debug.LogError("[QA] no ManualListView"); yield break; }
            var contentProp = new UnityEditor.SerializedObject(ml).FindProperty("contentRoot");
            var content = contentProp.objectReferenceValue as Transform;
            if (content == null || content.childCount <= cardIndex) { Debug.LogError("[QA] cards not ready"); yield break; }

            string cardName = content.GetChild(cardIndex).GetComponentInChildren<TMP_Text>().text;
            Debug.Log($"[QA] entering card[{cardIndex}] '{cardName}'");
            content.GetChild(cardIndex).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(2.5f); // course load + bind

            var cv = Object.FindFirstObjectByType<CourseView>(FindObjectsInactive.Include);
            if (cv == null) { Debug.LogError("[QA] no CourseView"); yield break; }
            var cvSO = new UnityEditor.SerializedObject(cv);
            var nextBtn = cvSO.FindProperty("nextStepButton").objectReferenceValue as Button;
            var stepCounter = cvSO.FindProperty("stepCounter").objectReferenceValue as TMP_Text;
            var stepName = cvSO.FindProperty("stepName").objectReferenceValue as TMP_Text;
            var bread = cvSO.FindProperty("breadcrumb").objectReferenceValue as TMP_Text;

            // Determine total step count from "n / m"
            int total = 1;
            var parts = stepCounter.text.Split('/');
            if (parts.Length == 2) int.TryParse(parts[1].Trim(), out total);
            Debug.Log($"[QA] total steps: {total}");

            // Snapshot step 1
            yield return new WaitForSeconds(perStepWait);
            Snap(1, stepName.text, bread.text, stepCounter.text);

            // Walk forward (use TestNext direct call — onClick.Invoke didn't reliably advance)
            for (int i = 2; i <= total; i++)
            {
                cv.TestNext();
                yield return new WaitForSeconds(perStepWait);
                Snap(i, stepName.text, bread.text, stepCounter.text);
            }

            Debug.Log($"[QA] WALKER DONE name={cardName}");
        }

        void Snap(int idx, string name, string bread, string counter)
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outDir, $"{prefix}_step{idx:D2}.png");
            QASnapshot.CaptureMainCamera(path);
            Debug.Log($"[QA] step{idx}: bread='{bread}' counter='{counter}' name='{name}' -> {path}");
        }
    }
}
#endif
