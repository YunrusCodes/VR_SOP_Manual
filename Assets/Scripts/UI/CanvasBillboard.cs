using UnityEngine;

namespace Inspection.UI
{
    /// <summary>
    /// Keeps a World Space Canvas docked in front of the user with damping.
    /// The canvas slides toward a fixed offset ahead of the centre-eye anchor
    /// and rotates to face it; small head movements don't yank the panel — it
    /// only catches up when the user looks far enough away that the panel
    /// drifts outside <see cref="recenterAngle"/> from gaze.
    ///
    /// Lives on the RootCanvas. The eye anchor reference is resolved at
    /// runtime so the script doesn't take a hard reference on Meta SDK types.
    /// </summary>
    public sealed class CanvasBillboard : MonoBehaviour
    {
        [SerializeField] float distance = 1.5f;
        [SerializeField] float heightOffset = -0.1f;
        [SerializeField] float positionDamping = 4f;
        [SerializeField] float rotationDamping = 6f;
        [SerializeField] float recenterAngle = 25f;

        Transform _eye;
        bool _recentering = true;

        void Awake()
        {
            _eye = FindCenterEye();
        }

        Transform FindCenterEye()
        {
            // Look for OVRCameraRig.centerEyeAnchor by walking the scene roots.
            var rig = GameObject.Find("OVRCameraRig");
            if (rig != null)
            {
                var found = FindByName(rig.transform, "CenterEyeAnchor");
                if (found != null) return found;
            }
            // Fallback: Camera.main.
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindByName(c, name);
                if (r != null) return r;
            }
            return null;
        }

        void LateUpdate()
        {
            if (_eye == null) { _eye = FindCenterEye(); if (_eye == null) return; }

            Vector3 forwardFlat = _eye.forward; forwardFlat.y = 0; if (forwardFlat.sqrMagnitude < 1e-4f) forwardFlat = Vector3.forward; forwardFlat.Normalize();
            Vector3 desiredPos = _eye.position + forwardFlat * distance + Vector3.up * heightOffset;

            Vector3 toCanvas = transform.position - _eye.position; toCanvas.y = 0; toCanvas.Normalize();
            float angle = Vector3.Angle(forwardFlat, toCanvas);
            if (angle > recenterAngle) _recentering = true;

            if (_recentering)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * positionDamping);
                if ((transform.position - desiredPos).sqrMagnitude < 0.0025f) _recentering = false;
            }

            Vector3 lookDir = transform.position - _eye.position; lookDir.y = 0; if (lookDir.sqrMagnitude < 1e-4f) lookDir = forwardFlat;
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationDamping);
        }
    }
}
