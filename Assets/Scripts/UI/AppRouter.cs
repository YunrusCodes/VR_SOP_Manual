using Inspection.Domain;
using UnityEngine;

namespace Inspection.UI
{
    public sealed class AppRouter : MonoBehaviour
    {
        [SerializeField] ManualListView manualList;
        [SerializeField] CourseView courseView;

        public void ShowManualList()
        {
            if (courseView != null) courseView.gameObject.SetActive(false);
            if (manualList != null) manualList.gameObject.SetActive(true);
        }

        public void ShowCourse(Course course)
        {
            if (manualList != null) manualList.gameObject.SetActive(false);
            if (courseView != null)
            {
                courseView.Bind(course);
                courseView.gameObject.SetActive(true);
            }
        }
    }
}
