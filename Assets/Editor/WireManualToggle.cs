using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Inspection.UI;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UIImage = UnityEngine.UI.Image;

namespace Inspection.EditorTools
{
    /// <summary>
    /// Rebuilds the palm-mounted manual toggle from scratch. Hierarchy:
    ///   LeftHandAnchor/
    ///     PalmMenuRoot          (PalmMenuVisibility)
    ///       ToggleCanvas        (Canvas + GraphicRaycaster + PointableCanvas)
    ///         UISurface         (Plane/Clipped surfaces, Ray + Poke interactables)
    ///         ToggleButton      (Image + Button + PressFireOnDown + ManualToggle)
    ///           Label           (TMP "X")
    /// </summary>
    public static class WireManualToggle
    {
        [MenuItem("Tools/Inspection/Wire Manual Toggle")]
        public static void Wire()
        {
            var rootCanvasGo = GameObject.Find("RootCanvas");
            if (rootCanvasGo == null) { Debug.LogError("RootCanvas missing"); return; }

            var leftHandAnchor = FindLeftHandAnchor();
            if (leftHandAnchor == null) { Debug.LogError("LeftHandAnchor not found under OVRCameraRig/TrackingSpace"); return; }

            // Strip prior toggle hierarchies (anywhere in the scene).
            foreach (var stale in Object.FindObjectsByType<PalmMenuVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            foreach (var stale in Object.FindObjectsByType<ManualToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Walk up to the ToggleCanvas root and nuke the whole hierarchy.
                var t = stale.transform;
                while (t.parent != null && t.parent.name != "PalmMenuRoot" && t.name != "ToggleCanvas") t = t.parent;
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            // Strip any prior PalmOffsetTuner (could be under RootCanvas from older builds
            // or under CenterEyeAnchor from the newer detached layout).
            foreach (var stale in Object.FindObjectsByType<PalmOffsetTuner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            for (int i = leftHandAnchor.childCount - 1; i >= 0; i--)
            {
                var c = leftHandAnchor.GetChild(i);
                if (c.name == "PalmMenuRoot" || c.name == "ToggleCanvas") Object.DestroyImmediate(c.gameObject);
            }
            // Strip any prior TunerCanvas / TestCanvas / PanelAnchor wherever they sit —
            // Resources.FindObjectsOfTypeAll includes inactive scene objects (GameObject.Find
            // skips those, hence prior versions accumulated hidden tuners after each re-wire).
            DestroySceneObjectsByName("TunerCanvas");
            DestroySceneObjectsByName("TestCanvas");
            DestroySceneObjectsByName("PanelAnchor");

            // Drop the layered stack: each panel billboards itself, and the palm
            // buttons drive a PanelToggleGroup so only one panel is open at a time.
            var staleStacked = rootCanvasGo.GetComponent<StackedPanel>();
            if (staleStacked != null) Object.DestroyImmediate(staleStacked);
            EnsureManualBillboard(rootCanvasGo);

            // PalmMenuRoot stays active so its Update keeps firing while the menu itself is hidden.
            var palmRoot = new GameObject("PalmMenuRoot", typeof(RectTransform));
            palmRoot.transform.SetParent(leftHandAnchor, false);

            // ToggleCanvas: world-space canvas floating above the palm.
            var tc = new GameObject("ToggleCanvas", typeof(RectTransform));
            tc.transform.SetParent(palmRoot.transform, false);
            var rt = tc.GetComponent<RectTransform>();
            // Static fallback for editor preview only. At runtime, PalmMenuVisibility
            // drives world-space position and rotation directly so it can apply the
            // height-above-palm and push-back-from-user offsets in metres.
            rt.localPosition = new Vector3(0f, -0.06f, 0.0f);
            rt.localEulerAngles = Vector3.zero;
            rt.localScale = new Vector3(0.0005f, 0.0005f, 0.0005f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Three 40px (=2cm) discs with 8px gaps → 136px wide.
            rt.sizeDelta = new Vector2(136, 40);
            var canvas = tc.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            tc.AddComponent<GraphicRaycaster>();
            var pointableCanvas = tc.AddComponent<PointableCanvas>();
            SetField(pointableCanvas, "_canvas", canvas);

            // UISurface — Meta interaction chain identical to the main canvas.
            var us = new GameObject("UISurface", typeof(RectTransform));
            us.transform.SetParent(tc.transform, false);
            var srt = us.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            var planeSurface = us.AddComponent<PlaneSurface>();
            SetField(planeSurface, "_doubleSided", true);
            var rayInteractable = us.AddComponent<RayInteractable>();
            var boundsClipper = us.AddComponent<BoundsClipper>();
            SetField(boundsClipper, "_position", Vector3.zero);
            SetField(boundsClipper, "_size", new Vector3(136, 40, 0.01f));
            var clipperDriver = us.AddComponent<RectTransformBoundsClipperDriver>();
            SetField(clipperDriver, "_boundsClipper", boundsClipper);
            var clippedSurface = us.AddComponent<ClippedPlaneSurface>();
            SetField(clippedSurface, "_planeSurface", planeSurface);
            SetClippersList(clippedSurface, "_clippers", boundsClipper);
            var pokeInteractable = us.AddComponent<PokeInteractable>();
            SetField(rayInteractable, "_surface", clippedSurface);
            SetField(rayInteractable, "_pointableElement", pointableCanvas);
            SetField(pokeInteractable, "_surfacePatch", clippedSurface);
            SetField(pokeInteractable, "_pointableElement", pointableCanvas);
            SetField(pokeInteractable, "_enterHoverNormal", 0.15f);
            SetField(pokeInteractable, "_exitHoverNormal", 0.20f);
            SetField(pokeInteractable, "_cancelSelectTangent", 0.10f);

            // Three side-by-side discs: manual / tuner / test. Centers at -48, 0, +48
            // give 8px gaps between adjacent 40px-wide discs.
            var knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var btnLeft   = SpawnPalmDisc(tc.transform, "ToggleButton", new Vector2(-48, 0), knobSprite, out var labelLeft);
            var btnMiddle = SpawnPalmDisc(tc.transform, "TunerButton",  new Vector2(  0, 0), knobSprite, out var labelMiddle);
            var btnRight  = SpawnPalmDisc(tc.transform, "TestButton",   new Vector2( 48, 0), knobSprite, out var labelRight);

            // PalmMenuVisibility on PalmMenuRoot — gates the disc canvas on palm-up.
            var leftHand = leftHandAnchor.GetComponent<OVRHand>();
            var visibility = palmRoot.AddComponent<PalmMenuVisibility>();
            SetField(visibility, "hand", leftHand);
            SetField(visibility, "handAnchor", leftHandAnchor);
            SetField(visibility, "menu", tc);
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            if (rig != null && rig.centerEyeAnchor != null)
                SetField(visibility, "faceTarget", rig.centerEyeAnchor);
            tc.SetActive(false);

            // Build the two extra canvases — each runs its own CanvasBillboard so a
            // freshly-enabled panel re-centres in front of the user.
            var tunerPanelGo = BuildTunerCanvas(visibility);
            if (tunerPanelGo != null) tunerPanelGo.SetActive(false);
            var testPanelGo = BuildTestCanvas();
            if (testPanelGo != null) testPanelGo.SetActive(false);

            // Group: exactly one of manual / tuner / test is open at a time. The
            // group component lives on the palm root so a single instance manages
            // all three discs.
            var existingGroup = palmRoot.GetComponent<PanelToggleGroup>();
            if (existingGroup != null) Object.DestroyImmediate(existingGroup);
            var group = palmRoot.AddComponent<PanelToggleGroup>();
            var entries = new System.Collections.Generic.List<PanelToggleGroup.Entry>
            {
                new PanelToggleGroup.Entry { panel = rootCanvasGo, iconLabel = labelLeft,   visibleIcon = "✕", hiddenIcon = "≡" },
                new PanelToggleGroup.Entry { panel = tunerPanelGo, iconLabel = labelMiddle, visibleIcon = "✕", hiddenIcon = "T" },
                new PanelToggleGroup.Entry { panel = testPanelGo,  iconLabel = labelRight,  visibleIcon = "✕", hiddenIcon = "?" },
            };
            SetField(group, "entries", entries);

            for (int i = btnLeft.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btnLeft.onClick, i);
            for (int i = btnMiddle.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btnMiddle.onClick, i);
            for (int i = btnRight.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btnRight.onClick, i);
            UnityEventTools.AddIntPersistentListener(btnLeft.onClick,   group.Toggle, 0);
            UnityEventTools.AddIntPersistentListener(btnMiddle.onClick, group.Toggle, 1);
            UnityEventTools.AddIntPersistentListener(btnRight.onClick,  group.Toggle, 2);

            EditorUtility.SetDirty(rootCanvasGo);
            EditorUtility.SetDirty(palmRoot);
            EditorUtility.SetDirty(tc);
            EditorUtility.SetDirty(us);
            EditorUtility.SetDirty(btnLeft.gameObject);
            EditorUtility.SetDirty(btnMiddle.gameObject);
            EditorUtility.SetDirty(btnRight.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[WireManualToggle] Palm-mounted toggle wired under {GetPath(leftHandAnchor)}");
        }

        static Button SpawnPalmDisc(Transform parent, string name, Vector2 anchoredPos, Sprite knobSprite, out TMP_Text labelTmp)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UIImage), typeof(Button));
            go.transform.SetParent(parent, false);
            var brt = go.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(40, 40);
            brt.anchoredPosition = anchoredPos;

            var img = go.GetComponent<UIImage>();
            if (knobSprite != null) img.sprite = knobSprite;
            img.color = new Color(1f, 1f, 1f, 0.9f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = new Color(1f, 1f, 1f, 1f);
            cb.highlightedColor = new Color(0.7f, 0.85f, 1f, 1f);
            cb.pressedColor     = new Color(0.45f, 0.75f, 1f, 1f);
            cb.selectedColor    = new Color(0.7f, 0.85f, 1f, 1f);
            cb.fadeDuration     = 0.05f;
            btn.colors = cb;

            go.AddComponent<PressFireOnDown>();

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22;
            tmp.color = new Color(0.10f, 0.12f, 0.18f, 1f);
            tmp.raycastTarget = false;
            labelTmp = tmp;

            return btn;
        }

        static GameObject BuildTunerCanvas(PalmMenuVisibility visibility)
        {
            // Root-level world-space canvas, manual-sized. Position+rotation are
            // overwritten each frame by StackedPanelManager.
            var tcGo = new GameObject("TunerCanvas", typeof(RectTransform));
            var trt = tcGo.GetComponent<RectTransform>();
            trt.localScale = new Vector3(0.00018f, 0.00018f, 0.00018f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(1920, 1080);

            var canvas = tcGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 150;
            tcGo.AddComponent<GraphicRaycaster>();
            var pointable = tcGo.AddComponent<PointableCanvas>();
            SetField(pointable, "_canvas", canvas);

            // Own billboard so it re-centres in front of the user when toggled on.
            AttachStandardBillboard(tcGo);

            // UISurface — interaction chain identical to RootCanvas's surface.
            var us = new GameObject("UISurface", typeof(RectTransform));
            us.transform.SetParent(tcGo.transform, false);
            var srt = us.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            var planeSurface = us.AddComponent<PlaneSurface>();
            SetField(planeSurface, "_doubleSided", true);
            var rayInteractable = us.AddComponent<RayInteractable>();
            var boundsClipper = us.AddComponent<BoundsClipper>();
            SetField(boundsClipper, "_position", Vector3.zero);
            SetField(boundsClipper, "_size", new Vector3(1920, 1080, 0.01f));
            var clipperDriver = us.AddComponent<RectTransformBoundsClipperDriver>();
            SetField(clipperDriver, "_boundsClipper", boundsClipper);
            var clippedSurface = us.AddComponent<ClippedPlaneSurface>();
            SetField(clippedSurface, "_planeSurface", planeSurface);
            SetClippersList(clippedSurface, "_clippers", boundsClipper);
            var pokeInteractable = us.AddComponent<PokeInteractable>();
            SetField(rayInteractable, "_surface", clippedSurface);
            SetField(rayInteractable, "_pointableElement", pointable);
            SetField(pokeInteractable, "_surfacePatch", clippedSurface);
            SetField(pokeInteractable, "_pointableElement", pointable);
            SetField(pokeInteractable, "_enterHoverNormal", 0.15f);
            SetField(pokeInteractable, "_exitHoverNormal", 0.20f);
            SetField(pokeInteractable, "_cancelSelectTangent", 0.10f);

            // Panel content fills the canvas behind the UISurface.
            var panel = new GameObject("PalmOffsetTuner",
                typeof(RectTransform), typeof(UIImage), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(tcGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.GetComponent<UIImage>().color = new Color(0.05f, 0.07f, 0.10f, 0.92f);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(80, 80, 80, 80);
            vlg.spacing = 40;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            SpawnHeader(panel.transform, "Palm Offset (cm)");
            var xVal = SpawnTunerRow(panel.transform, "X", out var xMinus, out var xPlus);
            var yVal = SpawnTunerRow(panel.transform, "Y", out var yMinus, out var yPlus);
            var zVal = SpawnTunerRow(panel.transform, "Z", out var zMinus, out var zPlus);

            var tuner = panel.AddComponent<PalmOffsetTuner>();
            SetField(tuner, "target", visibility);
            SetField(tuner, "xValue", xVal);
            SetField(tuner, "yValue", yVal);
            SetField(tuner, "zValue", zVal);

            UnityEventTools.AddPersistentListener(xMinus.onClick, (UnityAction)tuner.XMinus);
            UnityEventTools.AddPersistentListener(xPlus.onClick,  (UnityAction)tuner.XPlus);
            UnityEventTools.AddPersistentListener(yMinus.onClick, (UnityAction)tuner.YMinus);
            UnityEventTools.AddPersistentListener(yPlus.onClick,  (UnityAction)tuner.YPlus);
            UnityEventTools.AddPersistentListener(zMinus.onClick, (UnityAction)tuner.ZMinus);
            UnityEventTools.AddPersistentListener(zPlus.onClick,  (UnityAction)tuner.ZPlus);

            return tcGo;
        }

        // Sizes are scaled for the 1920×1080 tuner canvas.
        static void SpawnHeader(Transform parent, string text)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 140;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 100;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        static TMP_Text SpawnTunerRow(Transform parent, string axisLabel, out Button minusBtn, out Button plusBtn)
        {
            var row = new GameObject($"Row_{axisLabel}",
                typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 200;
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 40;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            SpawnRowLabel(row.transform, axisLabel, 110, 120);
            minusBtn = SpawnTunerButton(row.transform, "-");
            var valLabel = SpawnRowLabel(row.transform, "0 cm", 90, 380);
            valLabel.alignment = TextAlignmentOptions.Center;
            plusBtn = SpawnTunerButton(row.transform, "+");

            return valLabel;
        }

        static TMP_Text SpawnRowLabel(Transform parent, string text, int fontSize, int preferredWidth)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = preferredWidth;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        static GameObject BuildTestCanvas()
        {
            // Placeholder test canvas — same plumbing as the tuner but with a single
            // label so we can see layer behaviour without a full UI.
            var tcGo = new GameObject("TestCanvas", typeof(RectTransform));
            var trt = tcGo.GetComponent<RectTransform>();
            trt.localScale = new Vector3(0.00018f, 0.00018f, 0.00018f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(1920, 1080);

            var canvas = tcGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 160;
            tcGo.AddComponent<GraphicRaycaster>();
            var pointable = tcGo.AddComponent<PointableCanvas>();
            SetField(pointable, "_canvas", canvas);

            AttachStandardBillboard(tcGo);

            var us = new GameObject("UISurface", typeof(RectTransform));
            us.transform.SetParent(tcGo.transform, false);
            var srt = us.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            var planeSurface = us.AddComponent<PlaneSurface>();
            SetField(planeSurface, "_doubleSided", true);
            var rayInteractable = us.AddComponent<RayInteractable>();
            var boundsClipper = us.AddComponent<BoundsClipper>();
            SetField(boundsClipper, "_position", Vector3.zero);
            SetField(boundsClipper, "_size", new Vector3(1920, 1080, 0.01f));
            var clipperDriver = us.AddComponent<RectTransformBoundsClipperDriver>();
            SetField(clipperDriver, "_boundsClipper", boundsClipper);
            var clippedSurface = us.AddComponent<ClippedPlaneSurface>();
            SetField(clippedSurface, "_planeSurface", planeSurface);
            SetClippersList(clippedSurface, "_clippers", boundsClipper);
            var pokeInteractable = us.AddComponent<PokeInteractable>();
            SetField(rayInteractable, "_surface", clippedSurface);
            SetField(rayInteractable, "_pointableElement", pointable);
            SetField(pokeInteractable, "_surfacePatch", clippedSurface);
            SetField(pokeInteractable, "_pointableElement", pointable);
            SetField(pokeInteractable, "_enterHoverNormal", 0.15f);
            SetField(pokeInteractable, "_exitHoverNormal", 0.20f);
            SetField(pokeInteractable, "_cancelSelectTangent", 0.10f);

            var panel = new GameObject("TestContent",
                typeof(RectTransform), typeof(UIImage), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(tcGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.GetComponent<UIImage>().color = new Color(0.10f, 0.05f, 0.15f, 0.92f);
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(80, 80, 80, 80);
            vlg.spacing = 40;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            SpawnHeader(panel.transform, "Test Panel");
            SpawnHeader(panel.transform, "(placeholder)");

            return tcGo;
        }

        static void AttachStandardBillboard(GameObject go)
        {
            var b = go.GetComponent<CanvasBillboard>();
            if (b == null) b = go.AddComponent<CanvasBillboard>();
            SetField(b, "distance", 0.32f);
            SetField(b, "verticalGazeOffset", -0.10f);
            SetField(b, "horizontalGazeOffset", 0f);
            SetField(b, "rotationDamping", 14f);
            SetField(b, "positionDamping", 6f);
            SetField(b, "recenterAngleDegrees", 30f);
            SetField(b, "stableRequiredSeconds", 0.8f);
        }

        static void EnsureManualBillboard(GameObject rootCanvasGo)
        {
            AttachStandardBillboard(rootCanvasGo);
        }

        static void DestroySceneObjectsByName(string name)
        {
            foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (tr == null || tr.gameObject == null) continue;
                if (tr.name != name) continue;
                if (!tr.gameObject.scene.IsValid()) continue;
                Object.DestroyImmediate(tr.gameObject);
            }
        }

        static Button SpawnTunerButton(Transform parent, string text)
        {
            var go = new GameObject($"Btn_{text}",
                typeof(RectTransform), typeof(UIImage), typeof(Button), typeof(LayoutElement), typeof(PressFireOnDown));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 240;
            le.preferredHeight = 180;
            var img = go.GetComponent<UIImage>();
            img.color = new Color(0.20f, 0.28f, 0.40f, 0.95f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = new Color(1f, 1f, 1f, 1f);
            cb.highlightedColor = new Color(0.7f, 0.85f, 1f, 1f);
            cb.pressedColor     = new Color(0.45f, 0.75f, 1f, 1f);
            cb.selectedColor    = new Color(0.7f, 0.85f, 1f, 1f);
            cb.fadeDuration     = 0.05f;
            btn.colors = cb;

            var lbl = new GameObject("Label", typeof(RectTransform));
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 130;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }

        static Transform FindLeftHandAnchor()
        {
            // OVRCameraRig populates leftHandAnchor at Awake; in editor it can be null,
            // so also try a name-based search under TrackingSpace.
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            if (rig != null && rig.leftHandAnchor != null) return rig.leftHandAnchor;
            if (rig != null)
            {
                var ts = rig.transform.Find("TrackingSpace");
                if (ts != null)
                {
                    var lh = ts.Find("LeftHandAnchor");
                    if (lh != null) return lh;
                }
            }
            return null;
        }

        static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        static void SetField(object obj, string name, object value)
        {
            var t = obj.GetType();
            while (t != null)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { f.SetValue(obj, value); return; }
                t = t.BaseType;
            }
            Debug.LogWarning($"[WireManualToggle] Field '{name}' not found on {obj.GetType().Name}");
        }

        static void SetClippersList(object obj, string name, Component clipper)
        {
            var t = obj.GetType();
            FieldInfo fld = null;
            while (t != null)
            {
                fld = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fld != null) break;
                t = t.BaseType;
            }
            if (fld == null) { Debug.LogWarning($"[WireManualToggle] Field {name} not found"); return; }
            var listType = fld.FieldType;
            var listInst = (System.Collections.IList)System.Activator.CreateInstance(listType);
            listInst.Add(clipper);
            fld.SetValue(obj, listInst);
        }
    }
}
