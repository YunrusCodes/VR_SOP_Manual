using Inspection.Domain;
using UnityEngine;

namespace Inspection.UI
{
    public sealed class AppRouter : MonoBehaviour
    {
        [SerializeField] ManualListView manualList;
        [SerializeField] CourseView courseView;
        [SerializeField] OutlinePanel outlinePanel;

        public void ShowManualList()
        {
            if (courseView != null) courseView.gameObject.SetActive(false);
            if (outlinePanel != null) outlinePanel.gameObject.SetActive(false);
            if (manualList != null) manualList.gameObject.SetActive(true);
        }

        public void ShowCourse(Course course)
        {
            if (manualList != null) manualList.gameObject.SetActive(false);

            if (outlinePanel != null && course != null)
            {
                int initialOrder = course.Steps != null && course.Steps.Count > 0 ? course.Steps[0].Order : 0;
                outlinePanel.gameObject.SetActive(true);
                outlinePanel.Bind(course, initialOrder);
            }

            if (courseView != null)
            {
                courseView.Bind(course);
                courseView.gameObject.SetActive(true);
            }
        }
    }
}
