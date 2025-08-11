using CaptureTools.Integration;
using CaptureTools.Trace;
using CaptureTools.UI;
using CaptureTools.Utils;
using KSP.UI;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public partial class CaptureTools : MonoBehaviour
    {
        #region Fields

        public static CaptureTools Instance;
        public ICaptureToolsUI ui;
        public BuildAnimation buildAnimation;
        public Tracer trace;
        private bool selfDestruct = false;

        public static string filePath = "Captures";
        public static string FilePath
        {
            get
            {
                string path;

                if (Path.IsPathRooted(filePath))
                    path = filePath;
                else
                    path = Path.Combine(kspRoot, filePath);

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }

        // Serialisation.
        private static string kspRoot;
        private static string pluginDataPath;
        private static string configPath;

        private static int originalTargetFrameRate;
        private static int originalVSyncCount;

        // Camera Tools integration.
        private bool cameraToolsLoaded = false;
        private UnityEngine.Object camToolsInstance;
        private string cameraToolsCameraKey;
        private string cameraToolsRevertKey;

        // Integral UI.
        public bool showClapper = false;
        private static GUIStyle clapperStyle;

        public static KeyCode RecordingKeycode { private set; get; } = KeyCode.F8;

        #endregion

        #region Mono Methods

        internal void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                selfDestruct = true;
            }
            else
            {
                Instance = this;
            }

            // todo: Should these be addons?
            buildAnimation = gameObject.AddComponent<BuildAnimation>();
            trace = gameObject.AddComponent<Tracer>();
        }

        internal void Start()
        {
            if (selfDestruct)
                return;

            if (HighLogic.LoadedScene == GameScenes.LOADING)
                return;

            // Serialisation.

            kspRoot = KSPUtil.ApplicationRootPath;
            pluginDataPath = Path.Combine(kspRoot, "GameData", "CaptureTools", "PluginData");
            configPath = Path.Combine(pluginDataPath, "settings.cfg");
            LoadSettings();

            originalTargetFrameRate = Application.targetFrameRate;
            originalVSyncCount = QualitySettings.vSyncCount;

            CameraToolsCheck();
            BD.BDArmouryCheck();

            // Trace.
            PartRecorder.recordedFrames = 0;
        }

        internal void LateUpdate()
        {
            if (selfDestruct)
                return;

            // Capture.

            if (!useFixedUpdate)
            {
                if (CaptureMulti)
                    UpdateMultiCapture();

                if (CaptureMain)
                    UpdateMainCamera();
            }
        }

        internal void FixedUpdate()
        {
            if (selfDestruct)
                return;

            if (CaptureMain && HighLogic.LoadedSceneIsFlight)
                UpdateMainCameraFixed();

            // Not using FixedUpdate for fixed camera updates anymore. Using WaitForFixedUpdate instead.
        }

        internal void Update()
        {
            if (selfDestruct)
                return;

            if (!differentPlaybackFramerate && playbackFramerate.Value != Timing.captureFramerate)
                playbackFramerate.Value = Timing.captureFramerate;

            // Start main capture keybind.
            if ((Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr)) && Input.GetKeyDown(RecordingKeycode))
            {
                CTDebug.Log("Pressed record button");

                CaptureMain = !CaptureMain;

                if (!ui.Visible && UIMasterController.Instance.IsUIShowing)
                    ui.Visible = false;
            }

            // Stop all capture.

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CaptureMulti = false;
                CaptureMain = false;
            }
        }

        protected void OnGUI()
        {
            DrawMulticamGUI();
            DrawMainCamGUI();

            if (CaptureMain && mainCaptureAudio && audioOnly && showClapper)
                DrawClapper();
        }

        private void DrawClapper()
        {
            if (clapperStyle == null)
            {
                clapperStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 256 * (Screen.height / 540),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            int offset = 5 * (Screen.height / 540);

            GUI.Label(new Rect(0 + offset, 0 + offset, Screen.width, Screen.height), "<color=black>SYNC</color>", clapperStyle);
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "SYNC", clapperStyle);
        }

        internal void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // Main.

            if (selfDestruct)
                return;

            Time.captureFramerate = 0;
            Time.maximumDeltaTime = GameSettings.PHYSICS_FRAME_DT_LIMIT;
            Time.fixedDeltaTime = 0.02f;

            RestoreFramerate();

            // Serialisation.

            SaveSettings();

            // Stop capture.

            if (CaptureMain)
                CaptureMain = false;

            if (CaptureMulti)
                CaptureMulti = false;

            Destroy(buildAnimation);
        }

        #endregion

        #region Serialisation

        // todo: use automatic settings system from Kessler.

        public void SaveSettings()
        {
            if (!Directory.Exists(pluginDataPath))
                Directory.CreateDirectory(pluginDataPath);

            ConfigNode settings = new ConfigNode("CaptureToolsSettings");

            // All
            settings.SetValue("filePath", filePath, true);

            // Capture
            settings.SetValue("captureFramerate", Timing.captureFramerate, true);
            settings.SetValue("differentPlaybackFramerate", differentPlaybackFramerate, true);
            settings.SetValue("playbackFramerate", playbackFramerate, true);
            settings.SetValue("useFixedUpdate", useFixedUpdate, true);
            settings.SetValue("previewOnly", previewOnly, true);
            settings.SetValue("fullRes", fullRes, true);
            settings.SetValue("CRF", CRF, true);
            settings.SetValue("currentPreset", FFmpegPresetLoader.CurrentPreset?.Name ?? "None", true);
            settings.SetValue("presetAuthor", FFmpegPreset.defaultAuthor, true);

            // Main
            settings.SetValue("mainCaptureAudio", mainCaptureAudio, true);
            settings.SetValue("audioOnly", audioOnly, true);
            settings.SetValue("mainSmoothTime", mainSmoothTime, true);
            settings.SetValue("mainAspectRatio", mainAspectRatio, true);
            settings.SetValue("mainHeight", mainHeight, true);
            settings.SetValue("drawUIOnMain", drawUIOnMain, true);
            settings.SetValue("recordingKeycode", RecordingKeycode.ToString(), true);

            // Main smoothing multipliers.
            settings.SetValue("mainPositionSmoothTime", mainPositionSmoothTime, true);
            settings.SetValue("positionSmoothingEnabled", positionSmoothingEnabled, true);
            settings.SetValue("pivotSmoothTime", pivotSmoothTime, true);
            settings.SetValue("panSmoothTime", panSmoothTime, true);
            settings.SetValue("distanceSmoothTime", distanceSmoothTime, true);
            settings.SetValue("mainFOVsmoothTime", mainFOVsmoothTime, true);
            settings.SetValue("transitionalSpeedSmoothTime", transitionalSpeedSmoothTime, true);

            // Multicam
            settings.SetValue("showPreview", showPreview, true);
            settings.SetValue("shipLimit", shipLimit, true);
            settings.SetValue("cameraFov", cameraFov, true);
            settings.SetValue("cameraHeight", cameraHeight, true);
            settings.SetValue("cameraSlide", cameraSlide, true);
            settings.SetValue("cameraDistance", cameraDistance, true);
            settings.SetValue("multicamSmoothing", multicamSmoothing, true);
            settings.SetValue("bdTargetDelay", bdTargetDelay, true);

            // Trace
            settings.SetValue("traceFramerate", Tracer.framerate, true);
            settings.SetValue("traceFrameOfReference", Tracer.frameOfReference.ToString(), true);

            // HDRI
            settings.SetValue("HDRIWidth", HDRI.width, true);
            settings.SetValue("HDRISunBrightness", HDRI.sunBrightness, true);
            settings.SetValue("HDRIHideKerbalsInEditor", HDRI.hideKerbalsInEditor, true);

            // Build animation.
            settings.SetValue("buildPartSpeed", BuildAnimation.buildPartSpeed, true);
            settings.SetValue("buildTime", BuildAnimation.buildTime, true);

            // UI
            settings.SetValue("windowPosition", IMGUI.windowRect.position, true);
            settings.SetValue("toggleUIKeycode", IMGUI.toggleUIKeycode.ToString(), true);

            ConfigNode file = new ConfigNode();
            file.AddNode(settings);
            file.Save(configPath);
        }

        public void LoadSettings()
        {
            if (!File.Exists(configPath))
                return;

            ConfigNode file = ConfigNode.Load(configPath);
            ConfigNode settings = file.GetNode("CaptureToolsSettings");

            // All
            settings.TryGetValue("filePath", ref filePath);
            settings.TryGetValue("captureFramerate", Timing.captureFramerate);
            settings.TryGetValue("playbackFramerate", playbackFramerate);
            settings.TryGetValue("differentPlaybackFramerate", ref differentPlaybackFramerate);
            settings.TryGetValue("useFixedUpdate", ref useFixedUpdate);
            settings.TryGetValue("previewOnly", ref previewOnly);
            settings.TryGetValue("fullRes", ref fullRes);
            settings.TryGetValue("CRF", CRF);

            if (!settings.TryGetValue("presetAuthor", ref FFmpegPreset.defaultAuthor))
                FFmpegPreset.defaultAuthor = Environment.UserName;

            string presetName = string.Empty;
            if (settings.TryGetValue("currentPreset", ref presetName))
                FFmpegPresetLoader.SetCurrentPreset(presetName);

            // Main 
            settings.TryGetValue("mainCaptureAudio", ref mainCaptureAudio);
            settings.TryGetValue("audioOnly", ref audioOnly);
            settings.TryGetValue("mainSmoothTime", ref mainSmoothTime);
            settings.TryGetValue("mainAspectRatio", ref mainAspectRatio);
            settings.TryGetValue("mainHeight", ref mainHeight);
            settings.TryGetValue("drawUIOnMain", ref drawUIOnMain);

            // Main smoothing multipliers.
            settings.TryGetValue("mainPositionSmoothTime", ref mainPositionSmoothTime);
            settings.TryGetValue("positionSmoothingEnabled", ref positionSmoothingEnabled);
            settings.TryGetValue("pivotSmoothTime", ref pivotSmoothTime);
            settings.TryGetValue("panSmoothTime", ref panSmoothTime);
            settings.TryGetValue("distanceSmoothTime", ref distanceSmoothTime);
            settings.TryGetValue("mainFOVsmoothTime", ref mainFOVsmoothTime);
            settings.TryGetValue("transitionalSpeedSmoothTime", ref transitionalSpeedSmoothTime);

            // Multicam
            settings.TryGetValue("showPreview", ref showPreview);
            settings.TryGetValue("shipLimit", ref shipLimit);
            settings.TryGetValue("cameraFov", ref cameraFov);
            settings.TryGetValue("cameraHeight", ref cameraHeight);
            settings.TryGetValue("cameraSlide", ref cameraSlide);
            settings.TryGetValue("cameraDistance", ref cameraDistance);
            settings.TryGetValue("multicamSmoothing", ref multicamSmoothing);
            settings.TryGetValue("bdTargetDelay", ref bdTargetDelay);

            // Trace
            settings.TryGetValue("traceFramerate", ref Tracer.framerate);

            string traceFrameStr = Tracer.frameOfReference.ToString();
            if (settings.TryGetValue("traceFrameOfReference", ref traceFrameStr))
                Tracer.frameOfReference = (Tracer.ReferenceFrame)Enum.Parse(typeof(Tracer.ReferenceFrame), traceFrameStr);

            // HDRI
            settings.TryGetValue("HDRIWidth", ref HDRI.width);
            settings.TryGetValue("HDRISunBrightness", ref HDRI.sunBrightness);
            settings.TryGetValue("HDRIHideKerbalsInEditor", ref HDRI.hideKerbalsInEditor);

            // Build Animation
            settings.TryGetValue("buildPartSpeed", ref BuildAnimation.buildPartSpeed);
            settings.TryGetValue("buildTime", ref BuildAnimation.buildTime);

            // UI
            Vector2 windowPosition = IMGUI.windowRect.position;
            if (settings.TryGetValue("windowPosition", ref windowPosition))
                IMGUI.windowRect.position = windowPosition;

            IMGUI.toggleUIKeycode = TryParseKeycode(IMGUI.toggleUIKeycode, "toggleUIKeycode", settings);
            RecordingKeycode = TryParseKeycode(RecordingKeycode, "recordingKeycode", settings);
        }

        private static KeyCode TryParseKeycode(KeyCode defaultCode, string name, ConfigNode settings)
        {
            try
            {
                string keycodeString = defaultCode.ToString();

                if (settings.TryGetValue(name, ref keycodeString))
                    defaultCode = (KeyCode)Enum.Parse(typeof(KeyCode), keycodeString);

                return defaultCode;
            }
            catch
            {
                CTDebug.LogError("Failed to parse toggleUIKeycode");
                return defaultCode;
            }
        }

        #endregion

        #region Mod Integration

        private void CameraToolsCheck()
        {
            try
            {
                foreach (AssemblyLoader.LoadedAssembly assy in AssemblyLoader.loadedAssemblies)
                {
                    if (assy.assembly.FullName.Contains("CameraTools"))
                    {
                        Type cameraTools = assy.assembly.GetType("CameraTools.CamTools");
                        camToolsInstance = FindObjectOfType(cameraTools);

                        if (camToolsInstance != null)
                        {
                            cameraToolsLoaded = true;

                            FieldInfo cameraKey = cameraTools.GetField("cameraKey", BindingFlags.Public | BindingFlags.Instance);
                            cameraToolsCameraKey = (string)cameraKey.GetValue(camToolsInstance);

                            FieldInfo revertKey = cameraTools.GetField("revertKey", BindingFlags.Public | BindingFlags.Instance);
                            cameraToolsRevertKey = (string)revertKey.GetValue(camToolsInstance);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CTDebug.LogError($"Failed to get Camera Tools keybinds: {e.Message}");
            }
        }

        #endregion
    }
}