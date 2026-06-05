using UnityEngine;

namespace Inspection.UI
{
    /// <summary>
    /// Places a World Space Canvas in front of the user at the moment their
    /// head pose becomes stable — this avoids snapping while the user is
    /// still putting on the headset or while the app is mid-transition.
    /// After the initial snap the panel stays put; only big head turns drag
    /// it back into view via lazy follow.
    /// </summary>
    public sealed class CanvasBillboard : MonoBehaviour
    {
        [SerializeField] float distance = 0.32f;
        [SerializeField] float verticalGazeOffset = -0.10f;
        [SerializeField] float horizontalGazeOffset = 0f;
        [SerializeField] float rotationDamping = 14f;
        [SerializeField] float positionDamping = 6f;
        [SerializeField] float recenterAngleDegrees = 30f;
        [SerializeField] float stableRequiredSeconds = 1.0f;

        Transform _eye;
        bool _firstFramePlaced;
        int _eyeWaitFrames;
        Quaternion _lastEyeRot = Quaternion.identity;
        float _stableTime;
        bool _recentering;

        void OnEnable() => Recenter();

        /// <summary>Reset state so the panel snaps to gaze again on the next stable frame.</summary>
        public void Recenter()
        {
            _firstFramePlaced = false;
            _eyeWaitFrames = 0;
            _stableTime = 0f;
            _recentering = false;
        }

        /// <summary>
        /// Mark the panel as already placed at its current transform, so the
        /// billboard skips the head-stability wait and only does lazy follow.
        /// Use when handing off position from another panel (PanelToggleGroup
        /// does this on tab switch) — otherwise the panel would briefly hold
        /// its stale last-disabled position before snapping to gaze.
        /// </summary>
        public void MarkPlaced()
        {
            _firstFramePlaced = true;
            _stableTime = stableRequiredSeconds;
            _eyeWaitFrames = 30;
            _recentering = false;
        }

        Transform FindCenterEye()
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig != null)
            {
                var found = FindByName(rig.transform, "CenterEyeAnchor");
                if (found != null) return found;
            }
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
            if (_eye == null)
            {
                _eye = FindCenterEye();
                if (_eye == null) return;
            }

            // OVR centre eye reports (0,0,0) until a head pose is available.
            if (!_firstFramePlaced && _eye.position == Vector3.zero && _eyeWaitFrames < 30)
            {
                _eyeWaitFrames++;
                _lastEyeRot = _eye.rotation;
                return;
            }

            if (!_firstFramePlaced)
            {
                // Snap only after the head has been roughly still for ~0.35 s,
                // so we don't anchor to whatever direction the user happened
                // to glance toward during loading.
                float angDelta = Quaternion.Angle(_lastEyeRot, _eye.rotation);
                _lastEyeRot = _eye.rotation;
                if (angDelta < 1.5f) _stableTime += Time.deltaTime;
                else _stableTime = 0f;
                if (_stableTime < stableRequiredSeconds) return;

                Vector3 desiredPos = _eye.position
                    + _eye.forward * distance
                    + Vector3.up * verticalGazeOffset
                    + _eye.right * horizontalGazeOffset;
                transform.position = desiredPos;
                Vector3 look = desiredPos - _eye.position;
                if (look.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.LookRotation(look);
                _firstFramePlaced = true;
                return;
            }

            // Lazy follow: when the user looks far enough from the panel
            // (e.g., turning to look at something across the room), slide it
            // back to the new gaze rather than leaving it stranded.
            Vector3 panelDir = transform.position - _eye.position;
            if (panelDir.sqrMagnitude > 1e-4f)
            {
                float angle = Vector3.Angle(_eye.forward, panelDir.normalized);
                if (angle > recenterAngleDegrees) _recentering = true;
            }

            if (_recentering)
            {
                Vector3 targetPos = _eye.position
                    + _eye.forward * distance
                    + Vector3.up * verticalGazeOffset
                    + _eye.right * horizontalGazeOffset;
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionDamping);
                if (Vector3.Distance(transform.position, targetPos) < 0.01f) _recentering = false;
            }

            Vector3 toEye = transform.position - _eye.position;
            if (toEye.sqrMagnitude > 1e-4f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toEye);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationDamping);
            }
        }
    }
}
