// Debug-only runtime monitor that logs simulator-driven controller hover/trigger
// changes plus button click events to the Unity Console with [MON] / [CLICK] prefixes.
//
// Spawned at runtime by an editor command (`new GameObject().AddComponent<LiveMonitor>()`).
// Lives only during play mode; safe to ship since nothing references it.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Inspection.Debugging
{
    public sealed class LiveMonitor : MonoBehaviour
    {
        NearFarInteractor _rightNf, _leftNf;
        InputDevice _rightDev, _leftDev;
        string _rHover = "(none)", _lHover = "(none)";
        float _rTrig = -1, _lTrig = -1;
        bool _rMouseLast, _lMouseLast;

        static bool HasUsage(InputDevice d, string usage)
        {
            foreach (var u in d.usages)
                if (u == usage) return true;
            return false;
        }

        void Awake()
        {
            var rc = GameObject.Find("Right Controller");
            var lc = GameObject.Find("Left Controller");
            if (rc != null) _rightNf = rc.GetComponentInChildren<NearFarInteractor>(true);
            if (lc != null) _leftNf = lc.GetComponentInChildren<NearFarInteractor>(true);
            foreach (var d in InputSystem.devices)
            {
                if (HasUsage(d, "RightHand")) _rightDev = d;
                if (HasUsage(d, "LeftHand")) _leftDev = d;
            }
            Debug.Log("[MON] init: rightNf=" + (_rightNf != null) +
                      " leftNf=" + (_leftNf != null) +
                      " rDev=" + (_rightDev != null ? _rightDev.name : "?") +
                      " lDev=" + (_leftDev != null ? _leftDev.name : "?"));
        }

        TrackedDeviceGraphicRaycaster _raycaster;
        readonly List<RaycastResult> _rrBuf = new List<RaycastResult>();

        void Update()
        {
            if (_raycaster == null)
            {
                var c = GameObject.Find("RootCanvas");
                if (c != null) _raycaster = c.GetComponent<TrackedDeviceGraphicRaycaster>();
            }

            // UI hover via TrackedDeviceGraphicRaycaster from right controller forward
            if (_rightNf != null && _raycaster != null && EventSystem.current != null)
            {
                var rcXform = _rightNf.transform;
                var ped = new TrackedDeviceEventData(EventSystem.current);
                ped.position = new Vector2(Screen.width / 2f, Screen.height / 2f);
                ped.layerMask = ~0;
                ped.rayPoints = new List<Vector3> { rcXform.position, rcXform.position + rcXform.forward * 5f };
                _rrBuf.Clear();
                _raycaster.Raycast(ped, _rrBuf);
                string h = _rrBuf.Count > 0 ? _rrBuf[0].gameObject.name : "(none)";
                if (h != _rHover) { Debug.Log("[MON] RIGHT ray-hit: " + _rHover + " -> " + h); _rHover = h; }
            }

            float rt = ReadTrig(_rightDev);
            if (Mathf.Abs(rt - _rTrig) > 0.05f) { Debug.Log("[MON] RIGHT trigger: " + _rTrig.ToString("F2") + " -> " + rt.ToString("F2")); _rTrig = rt; }
            float lt = ReadTrig(_leftDev);
            if (Mathf.Abs(lt - _lTrig) > 0.05f) { Debug.Log("[MON] LEFT  trigger: " + _lTrig.ToString("F2") + " -> " + lt.ToString("F2")); _lTrig = lt; }

            var m = Mouse.current;
            if (m != null)
            {
                bool down = m.leftButton.isPressed;
                if (down != _rMouseLast) { Debug.Log("[MON] MOUSE-LEFT " + (down ? "down" : "up")); _rMouseLast = down; }
            }
        }

        static string Name(IXRHoverInteractable x)
        {
            var mb = x as MonoBehaviour;
            if (mb == null) return x != null ? x.ToString() : "?";
            var t = mb.transform;
            return (t.parent != null ? t.parent.name + "/" : "") + t.name;
        }

        static float ReadTrig(InputDevice d)
        {
            if (d == null) return 0;
            var c = d.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
            return c != null ? c.ReadValue() : 0;
        }
    }

    public sealed class ClickHookInstaller : MonoBehaviour
    {
        void Update()
        {
            var btns = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in btns)
            {
                if (b.GetComponent<Marker>() != null) continue;
                b.gameObject.AddComponent<Marker>();
                var name = (b.transform.parent != null ? b.transform.parent.name + "/" : "") + b.name;
                b.onClick.AddListener(() => Debug.Log("[CLICK] " + name));
            }
        }
        sealed class Marker : MonoBehaviour { }
    }
}
