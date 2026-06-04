using TMPro;
using UnityEngine;

namespace Inspection.Debugging
{
    /// <summary>
    /// Live readout of how far the canvas is from the user's head and right
    /// hand. Useful while tuning panel placement in MR — saves having to guess
    /// whether the current "looks too far" complaint is 50 cm or 80 cm.
    /// </summary>
    public sealed class DistanceHUD : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] Transform reference;   // usually the RootCanvas itself

        Transform _eye;
        Transform _rightHand;

        void Start()
        {
            if (reference == null) reference = transform.root;
            ResolveAnchors();
        }

        void ResolveAnchors()
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig != null)
            {
                _eye = FindRec(rig.transform, "CenterEyeAnchor");
                _rightHand = FindRec(rig.transform, "RightHandAnchor");
            }
            if (_eye == null) _eye = Camera.main != null ? Camera.main.transform : null;
        }

        static Transform FindRec(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindRec(c, name);
                if (r != null) return r;
            }
            return null;
        }

        void Update()
        {
            if (label == null) return;
            if (_eye == null || _rightHand == null) ResolveAnchors();

            float headD = _eye != null ? Vector3.Distance(_eye.position, reference.position) : -1f;
            float handD = _rightHand != null ? Vector3.Distance(_rightHand.position, reference.position) : -1f;

            label.text = string.Format(
                "頭→面板  <b>{0:F1}</b> cm\n手→面板  <b>{1:F1}</b> cm",
                headD * 100f,
                handD * 100f);
        }
    }
}
