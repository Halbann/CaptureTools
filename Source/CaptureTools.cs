using CaptureTools.Integration;
using CaptureTools.UI;
using KSP.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using CaptureTools.Utils;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public partial class CaptureTools : MonoBehaviour
    {
        #region Fields

        public static CaptureTools Instance;
        public ICaptureToolsUI ui;
        public static Timing timing = new Timing();
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

        // Sound.
        //private bool fixSound;
        //private AudioMixer mixer;
        //private AudioMixerGroup mixerGroup;
        //private int sourcesCount;
        //private bool sourcesWaiting;
        //private bool mixerAdded = false;

        //private bool pausePhysics;
        //private float nearClipDistance = 0;




        // Camera Tools integration.
        private bool cameraToolsLoaded = false;
        private UnityEngine.Object camToolsInstance;
        private string cameraToolsCameraKey;
        private string cameraToolsRevertKey;

        // Integral UI.
        public bool showClapper = false;
        private static GUIStyle clapperStyle;

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
            TraceRecorder.recordedFrames = 0;
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

            if (capturingTrace)
                TraceFixedUpdate();
        }

        internal void Update()
        {
            if (selfDestruct)
                return;

            bool alt = Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);

            // Start main capture keybind.
            if ((Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr)) && Input.GetKeyDown(toggleUIKeycode))
            {
                Debug.Log("[CaptureTools]: Pressed record button");

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

            timing.Update();

            // Sound.

            /*if (fixSound)
            {
                if (mixer)
                {
                    mixer.SetFloat("pitch", timeRatio);
                }

                //var sources = FindObjectsOfType<AudioSource>();
                //if (sources.Length != sourcesCount)
                //{
                //    sourcesCount = sources.Length;

                //    for (int i = 0; i < sources.Length; i++)
                //    {
                //        sources[i].outputAudioMixerGroup = mixerGroup;
                //    }
                //}
            }*/
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
                clapperStyle = new GUIStyle(GUI.skin.label);
                clapperStyle.fontSize = 256 * (Screen.height / 540);
                clapperStyle.fontStyle = FontStyle.Bold;
                clapperStyle.alignment = TextAnchor.MiddleCenter;
            }

            int offset = 5 * (Screen.height / 540);

            GUI.Label(new Rect(0 + offset, 0 + offset, Screen.width, Screen.height), "<color=black>SYNC</color>", clapperStyle);
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "SYNC", clapperStyle);
        }

        internal void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // Sound.

            //RemoveSoundSourceEvents();


            // Timing.

            timing.Reset();


            // Main.

            if (selfDestruct)
                return;

            Time.captureFramerate = 0;
            Time.maximumDeltaTime = GameSettings.PHYSICS_FRAME_DT_LIMIT;
            Time.fixedDeltaTime = 0.02f;

            RestoreFramerate();


            // Serialisation.

            SaveSettings();

            if (autosaveCoroutine != null)
                StopCoroutine(autosaveCoroutine);


            // Stop capture.

            if (CaptureMain)
                CaptureMain = false;

            if (CaptureMulti)
                CaptureMulti = false;
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
            settings.SetValue("captureFramerate", captureFramerate, true);
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
            settings.SetValue("traceFramerate", traceFramerate, true);
            settings.SetValue("traceFrameOfReference", traceFrameOfReference.ToString(), true);

            // HDRI
            settings.SetValue("HDRIWidth", HDRIWidth, true);
            settings.SetValue("HDRISunBrightness", HDRISunBrightness, true);
            settings.SetValue("HDRIHideKerbalsInEditor", HDRIHideKerbalsInEditor, true);

            // Build animation.
            settings.SetValue("buildPartSpeed", buildPartSpeed, true);
            settings.SetValue("buildTime", buildTime, true);

            // UI
            settings.SetValue("windowPosition", ui.windowRect.position, true);
            settings.SetValue("toggleUIKeycode", ui.toggleUIKeycode.ToString(), true);

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
            settings.TryGetValue("captureFramerate", captureFramerate);
            settings.TryGetValue("playbackFramerate", playbackFramerate);
            settings.TryGetValue("differentPlaybackFramerate", ref differentPlaybackFramerate);
            settings.TryGetValue("useFixedUpdate", ref useFixedUpdate);
            settings.TryGetValue("previewOnly", ref previewOnly);
            settings.TryGetValue("fullRes", ref fullRes);
            settings.TryGetValue("CRF", ref CRF);

            if (!settings.TryGetValue("presetAuthor", ref FFmpegPreset.defaultAuthor))
                FFmpegPreset.defaultAuthor = Environment.UserName;

            string presetName = string.Empty;
            if (settings.TryGetValue("currentPreset", ref presetName))
                FFmpegPresetLoader.SetCurrentPreset(presetName);

            // Main 
            settings.TryGetValue("mainCaptureAudio", ref mainCaptureAudio);
            settings.TryGetValue("audioOnly", ref audioOnly);
            //settings.TryGetValue("positionSmoothing", ref positionSmoothing);
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
            //settings.TryGetValue("traceFramerateSlider", ref traceFramerateSlider);
            settings.TryGetValue("traceFramerate", ref traceFramerate);

            string traceFrameStr = traceFrameOfReference.ToString();
            if (settings.TryGetValue("traceFrameOfReference", ref traceFrameStr))
                traceFrameOfReference = (TraceFrame)Enum.Parse(typeof(TraceFrame), traceFrameStr);


            // HDRI
            settings.TryGetValue("HDRIWidth", ref HDRIWidth);
            settings.TryGetValue("HDRISunBrightness", ref HDRISunBrightness);
            settings.TryGetValue("HDRIHideKerbalsInEditor", ref HDRIHideKerbalsInEditor);

            // Build Animation
            settings.TryGetValue("buildPartSpeed", ref buildPartSpeed);
            settings.TryGetValue("buildTime", ref buildTime);

            // UI
            Vector2 windowPosition = ui.windowRect.position;
            if (settings.TryGetValue("windowPosition", ref windowPosition))
                ui.windowRect.position = windowPosition;

            try
            {
                string toggleUIKeycodeString = ui.toggleUIKeycode.ToString();
                if (settings.TryGetValue("toggleUIKeycode", ref toggleUIKeycodeString))
                    ui.toggleUIKeycode = (KeyCode)Enum.Parse(typeof(KeyCode), toggleUIKeycodeString);
            }
            catch
            {
                Debug.LogError("[CaptureTools]: Failed to parse toggleUIKeycode");
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
                Debug.LogError($"[CaptureTools]: Failed to get Camera Tools keybinds: {e.Message}");
            }
        }

        #endregion
    }
}

