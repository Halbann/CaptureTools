using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using CaptureTools.Utils;
using CaptureTools.Trace;

namespace CaptureTools.UI
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class IMGUI : MonoBehaviour, ICaptureToolsUI
    {
        public CaptureTools ct;
        public bool Visible { get; set; }

        // Settings/constants.
        public static int windowWidth = 440;
        public static float timingSliderSpace = 12f;
        public static float sliderWidth = 220f;
        public static int entryHeight = 26;
        public static int maxVisibleEntries = 5;
        public static string presetAuthorNameColour = "grey";
        public static string windowTitle = $"{Meta.name} {Meta.version}";
        public static KeyCode toggleUIKeycode = KeyCode.F8;

        // Public/user state.
        public static Rect windowRect = new Rect(Screen.width * 0.05f, Screen.height * 0.1f, 0, 0);
        public static bool guiEnabled = false;
        public static UISection currentSection = UISection.Capture;
        public static bool showMainCameraSection = false;
        public static bool showMainSmoothingSection = false;
        public static bool showMulticamSection = false;
        public static bool showEncodingSection = false;
        public static bool showPresetsList = false;
        public static Vector2 presetsScrollPosition;

        // Private state.
        private readonly StringBuilder sb = new StringBuilder();
        private readonly List<FFmpegPreset> presetsSorted = new List<FFmpegPreset>();
        private int windowID;
        private bool guiHidden = false;
        private bool loading = false;
        private bool registeredEvents = false;
        private SaveAs saveAs = null;
        private ApplicationLauncherButton appLauncherButton;

        // Capture.
        private readonly SettingSlider captureFramerateSlider = new SettingSlider { name = "Capture Frame Rate", min = 24, max = 120, rounding = 0, softClamp = true };

        // Main Smoothing.
        private readonly SettingSlider smoothingSlider = new SettingSlider { name = "Smoothing", min = 0, max = 5f, rounding = 2 };
        private readonly SettingSlider posSmoothingSlider = new SettingSlider { name = "Position", min = 0, max = 2f, rounding = 2, useToggle = true };
        private readonly SettingSlider pivotSmoothingSlider = new SettingSlider { name = "Pivot", min = 0, max = 2f, rounding = 2 };
        private readonly SettingSlider panSmoothingSlider = new SettingSlider { name = "Pan", min = 0, max = 2f, rounding = 2 };
        private readonly SettingSlider distanceSmoothingSlider = new SettingSlider { name = "Distance", min = 0, max = 2f, rounding = 2 };
        private readonly SettingSlider zoomSmoothingSlider = new SettingSlider { name = "Zoom", min = 0, max = 2f, rounding = 2 };

        // Multicam.
        private readonly SettingSlider shipLimitSlider = new SettingSlider { name = "Ship Limit", min = 1, max = 16, rounding = 0 };
        private readonly SettingSlider cameraFovSlider = new SettingSlider { name = "Camera FOV", min = 2, max = 90, rounding = 1 };
        private readonly SettingSlider cameraHeightSlider = new SettingSlider { name = "Camera Height", min = 0, max = 20, rounding = 1 };
        private readonly SettingSlider cameraSlideSlider = new SettingSlider { name = "Camera Slide", min = 0, max = 20, rounding = 1 };
        private readonly SettingSlider cameraDistanceSlider = new SettingSlider { name = "Camera Distance", min = 10, max = 200, rounding = 1 };
        private readonly SettingSlider targetDelaySlider = new SettingSlider { name = "Target Delay", min = 0, max = 5, rounding = 1 };
        private readonly SettingSlider multicamSmoothingSlider = new SettingSlider { name = "Smoothing", min = 0, max = 5, rounding = 2 };

        // Trace.
        private readonly SettingSlider traceFramerateSlider = new SettingSlider { name = "Trace Frame Rate", min = 1, max = 50, rounding = 0 };

        // Animation.
        private readonly SettingSlider buildTimeSlider = new SettingSlider { name = "Build Duration", min = 1, max = 60, rounding = 2 };
        private readonly SettingSlider buildPartSpeedSlider = new SettingSlider { name = "Move Time", min = 0, max = 3f, rounding = 2 };

        // Encoding.
        private readonly SettingSlider crfSlider = new SettingSlider { name = "CRF", min = 10, max = 30, rounding = 0, softClamp = true };
        private readonly SettingSlider playbackFramerateSlider = new SettingSlider { name = "Playback Frame Rate", min = 24, max = 120, rounding = 0, softClamp = true, useToggle = true };

        // Timing.
        private readonly SettingSlider captureFramerateSliderToggle = new SettingSlider { name = "Capture Frame Rate", min = 24, max = 120, rounding = 0, softClamp = true, useToggle = true };
        private readonly SettingSlider timescaleSlider = new SettingSlider { name = "Time Scale", rounding = 3, useToggle = true };
        private readonly SettingSlider maxDeltaTimeSlider = new SettingSlider { name = "Max Delta Time", min = 0.02f, max = 0.1f, rounding = 2, useToggle = true };
        private readonly SettingSlider fixedDeltaTimeSlider = new SettingSlider { name = "Fixed Delta Time", min = 0.02f, max = 0.1f, rounding = 2, useToggle = true };

        // HDRI.
        private readonly SettingSlider sunBrightnessSlider = new SettingSlider { name = "Sun Brightness", min = 0, max = 500, rounding = 1 };


        public enum UISection
        {
            Capture,
            Trace,
            Timing,
            Physics,
            Animation,
            HDRI,
        }

        #region Mono Methods

        protected void Start()
        {
            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneRequested);
            GameEvents.onLevelWasLoaded.Add(OnSceneLoaded);

            AddToolbarButton();

            windowID = GUIUtility.GetControlID(FocusType.Passive);
            StartCoroutine(AutosaveCoroutine());
        }

        protected void Update()
        {
            if (!(Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr)) && Input.GetKeyDown(toggleUIKeycode))
                ToggleGui();
        }

        internal void OnGUI()
        {
            if (guiEnabled && !guiHidden && !loading)
            {
                Styles.Init();
                windowRect = GUILayout.Window(windowID, windowRect, FillWindow, windowTitle, GUILayout.Height(1), GUILayout.Width(windowWidth));
            }
        }

        protected void OnDestroy()
        {
            RemoveToolbarButton();

            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneRequested);
            GameEvents.onLevelWasLoaded.Remove(OnSceneLoaded);
        }

        #endregion

        #region FillWindow and Sections

        private void FillWindow(int windowID)
        {
            if (guiHidden)
                return;

            this.ct = CaptureTools.Instance;

            if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), ""))
                ToggleGui();

            if (GUI.Button(new Rect(windowRect.width - (18 * 2), 2, 16, 16), "\\", Styles.smallTextButtonStyle))
                Application.OpenURL(Path.GetFullPath(CaptureTools.FilePath));

            if (GUI.Button(new Rect(windowRect.width - (18 * 3), 2, 16, 16), "?", Styles.smallTextButtonStyle))
            {
                string wikiURL = "https://github.com/Halbann/CaptureTools/wiki/";
                Dictionary<UISection, string> sectionPageMap = new Dictionary<UISection, string>
                {
                    { UISection.Capture, "Capture" },
                    { UISection.Trace, "Trace" },
                    { UISection.Physics, "Physics" },
                    { UISection.Animation, "Animation" },
                    { UISection.HDRI, "HDRI" }
                    // note: no timing section.
                };

                if (sectionPageMap.TryGetValue(currentSection, out string page))
                    wikiURL += page;

                Application.OpenURL(wikiURL);
            }

            GUILayout.BeginHorizontal();
            string[] sections = Enum.GetNames(typeof(UISection));
            // todo: use GUILayout.Toolbar
            currentSection = (UISection)GUILayout.SelectionGrid((int)currentSection, sections, sections.Length, GUILayout.Width(windowWidth));
            GUILayout.EndHorizontal();

            switch (currentSection)
            {
                case UISection.Capture:
                    CaptureSection();
                    break;
                case UISection.Trace:
                    TraceSection();
                    break;
                case UISection.Timing:
                    TimingSection();
                    break;
                case UISection.Physics:
                    PhysicsSection();
                    break;
                case UISection.Animation:
                    AnimationSection();
                    break;
                case UISection.HDRI:
                    HDRISection();
                    break;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 500));

            // Keep window inside screen space.
            windowRect.position = new Vector2(Mathf.Clamp(windowRect.position.x, 0, Screen.width - windowRect.width), Mathf.Clamp(windowRect.position.y, 0, Screen.height - windowRect.height));
        }

        private void CaptureSection()
        {
            GUILayout.BeginVertical(Styles.boxStyle);
            GUIEnabled.Push(!ct.CaptureMain && !ct.CaptureMulti);

            FramerateSlider(captureFramerateSlider, Timing.captureFramerate);

            // Frame of reference.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sync: ");
            string[] syncStrings = new string[] { "Physics", "Rendering" };
            CaptureTools.useFixedUpdate = GUILayout.SelectionGrid(CaptureTools.useFixedUpdate ? 0 : 1, syncStrings, 2) == 0;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            CaptureTools.fullRes = GUILayout.Toggle(CaptureTools.fullRes, "Use Screen Resolution");
            CaptureTools.previewOnly = GUILayout.Toggle(CaptureTools.previewOnly, "Preview Only");

            GUIEnabled.Pop();

            CaptureTools.showPreview = GUILayout.Toggle(CaptureTools.showPreview, "Show Preview");

            if (SectionButton("Encoding", ref showEncodingSection))
            {
                GUILayout.BeginVertical(Styles.boxStyle);

                // todo: move this out of UI.
                if (!registeredEvents)
                {
                    void OnPresetsChanged()
                    {
                        presetsSorted.Clear();
                        presetsSorted.AddRange(FFmpegPresetLoader.presets.Values.OrderBy(p => p.Name));
                    }

                    FFmpegPresetLoader.OnPresetChanged += OnPresetsChanged;
                    OnPresetsChanged();
                    registeredEvents = true;
                }

                FFmpegPreset current = FFmpegPresetLoader.CurrentPreset;

                FramerateSlider(playbackFramerateSlider, CaptureTools.playbackFramerate, ref CaptureTools.differentPlaybackFramerate);

                CaptureTools.CRF.Value = crfSlider.Update(CaptureTools.CRF.Value);

                // todo: this if statement feels dumb.
                if (saveAs != null && !saveAs.complete)
                {
                    saveAs.Update();
                }
                else
                {
                    GUILayout.BeginHorizontal();

                    // Name.
                    SectionButton(current.Name, ref showPresetsList);

                    GUIEnabled.Push(current != null);
                    bool edited = current.Editable && current.Modified;

                    // Save.
                    GUIEnabled.Push(edited);
                    if (GUILayout.Button("Save", GUILayout.Width(ContentSizeCache.Size("Save", Styles.buttonStyle))) && edited)
                        current.SaveToFile();
                    GUIEnabled.Pop(); // End of save button.

                    // Save as.
                    if (GUILayout.Button("Save As", GUILayout.Width(ContentSizeCache.Size("Save As", Styles.buttonStyle))))
                    {
                        saveAs = new SaveAs(
                            s =>
                            {
                                FFmpegPreset newPreset = new FFmpegPreset(s, current.Command, FFmpegPreset.defaultAuthor);
                                if (FFmpegPresetLoader.AddPreset(newPreset))
                                {
                                    newPreset.SaveToFile();
                                    FFmpegPresetLoader.SetCurrentPreset(s);
                                }

                                saveAs = null;
                            },
                            () => saveAs = null,
                            current.Name,
                            s => !FFmpegPresetLoader.presets.ContainsKey(s));
                    }

                    // Reload.
                    bool reloadable = (current.Editable && current.Modified) || !current.Editable;
                    GUIEnabled.Push(reloadable);
                    if (GUILayout.Button("Reload", GUILayout.Width(ContentSizeCache.Size("Reload", Styles.buttonStyle))) && reloadable)
                        current.LoadFromFile();
                    GUIEnabled.Pop();

                    // Delete.
                    GUIEnabled.Push(current.Editable && presetsSorted.Count > 1);
                    if (GUILayout.Button("Delete", GUILayout.Width(ContentSizeCache.Size("Delete", Styles.buttonStyle))) && current.Editable && presetsSorted.Count > 1)
                    {
                        if (presetsSorted.Contains(current))
                        {
                            List<FFmpegPreset> list = presetsSorted;
                            int index = (list.IndexOf(current) - 1 + list.Count) % list.Count;
                            FFmpegPresetLoader.SetCurrentPreset(list[index].Name);
                        }

                        current.DeleteFile();
                    }
                    GUIEnabled.Pop(); // End of delete button.

                    GUIEnabled.Pop(); // End of all buttons.
                    GUILayout.EndHorizontal();
                }

                if (showPresetsList)
                {
                    GUILayout.BeginVertical(Styles.boxStyle);
                    presetsScrollPosition = GUILayout.BeginScrollView(presetsScrollPosition,
                        GUILayout.Height(Mathf.Min(presetsSorted.Count * entryHeight, entryHeight * maxVisibleEntries)));

                    foreach (FFmpegPreset preset in presetsSorted)
                    {
                        GUILayout.BeginHorizontal();

                        if (GUILayout.Button(GetPresetTitle(preset)))
                        {
                            FFmpegPresetLoader.SetCurrentPreset(preset.Name);
                            showPresetsList = false;
                        }

                        if (!string.IsNullOrEmpty(preset.path) && GUILayout.Button("\\", GUILayout.Width(16)))
                            Application.OpenURL(preset.path);

                        GUILayout.EndHorizontal();
                    }

                    GUILayout.EndScrollView();
                    GUILayout.EndVertical();
                }

                GUIEnabled.Push(current.Editable);
                FFmpegPresetLoader.CurrentPreset.Command = GUILayout.TextArea(FFmpegPresetLoader.CurrentPreset.Command, GUILayout.ExpandHeight(true));
                GUIEnabled.Pop();

                GUILayout.BeginHorizontal();

                const string ffmpegURL = "https://www.ffmpeg.org/ffmpeg-all.html";
                if (GUILayout.Button("Open FFmpeg Docs"))
                    Application.OpenURL(ffmpegURL);

                const string encoderBatURL = "GameData\\CaptureTools\\FFmpeg\\Windows\\getInfoAboutEncoders.bat";
                if (GUILayout.Button("Get Encoder Info") && File.Exists(encoderBatURL))
                    Application.OpenURL(encoderBatURL);

                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            if (SectionButton("Main Camera", ref showMainCameraSection))
            {
                GUILayout.BeginVertical(Styles.boxStyle);

                if (RecordButton(ct.CaptureMain, ct.MainCaptureStartFrame, CaptureTools.playbackFramerate, CaptureTools.previewOnly))
                    ct.CaptureMain = !ct.CaptureMain;

                GUILayout.BeginHorizontal();

                if (!ct.CaptureMain)
                {
                    CaptureTools.mainCaptureAudio = GUILayout.Toggle(CaptureTools.mainCaptureAudio, "Capture Audio");

                    if (CaptureTools.mainCaptureAudio)
                        CaptureTools.audioOnly = GUILayout.Toggle(CaptureTools.audioOnly, "Audio Only");

                    CaptureTools.drawUIOnMain = GUILayout.Toggle(CaptureTools.drawUIOnMain, "Draw UI");
                }

                GUILayout.EndHorizontal();

                // We can't change these settings while recording.
                if (!ct.CaptureMain && !CaptureTools.fullRes)
                {
                    // Resolution.

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(3);

                    GUILayout.Label("Resolution", GUILayout.Width(70));

                    string restext = GUILayout.TextField(CaptureTools.mainHeight.ToString(), GUILayout.Width(38));
                    if (int.TryParse(restext, out int result2))
                        CaptureTools.mainHeight = result2;
                    else if (restext?.Length == 0)
                        CaptureTools.mainHeight = 1080;

                    GUILayout.FlexibleSpace();

                    // Aspect Ratio.

                    GUILayout.Label("Aspect Ratio");
                    GUILayout.Space(5);

                    void DrawAspectField(ref float value)
                    {
                        string text = GUILayout.TextField(value.ToString(), GUILayout.Width(38));
                        if (float.TryParse(text, out float result))
                            value = result;
                        else if (text?.Length == 0)
                            value = 1;
                    }

                    DrawAspectField(ref CaptureTools.mainAspectRatio.x);
                    DrawAspectField(ref CaptureTools.mainAspectRatio.y);

                    GUILayout.Space(3);
                    GUILayout.EndHorizontal();
                }

                smoothingSlider.Update(ref CaptureTools.mainSmoothTime);

                showMainSmoothingSection = GUILayout.Toggle(showMainSmoothingSection,
                    "Smoothing Multipliers", Styles.buttonStyle);

                if (showMainSmoothingSection)
                {
                    GUILayout.BeginVertical(Styles.boxStyle);

                    posSmoothingSlider.Update(ref CaptureTools.mainPositionSmoothTime, ref CaptureTools.positionSmoothingEnabled);
                    pivotSmoothingSlider.Update(ref CaptureTools.pivotSmoothTime);
                    panSmoothingSlider.Update(ref CaptureTools.panSmoothTime);
                    distanceSmoothingSlider.Update(ref CaptureTools.distanceSmoothTime);
                    zoomSmoothingSlider.Update(ref CaptureTools.mainFOVsmoothTime);

                    GUILayout.EndVertical();
                }

                GUILayout.EndVertical();
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (SectionButton("Multicam", ref showMulticamSection))
                {
                    GUILayout.BeginVertical(Styles.boxStyle);

                    if (RecordButton(ct.CaptureMulti, ct.MulticamCaptureStartFrame, CaptureTools.playbackFramerate, CaptureTools.previewOnly))
                        ct.CaptureMulti = !ct.CaptureMulti;

                    shipLimitSlider.Update(ref CaptureTools.shipLimit);
                    cameraFovSlider.Update(ref CaptureTools.cameraFov);
                    cameraHeightSlider.Update(ref CaptureTools.cameraHeight);
                    cameraSlideSlider.Update(ref CaptureTools.cameraSlide);
                    cameraDistanceSlider.Update(ref CaptureTools.cameraDistance);
                    targetDelaySlider.Update(ref CaptureTools.bdTargetDelay);
                    multicamSmoothingSlider.Update(ref CaptureTools.multicamSmoothing);

                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndVertical();
        }

        private void TraceSection()
        {
            GUILayout.BeginVertical(Styles.boxStyle);
            GUIEnabled.Push(HighLogic.LoadedSceneIsFlight);

            // Record button.
            if (RecordButton(ct.trace.enabled, ct.trace.StartTime, false))
                ct.trace.enabled = !ct.trace.enabled;

            // File size.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Est. File Size: ");

            if (PartRecorder.recordedFrames == 0)
            {
                GUILayout.Label("0 MB");
            }
            else
            {
                float estimatedFileSizeMB = (0.06947368421f * PartRecorder.recordedFrames) / 1000f;
                if (GUILayout.Button($"{estimatedFileSizeMB:N1} MB") && !ct.trace.enabled)
                    PartRecorder.recordedFrames = 0;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (!ct.trace.enabled)
            {
                // Frame of reference.
                GUILayout.BeginHorizontal();
                GUILayout.Label("Frame of Reference: ");
                string[] frameStrings = Enum.GetNames(typeof(Tracer.ReferenceFrame));
                Tracer.frameOfReference = (Tracer.ReferenceFrame)GUILayout.SelectionGrid((int)Tracer.frameOfReference, frameStrings, frameStrings.Length);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                // Framerate.
                traceFramerateSlider.Update(ref Tracer.framerate);
            }

            GUIEnabled.Pop();
            GUILayout.EndVertical();
        }

        private void TimingSection()
        {
            GUILayout.BeginVertical(Styles.boxStyle);

            (Timing.maxDeltaTime.constrained.Value, Timing.maxDeltaTime.Apply) = maxDeltaTimeSlider.Update(Timing.maxDeltaTime, Timing.maxDeltaTime.Apply);
            (Timing.fixedDeltaTime.constrained.Value, Timing.fixedDeltaTime.Apply) = fixedDeltaTimeSlider.Update(Timing.fixedDeltaTime, Timing.fixedDeltaTime.Apply);

            (float framerateValue, bool apply) = captureFramerateSliderToggle.Update(Timing.captureFramerate, Timing.captureFramerate.Apply);
            Timing.captureFramerate.constrained.Value = RoundFramerate(framerateValue);
            Timing.captureFramerate.Apply = apply;

            (Timing.timeScale.constrained.Value, Timing.timeScale.Apply) = timescaleSlider.Update(Timing.timeScale, Timing.timeScale.Apply);

            GUILayout.Label($"Physics Delta: {Math.Round(Time.fixedDeltaTime, 4)}");
            GUILayout.Label($"Frame Delta: {Math.Round(Time.deltaTime, 4)}");
            GUILayout.Label($"Time Scale: {Math.Round(Time.timeScale, 2)}");
            GUILayout.Label($"Time Ratio: {Math.Round(Timing.Instance.TimeRatio, 2)}");

            GUILayout.EndVertical();
        }

        private void PhysicsSection()
        {
            GUILayout.BeginVertical(Styles.boxStyle);

            /*GUILayout.BeginHorizontal();

            bool prevPausePhysics = pausePhysics;
            pausePhysics = GUILayout.Toggle(pausePhysics, "Pause Physics");

            if (prevPausePhysics != pausePhysics)
                Physics.autoSimulation = !pausePhysics;

            if (GUILayout.Button("Step Physics"))
                Physics.Simulate(Time.fixedDeltaTime);

            GUILayout.EndHorizontal();*/

            GUILayout.BeginVertical(Styles.boxStyle);
            GUILayout.Label("Physics updates don't always synchronise with the framerate or camera movement. This can appear as stuttering." +
                " Activate interpolation only if you notice stuttering, or use with Time Scale for smooth physics-locked slow motion.");
            GUILayout.EndVertical();

            if (FlightGlobals.ActiveVessel?.parts?.FirstOrDefault() != null)
            {
                RigidbodyInterpolation currentInterp = FlightGlobals.ActiveVessel.parts[0].rb.interpolation;
                GUILayout.Label($"Interpolation Mode: {currentInterp}");
            }


            if (GUILayout.Button("Interpolate Rigidbodies"))
            {
                Rigidbody[] rbs = FindObjectsOfType<Rigidbody>();
                if (rbs[0].interpolation == RigidbodyInterpolation.None)
                    rbs.ToList().ForEach(rb => rb.interpolation = RigidbodyInterpolation.Interpolate);
                else if (rbs[0].interpolation == RigidbodyInterpolation.Interpolate)
                    rbs.ToList().ForEach(rb => rb.interpolation = RigidbodyInterpolation.Extrapolate);
                else
                    rbs.ToList().ForEach(rb => rb.interpolation = RigidbodyInterpolation.None);

                CTDebug.Log($"Set interpolation of all Rigidbodies to {rbs[0].interpolation}.");
            }

            GUILayout.EndVertical();
        }

        private void AnimationSection()
        {
            GUILayout.BeginVertical(Styles.boxStyle);

            GUILayout.BeginVertical(Styles.boxStyle);
            GUILayout.Label("This plays a very simple building animation for the current vessel. " +
                "Can only be run while the game is paused or time is frozen.");
            GUILayout.EndVertical();

            bool disableGUI = Time.timeScale != 0f && !ct.buildAnimation.Playing;
            GUIEnabled.Push(!disableGUI);

            bool building = ct.buildAnimation.Playing;
            if (GUILayout.Button(building ? "Stop" : "Play"))
                ct.buildAnimation.Playing = !building;

            GUIEnabled.Pop();

            buildTimeSlider.Update(ref BuildAnimation.buildTime);
            buildPartSpeedSlider.Update(ref BuildAnimation.buildPartSpeed);

            GUILayout.EndVertical();
        }

        private void HDRISection()
        {
            GUILayout.BeginVertical(Styles.boxStyle);

            GUILayout.BeginVertical(Styles.boxStyle);
            GUILayout.Label("Capture a high dynamic range image (HDRI) from the" +
                " game camera as an EXR for use in programs like Blender." +
                " This can take several seconds depending on the size of the image.");
            GUILayout.EndVertical();


            // Setting slider for powers of two.

            GUILayout.BeginHorizontal();
            GUILayout.Space(3);
            GUILayout.Label("Size", GUILayout.Width(70));
            HDRI.widthPower = Mathf.RoundToInt(Mathf.Log(HDRI.width, 2));
            HDRI.widthPower = Mathf.RoundToInt(GUILayout.HorizontalSlider(HDRI.widthPower, 8, 12f));
            HDRI.width = Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(Mathf.Pow(2f, HDRI.widthPower)));

            // Box
            string text = GUILayout.TextField(HDRI.width.ToString(), Styles.textBoxStyle, GUILayout.Width(38));
            if (int.TryParse(text, out int result))
                HDRI.width = result;
            else if (text?.Length == 0)
                HDRI.width = 2048;

            GUILayout.Space(3);
            GUILayout.EndHorizontal();

            sunBrightnessSlider.Update(ref HDRI.sunBrightness);
            HDRI.hideKerbalsInEditor = GUILayout.Toggle(HDRI.hideKerbalsInEditor, "Hide Kerbals in Editor");

            // Capture HDRI.

            if (GUILayout.Button("Capture HDRI"))
                HDRI.Capture();

            GUILayout.EndVertical();
        }

        #endregion

        #region UI Elements

        private static bool SectionButton(string name, ref bool value)
        {
            value = GUILayout.Toggle(value, name, Styles.buttonStyle);
            return value;
        }

        private bool RecordButton(bool recording, int startFrame, int framerate, bool preview)
        {
            float recordingTime = (Time.frameCount - startFrame) / (float)framerate;
            return RecordButtonInternal(recording, recordingTime, preview);
        }

        private bool RecordButton(bool recording, float startTime, bool preview)
        {
            float recordingTime = Time.time - startTime;
            return RecordButtonInternal(recording, recordingTime, preview);
        }

        private bool RecordButtonInternal(bool recording, float recordingTime, bool preview)
        {
            float recordingTimeMinutes = Mathf.Floor(recordingTime / 60);
            float recordingTimeSeconds = recordingTime % 60;

            sb.Clear();

            if (!recording)
            {
                sb.Append("○ Record");
            }
            else
            {
                if (!preview)
                    sb.Append($"<color=red>●</color> <b>{recordingTimeMinutes:00}:{recordingTimeSeconds:00}</b>");
                else
                    sb.Append("<color=white>●</color> <b>PREVIEW</b>");

                sb.Append($" <color=grey>({Mathf.RoundToInt(Timing.Instance.TimeRatio * 100)}%)</color>");
            }

            return GUILayout.Button(sb.ToString());
        }

        private void FramerateSlider(SettingSlider slider, Constrained framerate) =>
            framerate.Value = RoundFramerate(slider.Update(framerate));

        private void FramerateSlider(SettingSlider slider, Constrained framerate, ref bool toggle)
        {
            float framerateValue = framerate.Value;
            slider.Update(ref framerateValue, ref toggle);
            framerate.Value = RoundFramerate(framerateValue);
        }

        #endregion

        #region Utils

        public static float RoundFramerate(float framerate)
        {
            if (framerate > 30)
                return Mathf.Round(framerate / 10) * 10;
            else
                return Mathf.Round(framerate);
        }

        private string GetPresetTitle(FFmpegPreset preset) =>
            $"{preset.Name}  <color={presetAuthorNameColour}>{preset.Author}</color>";

        IEnumerator AutosaveCoroutine()
        {
            while (true)
            {
                if (guiEnabled)
                    ct?.SaveSettings();

                yield return new WaitForSecondsRealtime(10f);
            }
        }

        #endregion

        #region Toolbar and Activation

        public void AddToolbarButton()
        {
            //if (addedAppLauncherButton)
            //    return;

            if (ApplicationLauncher.Instance == null)
                return;

            if (HighLogic.LoadedScene != GameScenes.FLIGHT)
                return;

            Texture buttonTexture = GameDatabase.Instance.GetTexture("CaptureTools/Textures/icon", false);
            appLauncherButton = ApplicationLauncher.Instance.AddModApplication(ToggleGui, ToggleGui, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
            //addedAppLauncherButton = true;
        }

        public void RemoveToolbarButton()
        {
            if (appLauncherButton == null)
                return;

            ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
        }

        void ToggleGui()
        {
            if (guiEnabled)
            {
                DisableGui();

                if (appLauncherButton)
                    appLauncherButton.SetFalse(false);
            }
            else
            {
                EnableGui();

                if (appLauncherButton)
                    appLauncherButton.SetTrue(false);
            }
        }

        void EnableGui() =>
            guiEnabled = true;

        void DisableGui()
        {
            guiEnabled = false;
            ct.SaveSettings();
        }

        private void OnShowUI() =>
            guiHidden = false;

        private void OnHideUI() =>
            guiHidden = true;

        private void OnSceneLoaded(GameScenes data) =>
            loading = false;

        private void OnSceneRequested(GameScenes data) =>
            loading = true;

        #endregion
    }
}
