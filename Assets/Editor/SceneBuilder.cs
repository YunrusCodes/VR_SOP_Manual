using System.IO;
using Inspection.App;
using Inspection.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Inspection.EditorTools
{
    /// <summary>
    /// Builds App.unity, the CourseCard / ExceptionButton prefabs and the AppSettings asset
    /// from scratch, wiring all SerializeField references. Idempotent: deletes and recreates.
    ///
    /// Trigger from menu: Inspection > Build App Scene.
    ///
    /// The scene is intentionally built around a 2D Camera (not XR Origin), so it runs in the
    /// Editor without requiring Meta XR / OpenXR / XRI to be installed first. Swap in XR Origin
    /// for Quest 3 deployment per docs/spec.md §7.10 step 2.
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/App.unity";
        const string CourseCardPrefabPath = "Assets/Prefabs/UI/CourseCard.prefab";
        const string ExceptionButtonPrefabPath = "Assets/Prefabs/UI/ExceptionButton.prefab";
        const string AppSettingsPath = "Assets/Settings/AppSettings.asset";

        [MenuItem("Inspection/Build App Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder("Assets/Settings");

            var settings = CreateOrLoadSettings();
            var courseCardPrefab = CreateCourseCardPrefab();
            var exceptionButtonPrefab = CreateExceptionButtonPrefab();
            // Flush so freshly-created assets get persistent file IDs / GUIDs before being
            // referenced from the scene; otherwise SerializedObject assignment can serialize
            // as null. Reload to drop any in-memory transient handles AssetDatabase may have
            // swapped out during the import.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            settings = AssetDatabase.LoadAssetAtPath<AppSettings>(AppSettingsPath);
            courseCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CourseCardPrefabPath);
            exceptionButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExceptionButtonPrefabPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            var (router, manualList, courseView, overlay) =
                BuildRootCanvas(courseCardPrefab, exceptionButtonPrefab);
            BuildServices(settings, router, manualList, courseView, overlay);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] Built {ScenePath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ScenePath);
        }

        // ---- Settings ----------------------------------------------------------

        static AppSettings CreateOrLoadSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AppSettings>(AppSettingsPath);
            if (existing != null) return existing;
            var s = ScriptableObject.CreateInstance<AppSettings>();
            s.ApiBaseUrl = "http://192.168.1.10:8000";
            s.Company = "acme";
            s.VerboseLog = true;
            AssetDatabase.CreateAsset(s, AppSettingsPath);
            return s;
        }

        // ---- Prefabs -----------------------------------------------------------

        static GameObject CreateCourseCardPrefab()
        {
            var go = new GameObject("CourseCard", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(800, 130);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.18f, 0.95f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(28, 28, 18, 18);
            hlg.spacing = 24;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 130;
            le.preferredHeight = 130;

            var titleGo = NewText("Title", go.transform, "<課程名稱>", 40, TextAlignmentOptions.MidlineLeft);
            titleGo.GetComponent<TMP_Text>().enableWordWrapping = false;
            titleGo.GetComponent<TMP_Text>().overflowMode = TextOverflowModes.Ellipsis;
            var titleLE = titleGo.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1;
            titleLE.minHeight = 60;

            var btnGo = NewButton("EnterButton", go.transform, "進入", 36, new Vector2(200, 80));
            var btnLE = btnGo.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 200;
            btnLE.preferredHeight = 80;

            var card = go.AddComponent<CourseCard>();
            var so = new SerializedObject(card);
            so.FindProperty("title").objectReferenceValue = titleGo.GetComponent<TMP_Text>();
            so.FindProperty("enterButton").objectReferenceValue = btnGo.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, CourseCardPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateExceptionButtonPrefab()
        {
            var go = new GameObject("ExceptionButton", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(220, 64);

            var btnImage = go.AddComponent<Image>();
            btnImage.color = new Color(0.86f, 0.42f, 0.20f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            var labelGo = NewText("Label", go.transform, "例外", 28, TextAlignmentOptions.Center);
            var labelRT = (RectTransform)labelGo.transform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(8, 4);
            labelRT.offsetMax = new Vector2(-8, -4);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 220;
            le.preferredHeight = 64;

            var ex = go.AddComponent<ExceptionButton>();
            var so = new SerializedObject(ex);
            so.FindProperty("label").objectReferenceValue = labelGo.GetComponent<TMP_Text>();
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, ExceptionButtonPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---- Scene environment -------------------------------------------------

        static void BuildEnvironment()
        {
            var persistent = new GameObject("Persistent");

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(persistent.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.orthographic = false;
            camGo.transform.position = new Vector3(0, 1.5f, -2f);
            camGo.AddComponent<AudioListener>();

            // Lighting
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(persistent.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // EventSystem — use the new Input System UI module since the project enables Input System Package.
            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(persistent.transform);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // ---- Canvas + Views ----------------------------------------------------

        static (AppRouter router, ManualListView manualList, CourseView courseView, LoadingOverlay overlay)
            BuildRootCanvas(GameObject courseCardPrefab, GameObject exceptionButtonPrefab)
        {
            var canvasGo = new GameObject("RootCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var manualList = BuildManualListView(canvasGo.transform, courseCardPrefab);
            var courseView = BuildCourseView(canvasGo.transform, exceptionButtonPrefab);
            var overlay = BuildLoadingOverlay(canvasGo.transform);

            // Default visibility
            manualList.gameObject.SetActive(true);
            courseView.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);

            // AppRouter is on canvas — wire via SerializedObject
            var router = canvasGo.AddComponent<AppRouter>();
            var so = new SerializedObject(router);
            so.FindProperty("manualList").objectReferenceValue = manualList;
            so.FindProperty("courseView").objectReferenceValue = courseView;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (router, manualList, courseView, overlay);
        }

        static ManualListView BuildManualListView(Transform parent, GameObject courseCardPrefab)
        {
            var root = new GameObject("ManualListView", typeof(RectTransform));
            root.transform.SetParent(parent, worldPositionStays: false);
            FullStretch((RectTransform)root.transform);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.13f, 1f);

            // Header
            var header = NewText("Header", root.transform, "選擇課程", 72, TextAlignmentOptions.Center);
            var hr = (RectTransform)header.transform;
            hr.anchorMin = new Vector2(0, 1);
            hr.anchorMax = new Vector2(1, 1);
            hr.pivot = new Vector2(0.5f, 1);
            hr.sizeDelta = new Vector2(0, 100);
            hr.anchoredPosition = new Vector2(0, -10);

            // ScrollRect
            var scrollGo = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(root.transform, false);
            var scrollRT = (RectTransform)scrollGo.transform;
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = new Vector2(120, 60);
            scrollRT.offsetMax = new Vector2(-120, -120);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.2f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            FullStretch((RectTransform)viewport.transform);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRT = (RectTransform)content.transform;
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = (RectTransform)viewport.transform;
            sr.content = contentRT;
            sr.horizontal = false;
            sr.vertical = true;

            // EmptyState (hidden)
            var empty = NewText("EmptyState", root.transform, "目前沒有課程", 40, TextAlignmentOptions.Center);
            empty.SetActive(false);
            var er = (RectTransform)empty.transform;
            er.anchorMin = new Vector2(0.5f, 0.5f);
            er.anchorMax = new Vector2(0.5f, 0.5f);
            er.sizeDelta = new Vector2(600, 80);

            // ErrorLabel (hidden)
            var error = NewText("ErrorLabel", root.transform, "", 32, TextAlignmentOptions.Center);
            error.GetComponent<TMP_Text>().color = new Color(1f, 0.45f, 0.45f);
            error.SetActive(false);
            var errR = (RectTransform)error.transform;
            errR.anchorMin = new Vector2(0, 0);
            errR.anchorMax = new Vector2(1, 0);
            errR.sizeDelta = new Vector2(0, 60);
            errR.anchoredPosition = new Vector2(0, 30);

            var view = root.AddComponent<ManualListView>();
            var so = new SerializedObject(view);
            so.FindProperty("contentRoot").objectReferenceValue = contentRT;
            so.FindProperty("courseCardPrefab").objectReferenceValue = courseCardPrefab.GetComponent<CourseCard>();
            so.FindProperty("emptyState").objectReferenceValue = empty;
            so.FindProperty("errorLabel").objectReferenceValue = error.GetComponent<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        static CourseView BuildCourseView(Transform parent, GameObject exceptionButtonPrefab)
        {
            var root = new GameObject("CourseView", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            FullStretch((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.10f, 1f);

            // TopBar
            var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(root.transform, false);
            var tbRT = (RectTransform)topBar.transform;
            tbRT.anchorMin = new Vector2(0, 1);
            tbRT.anchorMax = new Vector2(1, 1);
            tbRT.pivot = new Vector2(0.5f, 1);
            tbRT.sizeDelta = new Vector2(0, 80);
            topBar.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.16f);

            var breadcrumb = NewText("Breadcrumb", topBar.transform, "—", 36, TextAlignmentOptions.MidlineLeft);
            var bcRT = (RectTransform)breadcrumb.transform;
            bcRT.anchorMin = new Vector2(0, 0);
            bcRT.anchorMax = new Vector2(0.6f, 1);
            bcRT.offsetMin = new Vector2(20, 0);
            bcRT.offsetMax = new Vector2(0, 0);

            var stepCounter = NewText("StepCounter", topBar.transform, "1 / 1", 36, TextAlignmentOptions.MidlineRight);
            var scRT = (RectTransform)stepCounter.transform;
            scRT.anchorMin = new Vector2(0.6f, 0);
            scRT.anchorMax = new Vector2(0.85f, 1);
            scRT.offsetMin = scRT.offsetMax = Vector2.zero;

            var backBtn = NewButton("BackToListButton", topBar.transform, "← 課程清單", 28, new Vector2(220, 56));
            var bbRT = (RectTransform)backBtn.transform;
            bbRT.anchorMin = new Vector2(1, 0.5f);
            bbRT.anchorMax = new Vector2(1, 0.5f);
            bbRT.pivot = new Vector2(1, 0.5f);
            bbRT.anchoredPosition = new Vector2(-20, 0);

            // ContentArea: left text column, right media column
            var contentArea = new GameObject("ContentArea", typeof(RectTransform));
            contentArea.transform.SetParent(root.transform, false);
            var caRT = (RectTransform)contentArea.transform;
            caRT.anchorMin = new Vector2(0, 0);
            caRT.anchorMax = new Vector2(1, 1);
            caRT.offsetMin = new Vector2(40, 180);
            caRT.offsetMax = new Vector2(-40, -100);

            // Left column
            var left = new GameObject("LeftColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            left.transform.SetParent(contentArea.transform, false);
            var leftRT = (RectTransform)left.transform;
            leftRT.anchorMin = new Vector2(0, 0);
            leftRT.anchorMax = new Vector2(0.6f, 1);
            leftRT.offsetMin = leftRT.offsetMax = Vector2.zero;
            var leftVlg = left.GetComponent<VerticalLayoutGroup>();
            leftVlg.padding = new RectOffset(8, 24, 8, 8);
            leftVlg.spacing = 16;
            leftVlg.childControlWidth = true;
            leftVlg.childControlHeight = true;
            leftVlg.childForceExpandWidth = true;
            leftVlg.childForceExpandHeight = false;

            var stepName = NewText("StepName", left.transform, "<Step>", 60, TextAlignmentOptions.TopLeft);
            stepName.AddComponent<LayoutElement>().preferredHeight = 64;

            var descScroll = new GameObject("DescriptionScroll",
                typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            descScroll.transform.SetParent(left.transform, false);
            descScroll.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);
            var descScrollLE = descScroll.AddComponent<LayoutElement>();
            descScrollLE.flexibleHeight = 1;
            descScrollLE.minHeight = 200;

            var descViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            descViewport.transform.SetParent(descScroll.transform, false);
            FullStretch((RectTransform)descViewport.transform);
            descViewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            descViewport.GetComponent<Mask>().showMaskGraphic = false;

            var description = NewText("Description", descViewport.transform, "<description>", 36, TextAlignmentOptions.TopLeft);
            var descRT = (RectTransform)description.transform;
            descRT.anchorMin = new Vector2(0, 1);
            descRT.anchorMax = new Vector2(1, 1);
            descRT.pivot = new Vector2(0.5f, 1);
            descRT.sizeDelta = new Vector2(0, 0);
            var descTmp = description.GetComponent<TMP_Text>();
            descTmp.enableWordWrapping = true;
            description.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var descSr = descScroll.GetComponent<ScrollRect>();
            descSr.viewport = (RectTransform)descViewport.transform;
            descSr.content = descRT;
            descSr.horizontal = false;
            descSr.vertical = true;

            var nextIndication = NewText("NextIndication", left.transform, "", 28, TextAlignmentOptions.TopLeft);
            nextIndication.GetComponent<TMP_Text>().fontStyle = FontStyles.Italic;
            nextIndication.GetComponent<TMP_Text>().color = new Color(0.7f, 0.85f, 1f);
            nextIndication.AddComponent<LayoutElement>().preferredHeight = 36;

            // Right column / MediaPanel
            var right = new GameObject("RightColumn", typeof(RectTransform));
            right.transform.SetParent(contentArea.transform, false);
            var rightRT = (RectTransform)right.transform;
            rightRT.anchorMin = new Vector2(0.6f, 0);
            rightRT.anchorMax = new Vector2(1, 1);
            rightRT.offsetMin = rightRT.offsetMax = Vector2.zero;

            var mediaPanel = new GameObject("MediaPanel", typeof(RectTransform), typeof(Image));
            mediaPanel.transform.SetParent(right.transform, false);
            var mpRT = (RectTransform)mediaPanel.transform;
            mpRT.anchorMin = new Vector2(0, 0);
            mpRT.anchorMax = new Vector2(1, 1);
            mpRT.offsetMin = new Vector2(8, 8);
            mpRT.offsetMax = new Vector2(-8, -8);
            mediaPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);

            var imageView = new GameObject("ImageView", typeof(RectTransform), typeof(RawImage));
            imageView.transform.SetParent(mediaPanel.transform, false);
            FullStretch((RectTransform)imageView.transform);

            var videoView = new GameObject("VideoView", typeof(RectTransform), typeof(RawImage), typeof(VideoPlayer));
            videoView.transform.SetParent(mediaPanel.transform, false);
            FullStretch((RectTransform)videoView.transform);
            var videoRaw = videoView.GetComponent<RawImage>();
            var rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
            rt.name = "VideoTarget";
            rt.useMipMap = false;
            // Persisted on disk so prefab refs work — save under Assets/Settings/.
            const string rtPath = "Assets/Settings/VideoTarget.renderTexture";
            AssetDatabase.CreateAsset(rt, rtPath);
            videoRaw.texture = rt;
            var vp = videoView.GetComponent<VideoPlayer>();
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.playOnAwake = false;
            vp.audioOutputMode = VideoAudioOutputMode.None;

            // ExceptionLayer
            var exLayer = new GameObject("ExceptionLayer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            exLayer.transform.SetParent(root.transform, false);
            var exRT = (RectTransform)exLayer.transform;
            exRT.anchorMin = new Vector2(0, 0);
            exRT.anchorMax = new Vector2(1, 0);
            exRT.pivot = new Vector2(0.5f, 0);
            exRT.sizeDelta = new Vector2(0, 80);
            exRT.anchoredPosition = new Vector2(0, 100);
            var exHlg = exLayer.GetComponent<HorizontalLayoutGroup>();
            exHlg.spacing = 16;
            exHlg.padding = new RectOffset(40, 40, 8, 8);
            exHlg.childAlignment = TextAnchor.MiddleCenter;
            exHlg.childForceExpandWidth = false;
            exHlg.childForceExpandHeight = false;

            // NavBar
            var navBar = new GameObject("NavBar", typeof(RectTransform));
            navBar.transform.SetParent(root.transform, false);
            var nbRT = (RectTransform)navBar.transform;
            nbRT.anchorMin = new Vector2(0, 0);
            nbRT.anchorMax = new Vector2(1, 0);
            nbRT.pivot = new Vector2(0.5f, 0);
            nbRT.sizeDelta = new Vector2(0, 90);

            var prevBtn = NewButton("PrevStepButton", navBar.transform, "← 上一步", 36, new Vector2(220, 70));
            var pRT = (RectTransform)prevBtn.transform;
            pRT.anchorMin = new Vector2(0, 0.5f);
            pRT.anchorMax = new Vector2(0, 0.5f);
            pRT.pivot = new Vector2(0, 0.5f);
            pRT.anchoredPosition = new Vector2(40, 0);

            var nextBtn = NewButton("NextStepButton", navBar.transform, "下一步 →", 36, new Vector2(220, 70));
            var nRT = (RectTransform)nextBtn.transform;
            nRT.anchorMin = new Vector2(1, 0.5f);
            nRT.anchorMax = new Vector2(1, 0.5f);
            nRT.pivot = new Vector2(1, 0.5f);
            nRT.anchoredPosition = new Vector2(-40, 0);

            var view = root.AddComponent<CourseView>();
            var so = new SerializedObject(view);
            so.FindProperty("breadcrumb").objectReferenceValue = breadcrumb.GetComponent<TMP_Text>();
            so.FindProperty("stepCounter").objectReferenceValue = stepCounter.GetComponent<TMP_Text>();
            so.FindProperty("stepName").objectReferenceValue = stepName.GetComponent<TMP_Text>();
            so.FindProperty("description").objectReferenceValue = description.GetComponent<TMP_Text>();
            so.FindProperty("nextIndication").objectReferenceValue = nextIndication.GetComponent<TMP_Text>();
            so.FindProperty("mediaPanel").objectReferenceValue = mediaPanel;
            so.FindProperty("imageView").objectReferenceValue = imageView.GetComponent<RawImage>();
            so.FindProperty("videoView").objectReferenceValue = videoRaw;
            so.FindProperty("videoPlayer").objectReferenceValue = vp;
            so.FindProperty("exceptionLayer").objectReferenceValue = exLayer.transform;
            so.FindProperty("exceptionButtonPrefab").objectReferenceValue = exceptionButtonPrefab.GetComponent<ExceptionButton>();
            so.FindProperty("prevStepButton").objectReferenceValue = prevBtn.GetComponent<Button>();
            so.FindProperty("nextStepButton").objectReferenceValue = nextBtn.GetComponent<Button>();
            so.FindProperty("backToListButton").objectReferenceValue = backBtn.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        static LoadingOverlay BuildLoadingOverlay(Transform parent)
        {
            var root = new GameObject("LoadingOverlay", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            FullStretch((RectTransform)root.transform);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.78f);
            // Block clicks on underlying view
            root.AddComponent<CanvasGroup>().blocksRaycasts = true;

            var label = NewText("Label", root.transform, "載入中…", 44, TextAlignmentOptions.Center);
            var lr = (RectTransform)label.transform;
            lr.anchorMin = new Vector2(0, 0.5f);
            lr.anchorMax = new Vector2(1, 0.5f);
            lr.sizeDelta = new Vector2(0, 100);

            var closeRoot = new GameObject("CloseRoot", typeof(RectTransform));
            closeRoot.transform.SetParent(root.transform, false);
            var cRT = (RectTransform)closeRoot.transform;
            cRT.anchorMin = new Vector2(0.5f, 0.3f);
            cRT.anchorMax = new Vector2(0.5f, 0.3f);
            cRT.sizeDelta = new Vector2(220, 70);

            var closeBtn = NewButton("CloseButton", closeRoot.transform, "關閉", 32, new Vector2(220, 70));
            var btn = closeBtn.GetComponent<Button>();
            closeRoot.SetActive(false);

            var overlay = root.AddComponent<LoadingOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("label").objectReferenceValue = label.GetComponent<TMP_Text>();
            so.FindProperty("closeButtonRoot").objectReferenceValue = closeRoot;
            so.FindProperty("closeButton").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return overlay;
        }

        static void BuildServices(AppSettings settings, AppRouter router,
            ManualListView manualList, CourseView courseView, LoadingOverlay overlay)
        {
            var services = new GameObject("Services");
            var bootstrapper = services.AddComponent<AppBootstrapper>();
            var so = new SerializedObject(bootstrapper);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("router").objectReferenceValue = router;
            so.FindProperty("manualList").objectReferenceValue = manualList;
            so.FindProperty("courseView").objectReferenceValue = courseView;
            so.FindProperty("overlay").objectReferenceValue = overlay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Helpers -----------------------------------------------------------

        static GameObject NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.enableWordWrapping = true;
            return go;
        }

        static GameObject NewButton(string name, Transform parent, string label, float size, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = sizeDelta;
            go.GetComponent<Image>().color = new Color(0.20f, 0.55f, 0.95f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var labelGo = NewText("Label", go.transform, label, size, TextAlignmentOptions.Center);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(8, 4);
            lr.offsetMax = new Vector2(-8, -4);
            return go;
        }

        static void FullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
