using UnityEngine;

namespace Inspection.UI
{
    /// <summary>
    /// Continuously rotates the GameObject this is attached to — used as a
    /// pure-Z-axis spinner for the loading overlay so users get feedback that
    /// the app didn't just freeze while it waits on the backend.
    /// </summary>
    public sealed class LoadingSpinner : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 240f;

        void Update()
        {
            transform.Rotate(0, 0, -degreesPerSecond * Time.deltaTime);
        }
    }
}
