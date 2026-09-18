using System.Collections.Generic;
using KBP.URDT;
using KBP.URDT.Inspect;
using KBP.URDT.TestPoligon;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Editor
{
    /// <summary>
    /// Rebuilds the launcher-first UI wave scene for the URDT test polygon.
    /// </summary>
    public static class UrdtUiTestPoligonSceneBuilder
    {
        private const string ROOT_FOLDER = "Assets/URDT_TestPoligon";
        private const string SCENES_FOLDER = ROOT_FOLDER + "/Scenes";
        private const string SCENE_PATH = SCENES_FOLDER + "/URDT_TestPoligon_UI.unity";
        private const string UI_INPUT_ACTIONS_PATH = ROOT_FOLDER + "/URDT_UI_InputActions.inputactions";
        private const string DEFAULT_UI_INPUT_ACTIONS_PATH = "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
        private const string COMMON_COMMANDS = "inspect,query,click,double_click,multi_click";

        [MenuItem("Tools/URDT Test Poligon/Rebuild UI Scene")]
        public static void RebuildUiScene()
        {
            EnsureFolders();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "URDT_TestPoligon_UI";

            CreateCamera();

            Canvas canvas = CreateCanvas();
            RectTransform safeArea = CreateSafeArea(canvas.transform);
            RectTransform appRoot = CreateAppRoot(safeArea);
            TMP_Text statusText = CreateHeader(appRoot);
            RectTransform windowHost = CreateWindowHost(appRoot);

            List<UrdtUiTarget> targets = new List<UrdtUiTarget>();

            GameObject mainMenuWindow = CreateWindow(windowHost, "MainMenuWindow", true);
            UrdtUiWindowTarget mainWindowTarget = AddWindowTarget(
                mainMenuWindow,
                canvas,
                "window_main_menu",
                "window_main_menu",
                "launcher",
                "inspect,query");
            targets.Add(mainWindowTarget);

            Button openUiButton = CreateMenuButton(mainMenuWindow.transform, "OpenUiSuiteButton", "UI");
            UrdtUiButtonTarget openUiTarget = AddButtonTarget(
                openUiButton.gameObject,
                canvas,
                "btn_open_ui_suite",
                "window_main_menu",
                "launcher",
                "launcher",
                COMMON_COMMANDS,
                selectable: openUiButton,
                clickResult: "open_ui_suite");
            targets.Add(openUiTarget);

            Button open2DButton = CreateMenuButton(mainMenuWindow.transform, "Open2DSuiteButton", "2D");
            UrdtUiButtonTarget open2DTarget = AddButtonTarget(
                open2DButton.gameObject,
                canvas,
                "btn_open_2d_suite",
                "window_main_menu",
                "launcher",
                "launcher",
                COMMON_COMMANDS,
                selectable: open2DButton,
                clickResult: "open_2d_suite");
            targets.Add(open2DTarget);

            Button open3DButton = CreateMenuButton(mainMenuWindow.transform, "Open3DSuiteButton", "3D");
            UrdtUiButtonTarget open3DTarget = AddButtonTarget(
                open3DButton.gameObject,
                canvas,
                "btn_open_3d_suite",
                "window_main_menu",
                "launcher",
                "launcher",
                COMMON_COMMANDS,
                selectable: open3DButton,
                clickResult: "open_3d_suite");
            targets.Add(open3DTarget);

            Button openIntegrationButton = CreateMenuButton(mainMenuWindow.transform, "OpenIntegrationSuiteButton", "Integration");
            UrdtUiButtonTarget openIntegrationTarget = AddButtonTarget(
                openIntegrationButton.gameObject,
                canvas,
                "btn_open_integration_suite",
                "window_main_menu",
                "launcher",
                "launcher",
                COMMON_COMMANDS,
                selectable: openIntegrationButton,
                clickResult: "open_integration_suite");
            targets.Add(openIntegrationTarget);

            Button runAllButton = CreateMenuButton(mainMenuWindow.transform, "RunAllSuitesButton", "Run All");
            UrdtUiButtonTarget runAllTarget = AddButtonTarget(
                runAllButton.gameObject,
                canvas,
                "btn_run_all_suites",
                "window_main_menu",
                "launcher",
                "launcher",
                COMMON_COMMANDS,
                selectable: runAllButton,
                clickResult: "run_all_requested");
            targets.Add(runAllTarget);

            UiSuiteRefs uiSuite = CreateUiSuite(windowHost, canvas, targets);
            SuiteShellRefs suite2D = CreateSuiteShell(windowHost, canvas, targets, "World2DSuiteWindow", "window_2d_suite", "2d", "2D");
            SuiteShellRefs suite3D = CreateSuiteShell(windowHost, canvas, targets, "World3DSuiteWindow", "window_3d_suite", "3d", "3D");
            SuiteShellRefs integrationSuite = CreateSuiteShell(windowHost, canvas, targets, "IntegrationSuiteWindow", "window_integration_suite", "integration", "Integration");
            GameObject modalWindow = CreateModal(safeArea);
            Button modalCloseButton = modalWindow.GetComponentInChildren<Button>(true);
            UrdtUiButtonTarget modalCloseTarget = AddButtonTarget(
                modalCloseButton.gameObject,
                canvas,
                "ui.modal_close_button",
                "window_modal",
                "ui",
                "ui",
                COMMON_COMMANDS,
                selectable: modalCloseButton,
                clickResult: "modal_closed");
            targets.Add(modalCloseTarget);

            CreateEventSystem();
            CreateUrdtHost();

            UrdtUiTestPoligonController controller =
                new GameObject("UrdtUiTestPoligonController").AddComponent<UrdtUiTestPoligonController>();
            controller.Configure(
                mainMenuWindow,
                uiSuite.Window,
                suite2D.Window,
                suite3D.Window,
                integrationSuite.Window,
                openUiButton,
                open2DButton,
                open3DButton,
                openIntegrationButton,
                runAllButton,
                uiSuite.BackButton,
                suite2D.BackButton,
                suite3D.BackButton,
                integrationSuite.BackButton,
                uiSuite.PrimaryButton,
                uiSuite.ResetButton,
                uiSuite.ModalOpenButton,
                modalCloseButton,
                uiSuite.Toggle,
                uiSuite.Slider,
                uiSuite.Input,
                uiSuite.Dropdown,
                uiSuite.ScrollRect,
                modalWindow,
                statusText);

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("URDT Test Poligon UI scene rebuilt: " + SCENE_PATH);

            UrdtMechanics2DBuilder.BuildAllAndSetup();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ROOT_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets", "URDT_TestPoligon");
            }

            if (!AssetDatabase.IsValidFolder(SCENES_FOLDER))
            {
                AssetDatabase.CreateFolder(ROOT_FOLDER, "Scenes");
            }
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 1f);
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("URDT_UI_Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static RectTransform CreateSafeArea(Transform parent)
        {
            GameObject safeAreaObject = CreateStretchObject("SafeArea", parent);
            safeAreaObject.AddComponent<UrdtTestPoligonSafeArea>();
            return safeAreaObject.GetComponent<RectTransform>();
        }

        private static RectTransform CreateAppRoot(Transform parent)
        {
            GameObject rootObject = CreateStretchObject("AppRoot", parent);
            VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rootObject.GetComponent<RectTransform>();
        }

        private static TMP_Text CreateHeader(Transform parent)
        {
            GameObject headerObject = CreateLayoutObject("Header", parent, 0f, 44f);
            HorizontalLayoutGroup layout = headerObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            LayoutElement headerLayout = headerObject.GetComponent<LayoutElement>();
            headerLayout.preferredHeight = 44f;
            headerLayout.flexibleHeight = 0f;

            TMP_Text title = CreateText("Title", headerObject.transform, "URDT Test Poligon", 24, TextAlignmentOptions.Left);
            title.color = Color.white;
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
            titleLayout.preferredHeight = 40f;
            titleLayout.flexibleHeight = 0f;

            TMP_Text status = CreateText("StatusText", headerObject.transform, "ready", 16, TextAlignmentOptions.Right);
            status.color = new Color(0.72f, 0.82f, 0.9f, 1f);
            LayoutElement statusLayout = status.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 280f;
            statusLayout.preferredHeight = 40f;
            statusLayout.flexibleHeight = 0f;
            return status;
        }

        private static RectTransform CreateWindowHost(Transform parent)
        {
            GameObject hostObject = CreateLayoutObject("WindowHost", parent, 0f, 0f);
            LayoutElement layoutElement = hostObject.GetComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            layoutElement.minHeight = 350f;
            return hostObject.GetComponent<RectTransform>();
        }

        private static GameObject CreateWindow(Transform parent, string name, bool active)
        {
            GameObject windowObject = CreateStretchObject(name, parent);
            Image image = windowObject.AddComponent<Image>();
            image.color = new Color(0.1f, 0.12f, 0.14f, 0.96f);
            VerticalLayoutGroup layout = windowObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            windowObject.SetActive(active);
            return windowObject;
        }

        private static Button CreateMenuButton(Transform parent, string name, string label)
        {
            Button button = CreateButton(parent, name, label, 64f);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = 240f;
            layout.preferredWidth = 520f;
            return button;
        }

        private static UiSuiteRefs CreateUiSuite(
            Transform parent,
            Canvas canvas,
            List<UrdtUiTarget> targets)
        {
            UiSuiteRefs refs = new UiSuiteRefs();
            refs.Window = CreateWindow(parent, "UiSuiteWindow", false);
            UrdtUiWindowTarget windowTarget = AddWindowTarget(
                refs.Window,
                canvas,
                "window_ui_suite",
                "window_ui_suite",
                "ui",
                "inspect,query");
            targets.Add(windowTarget);

            // Top Navigation Bar (Side-by-side for maximal active test zone)
            GameObject navBar = CreateLayoutObject("UiNavBar", refs.Window.transform, 0f, 44f);
            HorizontalLayoutGroup navLayout = navBar.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 10f;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = false;
            LayoutElement navLe = navBar.GetComponent<LayoutElement>();
            navLe.preferredHeight = 44f;
            navLe.flexibleHeight = 0f;

            refs.BackButton = CreateButton(navBar.transform, "UiBackButton", "Main Menu", 44f);
            targets.Add(AddButtonTarget(refs.BackButton.gameObject, canvas, "btn_ui_back", "window_ui_suite", "ui", "ui", COMMON_COMMANDS, selectable: refs.BackButton, clickResult: "open_main_menu"));

            refs.ModalOpenButton = CreateButton(navBar.transform, "OpenModalButton", "Modal", 44f);
            targets.Add(AddButtonTarget(refs.ModalOpenButton.gameObject, canvas, "ui.modal_button", "window_ui_suite", "ui", "ui", COMMON_COMMANDS, selectable: refs.ModalOpenButton, clickResult: "modal_open"));

            ScrollRect suiteScroll = CreateWindowScroll(refs.Window.transform, "UiSuiteScroll");
            targets.Add(AddScrollTarget(
                suiteScroll.gameObject,
                canvas,
                "ui.suite_scroll",
                "window_ui_suite",
                "ui",
                "ui",
                "inspect,query,scroll,drag,swipe",
                scrollRect: suiteScroll));
            Transform content = suiteScroll.content;

            // --- STICK & DRAWING STUDIO (HORIZONTAL, VIEWPORT-FIT) ---
            GameObject studioCard = CreateLayoutObject("StickDrawingStudio", content, 0f, 240f);
            Image studioBg = studioCard.AddComponent<Image>();
            studioBg.color = new Color(0.08f, 0.11f, 0.15f, 0.95f);
            VerticalLayoutGroup studioLayout = studioCard.AddComponent<VerticalLayoutGroup>();
            studioLayout.padding = new RectOffset(10, 10, 8, 8);
            studioLayout.spacing = 6f;
            studioLayout.childControlWidth = true;
            studioLayout.childControlHeight = true;
            studioLayout.childForceExpandWidth = true;
            studioLayout.childForceExpandHeight = false;

            TMP_Text studioHeader = CreateText("StudioHeader", studioCard.transform, "Stick & Drawing Studio", 16, TextAlignmentOptions.Center);
            studioHeader.color = new Color(0.95f, 0.85f, 0.3f, 1f);
            studioHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            GameObject studioBody = CreateLayoutObject("StudioBody", studioCard.transform, 0f, 200f);
            HorizontalLayoutGroup bodyLayout = studioBody.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 20f;
            bodyLayout.childAlignment = TextAnchor.MiddleCenter;
            bodyLayout.childControlWidth = false;
            bodyLayout.childControlHeight = false;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = false;

            // 1. Drawing Canvas Area (Left, 320x190)
            GameObject canvasArea = CreateLayoutObject("DrawingCanvasArea", studioBody.transform, 320f, 190f);
            canvasArea.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 190f);
            Image canvasBorder = canvasArea.AddComponent<Image>();
            canvasBorder.color = new Color(0.2f, 0.25f, 0.32f, 1f);
            RawImage canvasRaw = CreateStretchObject("CanvasDisplay", canvasArea.transform).AddComponent<RawImage>();
            Stretch(canvasRaw.rectTransform, 2f, 2f, -2f, -2f);

            GameObject penCursorObj = new GameObject("PenCursor");
            RectTransform penCursorRect = penCursorObj.AddComponent<RectTransform>();
            penCursorRect.SetParent(canvasRaw.transform, false);
            penCursorRect.anchorMin = new Vector2(0.5f, 0.5f);
            penCursorRect.anchorMax = new Vector2(0.5f, 0.5f);
            penCursorRect.pivot = new Vector2(0.5f, 0.5f);
            penCursorRect.sizeDelta = new Vector2(10f, 10f);
            Image penCursorImg = penCursorObj.AddComponent<Image>();
            penCursorImg.color = new Color(0.2f, 1f, 0.3f, 0.95f);
            penCursorImg.raycastTarget = false;

            // 2. Controls Area (Right, 240x190)
            GameObject controlsArea = CreateLayoutObject("ControlsArea", studioBody.transform, 240f, 190f);
            controlsArea.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 190f);
            HorizontalLayoutGroup controlsLayout = controlsArea.AddComponent<HorizontalLayoutGroup>();
            controlsLayout.spacing = 14f;
            controlsLayout.childAlignment = TextAnchor.MiddleCenter;
            controlsLayout.childControlWidth = false;
            controlsLayout.childControlHeight = false;
            controlsLayout.childForceExpandWidth = false;
            controlsLayout.childForceExpandHeight = false;

            // 2a. Virtual Stick (100x100)
            GameObject stickBaseObj = CreateLayoutObject("VirtualStick", controlsArea.transform, 100f, 100f);
            stickBaseObj.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);
            Image stickBaseImg = stickBaseObj.AddComponent<Image>();
            stickBaseImg.color = new Color(0.14f, 0.17f, 0.22f, 0.95f);

            GameObject stickHandleObj = new GameObject("Handle");
            RectTransform stickHandleRect = stickHandleObj.AddComponent<RectTransform>();
            stickHandleRect.SetParent(stickBaseObj.transform, false);
            stickHandleRect.sizeDelta = new Vector2(36f, 36f);
            Image stickHandleImg = stickHandleObj.AddComponent<Image>();
            stickHandleImg.color = new Color(0.22f, 0.55f, 0.95f, 1f);
            stickHandleImg.raycastTarget = false;

            UrdtVirtualStick virtualStick = stickBaseObj.AddComponent<UrdtVirtualStick>();
            virtualStick.Configure(stickBaseObj.GetComponent<RectTransform>(), stickHandleRect, 36f);

            UrdtUiStickTarget stickTarget = AddStickTarget(
                stickBaseObj,
                canvas,
                "ui.virtual_stick",
                "window_ui_suite",
                "ui",
                "ui",
                "inspect,query,drag,press_move,swipe",
                virtualStick);
            targets.Add(stickTarget);

            // 2b. Buttons Column (Hold to Draw + Clear Canvas)
            GameObject btnCol = CreateLayoutObject("BtnCol", controlsArea.transform, 120f, 100f);
            btnCol.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 100f);
            VerticalLayoutGroup colLayout = btnCol.AddComponent<VerticalLayoutGroup>();
            colLayout.spacing = 8f;
            colLayout.childAlignment = TextAnchor.MiddleCenter;
            colLayout.childControlWidth = true;
            colLayout.childControlHeight = false;
            colLayout.childForceExpandWidth = true;
            colLayout.childForceExpandHeight = false;

            GameObject holdBtnObj = CreateLayoutObject("HoldDrawButton", btnCol.transform, 120f, 48f);
            holdBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 48f);
            Image holdBtnBg = holdBtnObj.AddComponent<Image>();
            holdBtnBg.color = new Color(0.18f, 0.45f, 0.72f, 1f);
            TMP_Text holdBtnLabel = CreateText("Label", holdBtnObj.transform, "Рисовать", 14, TextAlignmentOptions.Center);
            holdBtnLabel.color = Color.white;
            Stretch(holdBtnLabel.rectTransform);

            UrdtHoldButton holdButton = holdBtnObj.AddComponent<UrdtHoldButton>();
            holdButton.Configure(holdBtnBg, holdBtnLabel.gameObject, "Рисовать", "РИСУЕТ!");

            UrdtUiButtonTarget holdBtnTarget = AddButtonTarget(
                holdBtnObj,
                canvas,
                "ui.btn_draw",
                "window_ui_suite",
                "ui",
                "ui",
                COMMON_COMMANDS + ",pointer_down,pointer_up",
                clickResult: "draw_held");
            targets.Add(holdBtnTarget);

            Button clearBtn = CreateButton(btnCol.transform, "ClearCanvasButton", "Очистить", 40f);
            targets.Add(AddButtonTarget(
                clearBtn.gameObject,
                canvas,
                "ui.btn_clear_canvas",
                "window_ui_suite",
                "ui",
                "ui",
                COMMON_COMMANDS,
                selectable: clearBtn,
                clickResult: "canvas_cleared"));

            UrdtDrawingCanvas drawingCanvas = canvasArea.AddComponent<UrdtDrawingCanvas>();
            drawingCanvas.Configure(canvasRaw, penCursorRect, virtualStick, holdButton, clearBtn, 320, 190, 140f);
            clearBtn.onClick.AddListener(drawingCanvas.ClearCanvas);

            UrdtUiDrawingTarget drawingTarget = AddDrawingTarget(
                canvasArea,
                canvas,
                "ui.drawing_canvas",
                "window_ui_suite",
                "ui",
                "ui",
                drawingCanvas);
            targets.Add(drawingTarget);

            refs.PrimaryButton = CreateButton(content, "PrimaryButton", "Primary", 56f);
            targets.Add(AddButtonTarget(refs.PrimaryButton.gameObject, canvas, "ui.primary_button", "window_ui_suite", "ui", "ui", COMMON_COMMANDS, selectable: refs.PrimaryButton, clickResult: "primary_button_clicked"));

            refs.Dropdown = CreateDropdown(content, "ModeDropdown");
            targets.Add(AddDropdownTarget(refs.Dropdown.gameObject, canvas, "ui.mode_dropdown", "window_ui_suite", "ui", "ui", COMMON_COMMANDS, dropdown: refs.Dropdown));

            refs.Toggle = CreateToggle(content, "StateToggle", "Toggle");
            targets.Add(AddToggleTarget(refs.Toggle.gameObject, canvas, "ui.state_toggle", "window_ui_suite", "ui", "ui", COMMON_COMMANDS, toggle: refs.Toggle));

            refs.Slider = CreateSlider(content, "ValueSlider");
            targets.Add(AddSliderTarget(refs.Slider.gameObject, canvas, "ui.value_slider", "window_ui_suite", "ui", "ui", "inspect,query,drag,press_move,swipe", slider: refs.Slider));

            refs.Input = CreateInputField(content, "TextInput");
            targets.Add(AddInputTarget(refs.Input.gameObject, canvas, "ui.text_input", "window_ui_suite", "ui", "ui", "inspect,query,click", inputField: refs.Input));

            refs.ScrollRect = CreateCommandScroll(content, "CommandScroll");
            targets.Add(AddScrollTarget(refs.ScrollRect.gameObject, canvas, "ui.command_scroll", "window_ui_suite", "ui", "ui", "inspect,query,scroll,drag,swipe", scrollRect: refs.ScrollRect));

            refs.ResetButton = CreateButton(content, "ResetButton", "Reset", 56f);
            targets.Add(AddButtonTarget(refs.ResetButton.gameObject, canvas, "ui.reset_button", "window_ui_suite", "ui", "ui", COMMON_COMMANDS, selectable: refs.ResetButton, clickResult: "ready"));
            return refs;
        }

        private static SuiteShellRefs CreateSuiteShell(
            Transform parent,
            Canvas canvas,
            List<UrdtUiTarget> targets,
            string objectName,
            string windowId,
            string module,
            string label)
        {
            SuiteShellRefs refs = new SuiteShellRefs();
            refs.Window = CreateWindow(parent, objectName, false);
            targets.Add(AddWindowTarget(refs.Window, canvas, windowId, windowId, module, "inspect,query"));

            refs.BackButton = CreateButton(refs.Window.transform, objectName + "_BackButton", "Main Menu", 44f);
            targets.Add(AddButtonTarget(refs.BackButton.gameObject, canvas, "btn_" + module + "_back", windowId, module, module, COMMON_COMMANDS, selectable: refs.BackButton));

            TMP_Text title = CreateText(objectName + "_Title", refs.Window.transform, label + " Suite", 24, TextAlignmentOptions.Center);
            title.color = Color.white;
            LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 36f;
            titleLe.flexibleHeight = 0f;

            Button singleButton = CreateButton(refs.Window.transform, objectName + "_SingleButton", "Single", 56f);
            targets.Add(AddButtonTarget(singleButton.gameObject, canvas, "btn_" + module + "_single", windowId, module, module, COMMON_COMMANDS, selectable: singleButton));

            Button fullButton = CreateButton(refs.Window.transform, objectName + "_FullCycleButton", "Full Cycle", 56f);
            targets.Add(AddButtonTarget(fullButton.gameObject, canvas, "btn_" + module + "_full_cycle", windowId, module, module, COMMON_COMMANDS, selectable: fullButton));
            return refs;
        }

        private static ScrollRect CreateWindowScroll(Transform parent, string name)
        {
            GameObject scrollObject = CreateLayoutObject(name, parent, 0f, 0f);
            LayoutElement layoutElement = scrollObject.GetComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            layoutElement.minHeight = 300f;
            Image image = scrollObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.15f, 0.18f, 0.65f);
            ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();

            GameObject viewport = CreateStretchObject("Viewport", scrollObject.transform);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            GameObject content = CreateStretchObject("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(12f, 0f);
            contentRect.offsetMax = new Vector2(-12f, 0f);
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return scrollRect;
        }

        private static Button CreateButton(Transform parent, string name, string label, float height)
        {
            GameObject buttonObject = CreateLayoutObject(name, parent, 0f, height);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.38f, 0.62f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText(name + "_Label", buttonObject.transform, label, 20, TextAlignmentOptions.Center);
            text.color = Color.white;
            Stretch(text.rectTransform);
            return button;
        }

        private static Toggle CreateToggle(Transform parent, string name, string label)
        {
            GameObject toggleObject = CreateLayoutObject(name, parent, 0f, 56f);
            HorizontalLayoutGroup layout = toggleObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            GameObject boxObject = CreateLayoutObject("CheckBox", toggleObject.transform, 34f, 34f);
            Image boxImage = boxObject.AddComponent<Image>();
            boxImage.color = new Color(0.82f, 0.82f, 0.82f, 1f);
            GameObject markObject = CreateStretchObject("CheckMark", boxObject.transform);
            Image markImage = markObject.AddComponent<Image>();
            markImage.color = new Color(0.24f, 0.68f, 0.43f, 1f);

            TMP_Text text = CreateText(name + "_Label", toggleObject.transform, label, 20, TextAlignmentOptions.Left);
            text.color = Color.white;
            LayoutElement labelLayout = text.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.preferredHeight = 40f;

            toggle.targetGraphic = boxImage;
            toggle.graphic = markImage;
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, string name)
        {
            GameObject sliderObject = CreateLayoutObject(name, parent, 0f, 64f);
            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.25f;

            GameObject backgroundObject = CreateStretchObject("Background", sliderObject.transform);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.offsetMin = new Vector2(18f, 26f);
            backgroundRect.offsetMax = new Vector2(-18f, -26f);
            Image backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.28f, 0.28f, 0.28f, 1f);

            GameObject fillAreaObject = CreateStretchObject("Fill Area", sliderObject.transform);
            RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            fillAreaRect.offsetMin = new Vector2(18f, 26f);
            fillAreaRect.offsetMax = new Vector2(-18f, -26f);
            GameObject fillObject = CreateStretchObject("Fill", fillAreaObject.transform);
            Image fillImage = fillObject.AddComponent<Image>();
            fillImage.color = new Color(0.94f, 0.68f, 0.24f, 1f);

            GameObject handleObject = CreateLayoutObject("Handle", sliderObject.transform, 26f, 36f);
            Image handleImage = handleObject.AddComponent<Image>();
            handleImage.color = new Color(0.96f, 0.96f, 0.96f, 1f);

            slider.targetGraphic = handleImage;
            slider.fillRect = fillObject.GetComponent<RectTransform>();
            slider.handleRect = handleObject.GetComponent<RectTransform>();
            return slider;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name)
        {
            GameObject inputObject = CreateLayoutObject(name, parent, 0f, 58f);
            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();

            GameObject viewportObject = CreateStretchObject("TextViewport", inputObject.transform);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.offsetMin = new Vector2(14f, 8f);
            viewport.offsetMax = new Vector2(-14f, -8f);
            viewportObject.AddComponent<RectMask2D>();

            TMP_Text placeholder = CreateText("Placeholder", viewportObject.transform, "type here", 18, TextAlignmentOptions.Left);
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            Stretch(placeholder.rectTransform);

            TMP_Text text = CreateText("Text", viewportObject.transform, string.Empty, 18, TextAlignmentOptions.Left);
            text.color = Color.black;
            Stretch(text.rectTransform);

            input.textViewport = viewport;
            input.placeholder = placeholder;
            input.textComponent = text;
            input.targetGraphic = image;
            return input;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, string name)
        {
            GameObject dropdownObject = CreateLayoutObject(name, parent, 0f, 58f);
            Image image = dropdownObject.AddComponent<Image>();
            image.color = new Color(0.86f, 0.89f, 0.92f, 1f);
            TMP_Dropdown dropdown = dropdownObject.AddComponent<TMP_Dropdown>();

            TMP_Text caption = CreateText("Caption", dropdownObject.transform, "Mode A", 18, TextAlignmentOptions.Left);
            caption.color = Color.black;
            Stretch(caption.rectTransform, 14f, 8f, -42f, -8f);

            TMP_Text itemText;
            RectTransform template = CreateDropdownTemplate(dropdownObject.transform, out itemText);
            dropdown.targetGraphic = image;
            dropdown.captionText = caption;
            dropdown.itemText = itemText;
            dropdown.template = template;
            dropdown.options = new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData("Mode A"),
                new TMP_Dropdown.OptionData("Mode B"),
                new TMP_Dropdown.OptionData("Mode C")
            };
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static RectTransform CreateDropdownTemplate(Transform parent, out TMP_Text itemText)
        {
            GameObject templateObject = CreateStretchObject("Template", parent);
            RectTransform templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 132f);
            templateObject.SetActive(false);
            Image templateImage = templateObject.AddComponent<Image>();
            templateImage.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            ScrollRect scrollRect = templateObject.AddComponent<ScrollRect>();

            GameObject viewportObject = CreateStretchObject("Viewport", templateObject.transform);
            viewportObject.AddComponent<RectMask2D>();

            GameObject contentObject = CreateStretchObject("Content", viewportObject.transform);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 96f);
            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform item = CreateDropdownItem(content);
            scrollRect.viewport = viewportObject.GetComponent<RectTransform>();
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            itemText = item.GetComponentInChildren<TMP_Text>(true);
            return templateRect;
        }

        private static RectTransform CreateDropdownItem(Transform parent)
        {
            GameObject itemObject = CreateLayoutObject("Item", parent, 0f, 32f);
            Toggle toggle = itemObject.AddComponent<Toggle>();
            Image background = itemObject.AddComponent<Image>();
            background.color = Color.white;
            toggle.targetGraphic = background;

            TMP_Text label = CreateText("Item Label", itemObject.transform, "Option", 16, TextAlignmentOptions.Left);
            label.color = Color.black;
            Stretch(label.rectTransform, 10f, 0f, -10f, 0f);
            return itemObject.GetComponent<RectTransform>();
        }

        private static ScrollRect CreateCommandScroll(Transform parent, string name)
        {
            ScrollRect scrollRect = CreateWindowScroll(parent, name);
            LayoutElement layoutElement = scrollRect.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 180f;
            layoutElement.flexibleHeight = 0f;

            for (int i = 0; i < 8; i++)
            {
                TMP_Text row = CreateText("CommandRow_" + i, scrollRect.content, "Command " + (i + 1), 15, TextAlignmentOptions.Center);
                row.color = Color.black;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            }

            Image image = scrollRect.GetComponent<Image>();
            image.color = new Color(0.9f, 0.92f, 0.95f, 1f);
            return scrollRect;
        }

        private static GameObject CreateModal(Transform parent)
        {
            GameObject overlay = CreateStretchObject("ModalWindow", parent);
            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject panel = CreateLayoutObject("ModalPanel", overlay.transform, 420f, 220f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.2f, 0.98f);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            TMP_Text text = CreateText("ModalText", panel.transform, "Modal active", 24, TextAlignmentOptions.Center);
            text.color = Color.white;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
            CreateButton(panel.transform, "CloseModalButton", "Close", 56f);
            overlay.SetActive(false);
            return overlay;
        }

        private static UrdtUiWindowTarget AddWindowTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string module,
            string commands)
        {
            UrdtUiWindowTarget target = gameObject.AddComponent<UrdtUiWindowTarget>();
            target.ConfigureWindow(
                targetId,
                activeWindow,
                module,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static UrdtUiButtonTarget AddButtonTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            string commands,
            Selectable selectable = null,
            string clickResult = null)
        {
            UrdtUiButtonTarget target = gameObject.AddComponent<UrdtUiButtonTarget>();
            target.ConfigureButton(
                targetId,
                activeWindow,
                activeModule,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                selectable,
                gameObject.GetComponent<RectTransform>(),
                canvas,
                clickResult);
            return target;
        }

        private static UrdtUiToggleTarget AddToggleTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            string commands,
            Toggle toggle = null)
        {
            UrdtUiToggleTarget target = gameObject.AddComponent<UrdtUiToggleTarget>();
            target.ConfigureToggle(
                targetId,
                activeWindow,
                activeModule,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                toggle,
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static UrdtUiSliderTarget AddSliderTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            string commands,
            Slider slider = null)
        {
            UrdtUiSliderTarget target = gameObject.AddComponent<UrdtUiSliderTarget>();
            target.ConfigureSlider(
                targetId,
                activeWindow,
                activeModule,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                slider,
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static UrdtUiInputTarget AddInputTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            string commands,
            TMP_InputField inputField = null)
        {
            UrdtUiInputTarget target = gameObject.AddComponent<UrdtUiInputTarget>();
            target.ConfigureInput(
                targetId,
                activeWindow,
                activeModule,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                inputField,
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static UrdtUiDropdownTarget AddDropdownTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            string commands,
            TMP_Dropdown dropdown = null)
        {
            UrdtUiDropdownTarget target = gameObject.AddComponent<UrdtUiDropdownTarget>();
            target.ConfigureDropdown(
                targetId,
                activeWindow,
                activeModule,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                dropdown,
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static UrdtUiScrollTarget AddScrollTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            string commands,
            ScrollRect scrollRect = null)
        {
            UrdtUiScrollTarget target = gameObject.AddComponent<UrdtUiScrollTarget>();
            target.ConfigureScroll(
                targetId,
                activeWindow,
                activeModule,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                scrollRect,
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static UrdtUiStickTarget AddStickTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            string commands,
            UrdtVirtualStick stick = null)
        {
            UrdtUiStickTarget target = gameObject.AddComponent<UrdtUiStickTarget>();
            target.ConfigureStick(
                targetId,
                activeWindow,
                activeModule,
                module,
                UrdtDebugTarget.ParseCommandString(commands),
                stick,
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static UrdtUiDrawingTarget AddDrawingTarget(
            GameObject gameObject,
            Canvas canvas,
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            UrdtDrawingCanvas drawingCanvas = null)
        {
            UrdtUiDrawingTarget target = gameObject.AddComponent<UrdtUiDrawingTarget>();
            target.ConfigureDrawing(
                targetId,
                activeWindow,
                activeModule,
                module,
                drawingCanvas,
                gameObject.GetComponent<RectTransform>(),
                canvas);
            return target;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.UnassignActions();
            AssetDatabase.DeleteAsset(ROOT_FOLDER + "/URDT_UI_InputActions.asset");
            AssetDatabase.DeleteAsset(UI_INPUT_ACTIONS_PATH);

            if (!AssetDatabase.CopyAsset(DEFAULT_UI_INPUT_ACTIONS_PATH, UI_INPUT_ACTIONS_PATH))
            {
                throw new System.InvalidOperationException("Could not create URDT UI input actions asset.");
            }

            AssetDatabase.ImportAsset(UI_INPUT_ACTIONS_PATH);
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UI_INPUT_ACTIONS_PATH);
            inputModule.actionsAsset = actions;
            inputModule.point = InputActionReference.Create(actions.FindAction("UI/Point", true));
            inputModule.leftClick = InputActionReference.Create(actions.FindAction("UI/Click", true));
            inputModule.rightClick = InputActionReference.Create(actions.FindAction("UI/RightClick", true));
            inputModule.middleClick = InputActionReference.Create(actions.FindAction("UI/MiddleClick", true));
            inputModule.scrollWheel = InputActionReference.Create(actions.FindAction("UI/ScrollWheel", true));
            inputModule.move = InputActionReference.Create(actions.FindAction("UI/Navigate", true));
            inputModule.submit = InputActionReference.Create(actions.FindAction("UI/Submit", true));
            inputModule.cancel = InputActionReference.Create(actions.FindAction("UI/Cancel", true));
            EditorUtility.SetDirty(inputModule);
            AssetDatabase.SaveAssets();
        }

        private static void CreateUrdtHost()
        {
            GameObject hostObject = new GameObject("URDT_ServerHost");
            UrdtServerHost host = hostObject.AddComponent<UrdtServerHost>();
            UrdtTestPoligonBootstrap bootstrap = hostObject.AddComponent<UrdtTestPoligonBootstrap>();
            bootstrap.Configure(host, true, 7777, "urdt-test-poligon");
        }

        private static GameObject CreateLayoutObject(string name, Transform parent, float preferredWidth, float preferredHeight)
        {
            GameObject gameObject = new GameObject(name);
            RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            if (preferredWidth > 0f || preferredHeight > 0f)
            {
                rectTransform.sizeDelta = new Vector2(preferredWidth, preferredHeight);
            }
            LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.preferredHeight = preferredHeight;
            return gameObject;
        }

        private static GameObject CreateStretchObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name);
            RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            Stretch(rectTransform);
            return gameObject;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateStretchObject(name, parent);
            TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            Stretch(rectTransform, 0f, 0f, 0f, 0f);
        }

        private static void Stretch(RectTransform rectTransform, float left, float bottom, float right, float top)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(right, top);
        }

        private sealed class UiSuiteRefs
        {
            public GameObject Window;
            public Button BackButton;
            public Button PrimaryButton;
            public Button ResetButton;
            public Button ModalOpenButton;
            public Toggle Toggle;
            public Slider Slider;
            public TMP_InputField Input;
            public TMP_Dropdown Dropdown;
            public ScrollRect ScrollRect;
        }

        private sealed class SuiteShellRefs
        {
            public GameObject Window;
            public Button BackButton;
        }
    }
}
