using UnityEngine;

namespace Inspection.UI
{
    /// <summary>
    /// Gates a head/hand-attached menu so it only appears when the user opens
    /// their left palm and tilts it upward (the classic "look at my palm to
    /// reveal a wrist menu" gesture). Hysteresis on the palm-up dot product
    /// avoids flicker at the edge of the threshold.
    /// <para/>
    /// Offsets are kept in centimetres (and as ints) so they can be driven by
    /// an in-headset tuner panel that nudges them one step at a time.
    /// X = sideways (perpendicular to the horizontal palm→user direction),
    /// Y = world up, Z = away from the user along the horizontal plane.
    /// </summary>
    public sealed class PalmMenuVisibility : MonoBehaviour
    {
        [SerializeField] OVRHand hand;
        [SerializeField] Transform handAnchor;
        [SerializeField] GameObject menu;
        [SerializeField] Transform faceTarget;

        // OVR LeftHandAnchor convention with palm open: palm normal ≈ -Y (back of hand is +Y).
        [SerializeField] Vector3 palmNormalLocal = new Vector3(0f, -1f, 0f);

        public int sideCm = -5;
        public int upCm = 13;
        public int backCm = 8;

        [Range(0f, 1f)] [SerializeField] float showThreshold = 0.55f;
        [Range(0f, 1f)] [SerializeField] float hideThreshold = 0.35f;

        bool _showing;

        void OnEnable()
        {
            _showing = false;
            if (menu != null) menu.SetActive(false);
        }

        void LateUpdate()
        {
            bool show = false;
            if (hand != null && handAnchor != null && hand.IsTracked)
            {
                Vector3 palmWorld = handAnchor.TransformDirection(palmNormalLocal.normalized);
                float dot = Vector3.Dot(palmWorld, Vector3.up);
                show = _showing ? (dot > hideThreshold) : (dot > showThreshold);
            }

            if (_showing != show)
            {
                _showing = show;
                if (menu != null) menu.SetActive(show);
            }

            if (show && menu != null && handAnchor != null && faceTarget != null)
            {
                Vector3 palmAnchor = handAnchor.position;
                Vector3 toCam = faceTarget.position - palmAnchor;
                Vector3 toCamFlat = new Vector3(toCam.x, 0f, toCam.z);
                if (toCamFlat.sqrMagnitude > 1e-6f)
                {
                    Vector3 toCamFlatN = toCamFlat.normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, toCamFlatN).normalized;
                    Vector3 pos = palmAnchor
                        + right        * (sideCm * 0.01f)
                        + Vector3.up   * (upCm   * 0.01f)
                        + (-toCamFlatN) * (backCm * 0.01f);

                    Vector3 toCamFromMenu = faceTarget.position - pos;
                    Quaternion rot = Quaternion.LookRotation(toCamFromMenu, Vector3.up);
                    menu.transform.SetPositionAndRotation(pos, rot);
                }
            }
        }
    }
}
