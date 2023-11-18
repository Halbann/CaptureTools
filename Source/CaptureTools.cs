using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

using CaptureTools.Integration;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public partial class CaptureTools : MonoBehaviour
    {
        #region Fields

        public static CaptureTools Instance;
        private bool selfDestruct = false;

        public static string filePath = "Captures";
        public string FilePath
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
        private Coroutine autosaveCoroutine;


        // Timing.
        public static float maxDeltaTime;
        private float defaultMaxDeltaTime;
        public bool applyMaxDeltaTime;

        public static float fixedDeltaTime;
        private float defaultFixedDeltaTime;
        public bool applyFixedDeltaTime;

        public static float captureFramerate = 60;
        private int defaultCaptureFramerate;
        public bool applyCaptureFramerate;

        public static float timeScale = 1f;
        public bool applyTimescale;

        private Queue<float> ptrRollingQ = new Queue<float>();
        private float ptrLast;
        private float timeRatio;

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


        // Editor zoom.
        private float lastMiddleMouseClickTime;
        public static float editorZoomSpeed = 0.05f;
        private bool editorZoomScrollLock = false;


        // Camera Tools integration.
        private bool cameraToolsLoaded = false;
        private UnityEngine.Object camToolsInstance;
        private string cameraToolsCameraKey;
        private string cameraToolsRevertKey;

        #endregion

        #region Main

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

            autosaveCoroutine = StartCoroutine(AutosaveCoroutine());


            // Timing defaults.

            maxDeltaTime = Time.maximumDeltaTime;
            defaultMaxDeltaTime = Time.maximumDeltaTime;

            fixedDeltaTime = Time.fixedDeltaTime;
            defaultFixedDeltaTime = Time.fixedDeltaTime;

            //captureFramerate = 30;
            defaultCaptureFramerate = Time.captureFramerate;

            originalTargetFrameRate = Application.targetFrameRate;
            originalVSyncCount = QualitySettings.vSyncCount;

            // Capture.

            //var layers = Enumerable.Range(0, 32).Select(n => LayerMask.LayerToName(n)).ToList();
            //Debug.Log(layers);


            CameraToolsCheck();
            BD.BDArmouryCheck();
            Scatterer.Check();

            // Trace.
            TraceRecorder.recordedFrames = 0;


            // UI.

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);

            AddToolbarButton();

            windowID = GUIUtility.GetControlID(FocusType.Passive);
        }

        internal void LateUpdate()
        {
            if (selfDestruct)
                return;

            // Capture.

            if (!useFixedUpdate)
            {
                if (CaptureMulti)
                {
                    UpdateMultiCaptureFixed();
                    UpdateMultiCapture();
                }

                if (CaptureMain)
                    UpdateMainCamera();
            }

            //if (useFixedUpdate && CaptureMain)
            //    UpdateMainCameraLate();
        }

        internal void FixedUpdate()
        {
            if (selfDestruct)
                return;

            if (CaptureMain && HighLogic.LoadedSceneIsFlight)
                UpdateMainCameraFixed();

            if (useFixedUpdate)
            {
                if (CaptureMulti)
                {
                    UpdateMultiCaptureFixed();
                    UpdateMultiCapture();
                }

                if (CaptureMain)
                    UpdateMainCamera();
            }

            if (capturingTrace)
                TraceFixedUpdate();
        }

        internal void Update()
        {
            if (selfDestruct)
                return;


            // UI

            if (Input.GetKeyDown(toggleUIKeycode))
                ToggleGui();


            // Stop all capture.

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CaptureMulti = false;
                CaptureMain = false;
            }


            // Timing.

            float rtss = Time.realtimeSinceStartup;
            UpdatePTR(rtss, Time.deltaTime);


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


            // Editor zoom.

            UpdateEditorZoom();
        }

        internal void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            // Sound.

            //RemoveSoundSourceEvents();


            // Timing.

            applyCaptureFramerate = false;
            applyFixedDeltaTime = false;
            applyMaxDeltaTime = false;


            // Main.

            if (selfDestruct)
                return;

            RemoveToolbarButton();

            Time.captureFramerate = 0;
            Time.maximumDeltaTime = GameSettings.PHYSICS_FRAME_DT_LIMIT;
            Time.fixedDeltaTime = 0.02f;

            Application.targetFrameRate = originalTargetFrameRate;
            QualitySettings.vSyncCount = originalVSyncCount;


            // Serialisation.

            SaveSettings();

            if (autosaveCoroutine != null)
                StopCoroutine(autosaveCoroutine);
        }

        #endregion

        #region Audio

        /*void FindAudioStuff()
        {
            var mixers = FindObjectsOfType<AudioMixer>();
            var groups = FindObjectsOfType<AudioMixerGroup>();
            var sources = FindObjectsOfType<AudioSource>();
            var listeners = FindObjectsOfType<AudioListener>();

            Debug.Log(mixers);
            Debug.Log(sources);
            
            //mixers.First().SetFloat("pitch", 0.5f);

            var bundle = CaptureToolsAssets.bundle;
            mixer = bundle.LoadAsset<AudioMixer>("Assets/CaptureTimePitch.mixer");
            mixerGroup = bundle.LoadAsset<AudioMixerGroup>("Assets/CaptureTimePitch.mixer");
            var snapshot = bundle.LoadAsset<AudioMixerSnapshot>("Assets/CaptureTimePitch.mixer");

            //var mymixer = Instantiate(mixer);

            foreach (var source in sources)
            {
                source.outputAudioMixerGroup = mixerGroup; 
            }

            mixerAdded = true;
        }

        void RemoveSoundSourceEvents()
        {
            GameEvents.onPartExplode.Remove(OnPartExplode);
            GameEvents.onPartDeCouple.Remove(OnPartDeCouple);
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
            GameEvents.onVesselLoaded.Remove(OnVesselCreate);
            GameEvents.onStageActivate.Remove(OnStageActivate);
            GameEvents.onStageSeparation.Remove(OnStageSeparation);
            GameEvents.onEngineActiveChange.Remove(OnEngineActiveChange);
        }

        void AddSoundSourceEvents()
        {
            GameEvents.onPartExplode.Add(OnPartExplode);
            GameEvents.onPartDeCouple.Add(OnPartDeCouple);
            GameEvents.onVesselCreate.Add(OnVesselCreate);
            GameEvents.onVesselLoaded.Add(OnVesselCreate);
            GameEvents.onStageActivate.Add(OnStageActivate);
            GameEvents.onStageSeparation.Add(OnStageSeparation);
            GameEvents.onEngineActiveChange.Add(OnEngineActiveChange);
        }

        private void OnVesselCreate(Vessel data) =>
            SetMixerGroup();

        private void OnPartDeCouple(Part data) =>
            SetMixerGroup();

        private void OnPartExplode(GameEvents.ExplosionReaction data) =>
            SetMixerGroup();

        private void OnEngineActiveChange(ModuleEngines data) =>
            SetMixerGroup();

        private void OnStageSeparation(EventReport data) =>
            SetMixerGroup();

        private void OnStageActivate(int data) =>
            SetMixerGroup();

        void SetMixerGroup()
        {
            if (!mixerAdded || sourcesWaiting ) return;
            StartCoroutine(SetMixerGroupDelayed());
        }

        IEnumerator SetMixerGroupDelayed()
        {
            sourcesWaiting = true;
            yield return new WaitForEndOfFrame();
            sourcesWaiting = false;

            var sources = FindObjectsOfType<AudioSource>();
            foreach (var source in sources)
            {
                source.outputAudioMixerGroup = mixerGroup; 
            }
        }*/

        private void UpdatePTR(float rtss, float deltaTime)
        {
            //PTR calculation

            if (deltaTime > 0)
                ptrRollingQ.Enqueue( deltaTime / (rtss - ptrLast) );

            ptrLast = rtss;

            while (ptrRollingQ.Count > 60)
            {
                ptrRollingQ.Dequeue();
            }

            if (ptrRollingQ.Count > 0)
            {
                timeRatio = ptrRollingQ.Average();
            }
        }

        #endregion

        #region Clipping Distance

        //void UpdateClipDistance()
        //{
        //    foreach (var cam in FlightCamera.fetch.cameras)
        //        cam.nearClipPlane = nearClipDistance;
        //}

        #endregion

        #region Kerbals

        void HideKerbals()
        {
            var kerbalEVAs = FindObjectsOfType<KerbalEVA>();
            var kerbals = FindObjectsOfType<Kerbal>();
            var renderers = FindObjectsOfType<SkinnedMeshRenderer>();


            //foreach (Kerbal kerbal in kerbals)
            //{
            //    kerbal.ShowHelmet(!kerbal.showHelmet);
            //}

            foreach (KerbalEVA kerbalEVA in kerbalEVAs)
            {
                kerbalEVA.bodyMesh.enabled = false;
                kerbalEVA.helmetMesh.enabled = false;

                var EVArenderers = kerbalEVA.GetComponents<SkinnedMeshRenderer>();

                var EVArenderers2 = kerbalEVA.GetComponentsInChildren<SkinnedMeshRenderer>();

                var helmetObject = kerbalEVA.helmetMesh.gameObject;
                var go = kerbalEVA.gameObject;
                var pgo = kerbalEVA.helmetMesh.gameObject.transform.parent.gameObject;
                var gpgo = pgo.transform.parent.gameObject;
                var ggpgo = gpgo.transform.parent.gameObject;
                //kerbalEVA.gameObject.SetActive(false);
                var head = kerbalEVA.transform.Find("head01").gameObject;
            }
        }

        #endregion

        #region Editor Zoom

        private void UpdateEditorZoom()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            if (Input.GetKey(KeyCode.LeftAlt))
            {
                InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS, "CaptureToolsEditorZoom");
                editorZoomScrollLock = true;

                // Scroll zoom.
                if (Input.GetAxis("Mouse ScrollWheel") != 0)
                {
                    Camera camera = EditorLogic.fetch.editorCamera;

                    if (Input.GetAxis("Mouse ScrollWheel") > 0f) // forward
                    {
                        camera.fieldOfView *= 1 + editorZoomSpeed;
                    }
                    else if (Input.GetAxis("Mouse ScrollWheel") < 0f) // backwards
                    {
                        camera.fieldOfView *= 1 - editorZoomSpeed;
                    }

                    camera.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView, 5f, 90f);

                    var childCameras = camera.GetComponentsInChildren<Camera>().ToList();
                    childCameras.ForEach(c => c.fieldOfView = camera.fieldOfView);
                }
            }
            else if (editorZoomScrollLock)
            {
                InputLockManager.RemoveControlLock("CaptureToolsEditorZoom");
                editorZoomScrollLock = false;
            }

            // Double click middle mouse button to reset zoom.
            if (Input.GetKeyDown(KeyCode.Mouse2))
            {
                if (Time.realtimeSinceStartup - lastMiddleMouseClickTime < 0.5f)
                {
                    Camera camera = EditorLogic.fetch.editorCamera;
                    camera.fieldOfView = 60f;

                    var childCameras = camera.GetComponentsInChildren<Camera>().ToList();
                    childCameras.ForEach(c => c.fieldOfView = camera.fieldOfView);
                }

                lastMiddleMouseClickTime = Time.realtimeSinceStartup;
            }
        }

        #endregion

        #region Serialisation

        // todo: add persistent field attribute

        void SaveSettings()
        {
            if (!Directory.Exists(pluginDataPath))
                Directory.CreateDirectory(pluginDataPath);

            ConfigNode settings = new ConfigNode("CaptureToolsSettings");

            // All
            settings.SetValue("filePath", filePath, true);

            // Capture
            settings.SetValue("captureFramerate", captureFramerate, true);
            settings.SetValue("playbackFramerate", playbackFramerate, true);
            settings.SetValue("useFixedUpdate", useFixedUpdate, true);
            settings.SetValue("previewOnly", previewOnly, true);
            settings.SetValue("fullRes", fullRes, true);
            settings.SetValue("CRF", CRF, true);

            // Main
            settings.SetValue("mainCaptureAudio", mainCaptureAudio, true);
            settings.SetValue("audioOnly", audioOnly, true);
            //settings.SetValue("positionSmoothing", positionSmoothing, true);
            settings.SetValue("mainSmoothTime", mainSmoothTime, true);
            settings.SetValue("mainAspectRatio", mainAspectRatio, true);
            settings.SetValue("mainHeight", mainHeight, true);
            settings.SetValue("drawUIOnMain", drawUIOnMain, true);

            // Main smoothing multipliers.
            settings.SetValue("mainPositionSmoothTime", mainPositionSmoothTime, true);
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

            // Trace
            //settings.SetValue("traceFramerateSlider", traceFramerateSlider, true);
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
            settings.SetValue("windowPosition", windowRect.position, true);
            settings.SetValue("toggleUIKeycode", toggleUIKeycode.ToString(), true);

            ConfigNode file = new ConfigNode();
            file.AddNode(settings);
            file.Save(configPath);
        }

        void LoadSettings()
        {
            if (!File.Exists(configPath))
                return;

            ConfigNode file = ConfigNode.Load(configPath);
            ConfigNode settings = file.GetNode("CaptureToolsSettings");

            // All
            settings.TryGetValue("filePath", ref filePath);

            // Capture
            settings.TryGetValue("captureFramerate", ref captureFramerate);
            settings.TryGetValue("playbackFramerate", ref playbackFramerate);
            settings.TryGetValue("useFixedUpdate", ref useFixedUpdate);
            settings.TryGetValue("previewOnly", ref previewOnly);
            settings.TryGetValue("fullRes", ref fullRes);
            settings.TryGetValue("CRF", ref CRF);

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
            Vector2 windowPosition = windowRect.position;
            if (settings.TryGetValue("windowPosition", ref windowPosition))
                windowRect.position = windowPosition;

            try
            {
                string toggleUIKeycodeString = toggleUIKeycode.ToString();
                if (settings.TryGetValue("toggleUIKeycode", ref toggleUIKeycodeString))
                    toggleUIKeycode = (KeyCode)Enum.Parse(typeof(KeyCode), toggleUIKeycodeString);
            }
            catch
            {
                Debug.LogError("[CaptureTools]: Failed to parse toggleUIKeycode");
            }
        }

        IEnumerator AutosaveCoroutine()
        {
            while (true)
            {
                if (guiEnabled)
                    SaveSettings();

                yield return new WaitForSecondsRealtime(10f);
            }
        }

        #endregion

        #region Mod Integration

        private void CameraToolsCheck()
        {
            try
            {
                foreach (var assy in AssemblyLoader.loadedAssemblies)
                {
                    if (assy.assembly.FullName.Contains("CameraTools"))
                    {
                        var cameraTools = assy.assembly.GetType("CameraTools.CamTools");
                        camToolsInstance = FindObjectOfType(cameraTools);

                        if (camToolsInstance != null)
                        {
                            cameraToolsLoaded = true;

                            var cameraKey = cameraTools.GetField("cameraKey", BindingFlags.Public | BindingFlags.Instance);
                            cameraToolsCameraKey = (string)cameraKey.GetValue(camToolsInstance);

                            var revertKey = cameraTools.GetField("revertKey", BindingFlags.Public | BindingFlags.Instance);
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

