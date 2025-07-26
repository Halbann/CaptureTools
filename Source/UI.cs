using KSP.UI.Screens;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CaptureTools
{
    partial class CaptureTools
    {
        // GUI.
        private Rect windowRect = new Rect(Screen.width * 0.05f, Screen.height * 0.1f, 0, 0);
        private static GUIStyle boxStyle;
        private static GUIStyle textBoxStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle smallTextButtonStyle;
        private static bool initStyles = false;
        private static int windowWidth = 440;
        private int windowID;
        private static KeyCode toggleUIKeycode = KeyCode.F8;
        private static UISection currentSection = UISection.Capture;
        private static float timingSliderSpace = 12f;
        private static float sliderWidth = 220f;

        //private static bool showTimingSection = false;
        //private static bool showCaptureSection = false;
        private static bool showMainCameraSection = false;
        private static bool showMainSmoothingSection = false;
        private static bool showMulticamSection = false;
        //private static bool showSoundSection = false;
        //private static bool showAnimationSection = false;
        //private static bool showTraceSection = false;
        //private static bool showHDRISection = false;
        //private static bool showExperimentalSection = false;
        //private static bool showPhysicsSection = false;

        //private static bool addedAppLauncherButton = false;
        private ApplicationLauncherButton appLauncherButton;
        public static bool guiEnabled = false;
        private bool guiHidden = false;
        private bool loading = false;

        public bool showClapper = false;
        private static GUIStyle clapperStyle;

        // Sliders
        SettingSliderType captureFramerateSlider = new SettingSliderType { name = "Capture Frame Rate", min = 24, max = 120, rounding = 0, softClamp = true };
        SettingSliderType playbackFramerateSlider = new SettingSliderType { name = "Playback Frame Rate", min = 24, max = 120, rounding = 0, softClamp = true, useToggle = true };

        SettingSliderType captureFramerateSliderToggle = new SettingSliderType { name = "Capture Frame Rate", min = 24, max = 120, rounding = 0, softClamp = true, useToggle = true };
        SettingSliderType timescaleSlider = new SettingSliderType { name = "Time Scale", rounding = 3, useToggle = true };
        SettingSliderType maxDeltaTimeSlider = new SettingSliderType { name = "Max Delta Time", min = 0.02f, rounding = 2, useToggle = true };
        SettingSliderType fixedDeltaTimeSlider = new SettingSliderType { name = "Fixed Delta Time", min = 0.02f, rounding = 2, useToggle = true };

        #region GUI

        internal void OnGUI()
        {
            if (guiEnabled && !guiHidden && !loading)
                DrawGUI();

            DrawMulticamGUI();
            DrawMainCamGUI();

            if (CaptureMain && mainCaptureAudio && audioOnly && showClapper)
                DrawClapper();
        }

        public void DrawGUI() =>
            windowRect = GUILayout.Window(windowID, windowRect, FillWindow, "Capture Tools", GUILayout.Height(1), GUILayout.Width(windowWidth));

        private void InitStyles()
        {
            initStyles = true;

            boxStyle = GUI.skin.GetStyle("Box");

            textBoxStyle = new GUIStyle(GUI.skin.textField);
            textBoxStyle.alignment = TextAnchor.MiddleCenter;

            buttonStyle = GUI.skin.button;

            smallTextButtonStyle = new GUIStyle(buttonStyle);
            smallTextButtonStyle.fontSize = 10;
            smallTextButtonStyle.alignment = TextAnchor.MiddleRight;
        }

        private enum UISection
        {
            Capture,
            Trace,
            Timing,
            Physics,
            Animation,
            HDRI,
        }

        private void FillWindow(int windowID)
        {
            if (guiHidden)
                return;

            if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), ""))
                ToggleGui();

            if (GUI.Button(new Rect(windowRect.width - (18 * 2), 2, 16, 16), "\\", smallTextButtonStyle))
                Application.OpenURL(Path.GetFullPath(FilePath));

            if (!initStyles)
                InitStyles();

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

            // Experimental.

            /*VerticalSeparator();
            if (SectionButton("Experiments", ref showExperimentalSection))
            {
                GUILayout.BeginVertical(boxStyle);

                // Camera.

                if (nearClipDistance == 0)
                    nearClipDistance = FlightCamera.fetch.mainCamera.nearClipPlane;

                GUILayout.Label($"Camera Clipping Distance: {nearClipDistance}");
                float prevNearClip = nearClipDistance;
                nearClipDistance = GUILayout.HorizontalSlider(nearClipDistance, 0.0001f, 0.1f);
                if (prevNearClip != nearClipDistance)
                {
                    UpdateClipDistance();
                }

                // Kerbals

                if (GUILayout.Button("Hide Kerbals"))
                {
                    HideKerbals();
                }

                GUILayout.EndVertical();
            }*/

            // Debug

            /*if (FlightGlobals.ActiveVessel != null)
            {
                var active = FlightGlobals.ActiveVessel;
                GUILayout.Label($"RB Velocity: {active.rb_velocity:N1}");
                GUILayout.Label($"Offset: {(Vector3)FloatingOrigin.Offset:N1}");
                GUILayout.Label($"Offset Non-frame: {(Vector3)FloatingOrigin.OffsetNonKrakensbane:N1}");
                GUILayout.Label($"Cam Velocity: {mainVelocity:N1}");
                GUILayout.Label($"Engage: {debugKrakensbaneLatestEngage:N1}");
                GUILayout.Label($"Disengage: {debugKrakensbaneLatestDisengage:N1}");
            }*/



            GUI.DragWindow(new Rect(0, 0, 10000, 500));

            // Keep window inside screen space.
            windowRect.position = new Vector2(Mathf.Clamp(windowRect.position.x, 0, Screen.width - windowRect.width),
                               Mathf.Clamp(windowRect.position.y, 0, Screen.height - windowRect.height));
        }

        private void CaptureSection()
        {
            GUILayout.BeginVertical(boxStyle);

            if (CaptureMain || CaptureMulti)
                GUI.enabled = false;

            SettingSlider("Quality (CRF)", ref CRF, 10, 40, 0);
            UpdateFramerateSlider(captureFramerateSlider, captureFramerate);
            UpdateFramerateSlider(playbackFramerateSlider, playbackFramerate, ref differentPlaybackFramerate);

            // todo: move out of UI?
            if (!differentPlaybackFramerate && playbackFramerate.Value != captureFramerate)
                playbackFramerate.Value = captureFramerate;

            // Frame of reference.
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Sync: ");
            string[] syncStrings = new string[] { "Physics", "Rendering" };
            useFixedUpdate = 0 == GUILayout.SelectionGrid(useFixedUpdate ? 0 : 1, syncStrings, 2);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            fullRes = GUILayout.Toggle(fullRes, "Use Screen Resolution");
            previewOnly = GUILayout.Toggle(previewOnly, "Preview Only");

            GUI.enabled = true;

            showPreview = GUILayout.Toggle(showPreview, "Show Preview");

            if (SectionButton("Main Camera", ref showMainCameraSection))
            {
                GUILayout.BeginVertical(boxStyle);

                //if (RecordButton(CaptureMain, mainCaptureStartTime))
                if (RecordButton(CaptureMain, mainCaptureStartFrame, playbackFramerate, previewOnly))
                    CaptureMain = !CaptureMain;

                GUILayout.BeginHorizontal();

                if (!CaptureMain)
                {
                    mainCaptureAudio = GUILayout.Toggle(mainCaptureAudio, "Capture Audio");

                    if (mainCaptureAudio)
                        audioOnly = GUILayout.Toggle(audioOnly, "Audio Only");

                    drawUIOnMain = GUILayout.Toggle(drawUIOnMain, "Draw UI");
                }

                GUILayout.EndHorizontal();

                // We can't change these settings while recording.
                if (!CaptureMain && !fullRes)
                {
                    // Resolution.

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(3);
                    GUILayout.Label("Resolution", GUILayout.Width(70));

                    string restext = GUILayout.TextField(mainHeight.ToString(), GUILayout.Width(38));
                    if (int.TryParse(restext, out int result2))
                        mainHeight = result2;
                    else if (restext == "")
                        mainHeight = 1080;

                    //GUILayout.EndHorizontal();

                    GUILayout.FlexibleSpace();

                    // Aspect Ratio.

                    //GUILayout.BeginHorizontal();
                    //GUILayout.Label("Aspect Ratio", GUILayout.Width(70));
                    GUILayout.Label("Aspect Ratio");
                    GUILayout.Space(5);

                    string text = GUILayout.TextField(mainAspectRatio.x.ToString(), GUILayout.Width(38));
                    if (float.TryParse(text, out float result))
                        mainAspectRatio.x = result;
                    else if (text == "")
                        mainAspectRatio.x = 1;

                    text = GUILayout.TextField(mainAspectRatio.y.ToString(), GUILayout.Width(38));
                    if (float.TryParse(text, out result))
                        mainAspectRatio.y = result;
                    else if (text == "")
                        mainAspectRatio.y = 1;

                    GUILayout.Space(3);
                    GUILayout.EndHorizontal();
                }

                SettingSlider("Smoothing", ref mainSmoothTime, 0, 5f, 2);
                //SettingSlider("Smoothing Blend", ref positionSmoothing, 0f, 1f, 2);

                //GUILayout.BeginHorizontal();
                //GUILayout.FlexibleSpace();
                //showMainSmoothingSection = GUILayout.Toggle(showMainSmoothingSection, 
                //    "Smoothing Multipliers", buttonStyle, GUILayout.Width(140));
                //GUILayout.FlexibleSpace();
                //GUILayout.EndHorizontal();

                showMainSmoothingSection = GUILayout.Toggle(showMainSmoothingSection,
                    "Smoothing Multipliers", buttonStyle);

                //if (SectionButton("Smoothing Multipliers", ref showMainSmoothingSection))
                if (showMainSmoothingSection)
                {
                    GUILayout.BeginVertical(boxStyle);
                    SettingSliderToggle("Position", ref mainPositionSmoothTime, 0, 2f, 2, ref positionSmoothingEnabled);
                    SettingSlider("Pivot", ref pivotSmoothTime, 0, 2f, 2);
                    SettingSlider("Pan", ref panSmoothTime, 0, 2f, 2);
                    SettingSlider("Distance", ref distanceSmoothTime, 0, 2f, 2);
                    SettingSlider("Zoom", ref mainFOVsmoothTime, 0, 2f, 2);
                    GUILayout.EndVertical();
                }

                //SettingSlider("Lerp", ref mainCameraSlerp, 0.01f, 20f, 1);
                //SettingSlider("Max Speed", ref mainMaxSpeed, 0.01f, 200f, 1);

                GUILayout.EndVertical();
            }


            if (HighLogic.LoadedSceneIsFlight)
            {
                if (SectionButton("Multicam", ref showMulticamSection))
                {
                    GUILayout.BeginVertical(boxStyle);

                    //if (RecordButton(CaptureMulti, multicamCaptureStartTime))
                    if (RecordButton(CaptureMulti, multicamCaptureStartFrame, playbackFramerate, previewOnly))
                        CaptureMulti = !CaptureMulti;

                    SettingSlider("Ship Limit", ref shipLimit, 1, 16, 0);
                    SettingSlider("Camera FOV", ref cameraFov, 2, 90, 1);
                    SettingSlider("Camera Height", ref cameraHeight, 0, 20, 1);
                    SettingSlider("Camera Slide", ref cameraSlide, 0, 20, 1);
                    SettingSlider("Camera Distance", ref cameraDistance, 10, 200, 1);
                    SettingSlider("Target Delay", ref bdTargetDelay, 0, 5f, 1);
                    SettingSlider("Smoothing", ref multicamSmoothing, 0.01f, 5f, 2);

                    /*var setup = multiSetups.FirstOrDefault();
                    if (setup != null)
                    {
                        if (setup.vesselTarget != null)
                            GUILayout.Label($"vesselTarget: {setup.vesselTarget.vesselName}");

                        if (setup.lastBDTarget != null)
                            GUILayout.Label($"lastBDTarget: {setup.lastBDTarget.vesselName}");

                        GUILayout.Label($"lastBDTargetTime: {setup.lastBDTargetTime}");
                        GUILayout.Label($"lastBDTargetPos: {setup.lastBDTargetPos:0}");
                    }*/

                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndVertical();
        }

        private void TraceSection()
        {
            GUILayout.BeginVertical(boxStyle);

            if (!HighLogic.LoadedSceneIsFlight)
                GUI.enabled = false;

            // Record button.
            if (RecordButton(capturingTrace, traceStartTime, false))
            {
                if (capturingTrace)
                    StopPartCapture();
                else
                    StartPartCapture();
            }


            // File size.
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Est. File Size: ");

            if (TraceRecorder.recordedFrames == 0)
                GUILayout.Label("0 MB");
            else
            {
                float estimatedFileSizeMB = (0.06947368421f * TraceRecorder.recordedFrames) / 1000f;
                if (GUILayout.Button($"{estimatedFileSizeMB:N1} MB") && !capturingTrace)
                    TraceRecorder.recordedFrames = 0;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            if (!capturingTrace)
            {
                // Frame of reference.
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Frame of Reference: ");
                string[] frameStrings = Enum.GetNames(typeof(TraceFrame));
                traceFrameOfReference = (TraceFrame)GUILayout.SelectionGrid((int)traceFrameOfReference, frameStrings, frameStrings.Length);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                // Framerate.
                //GUILayout.Label($"Trace Framerate: {traceFramerate} FPS");
                //traceFramerateSlider = GUILayout.HorizontalSlider(traceFramerateSlider, 0, traceFramerates.Count - 1);
                //traceFramerate = traceFramerates[Mathf.RoundToInt(traceFramerateSlider)];

                SettingSlider("Trace Framerate", ref traceFramerate, 1, 50, 0);
            }

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void TimingSection()
        {
            // todo: move all of the application out of UI. It definitely doesn't belong here.

            GUILayout.BeginVertical(boxStyle);

            // Max delta time.
            maxDeltaTimeSlider.Update(ref maxDeltaTime, ref applyMaxDeltaTime);
            maxDeltaTime = Mathf.Clamp(maxDeltaTime, Time.fixedDeltaTime, 1f);

            // todo: this should not apply the default all the time that applyMaxDeltaTime is false.
            if (applyMaxDeltaTime && Time.maximumDeltaTime != maxDeltaTime)
                Time.maximumDeltaTime = maxDeltaTime;
            else if (!applyMaxDeltaTime && Time.maximumDeltaTime != defaultMaxDeltaTime)
                Time.maximumDeltaTime = defaultMaxDeltaTime;

            // Fixed Delta Time.
            fixedDeltaTimeSlider.Update(ref fixedDeltaTime, ref applyFixedDeltaTime);

            // todo: likewise
            if (applyFixedDeltaTime && Time.fixedDeltaTime != fixedDeltaTime)
                Time.fixedDeltaTime = fixedDeltaTime;
            else if (!applyFixedDeltaTime && Time.fixedDeltaTime != defaultFixedDeltaTime)
                Time.fixedDeltaTime = defaultFixedDeltaTime;

            // Capture framerate.
            bool prevApplyCaptureFramerate = applyCaptureFramerate;
            float prevCaptureFramerate = captureFramerate;

            UpdateFramerateSlider(captureFramerateSliderToggle, captureFramerate, ref applyCaptureFramerate);

            if (prevApplyCaptureFramerate != applyCaptureFramerate || captureFramerate != prevCaptureFramerate)
                Time.captureFramerate = applyCaptureFramerate ? captureFramerate : defaultCaptureFramerate;

            // Time Scale. todo: this is also silly.
            timescaleSlider.Update(ref timeScale, ref applyTimescale);
            if (applyTimescale && Time.timeScale != timeScale)
                Time.timeScale = timeScale;
            else if (!applyTimescale && Time.timeScale == timeScale)
                Time.timeScale = 1f;

            GUILayout.Label($"Physics Delta: {Math.Round(Time.fixedDeltaTime, 4)}");
            GUILayout.Label($"Frame Delta: {Math.Round(Time.deltaTime, 4)}");
            GUILayout.Label($"Time Scale: {Math.Round(Time.timeScale, 2)}");
            GUILayout.Label($"Time Ratio: {Math.Round(timeRatio, 2)}");

            GUILayout.EndVertical();
        }

        private void PhysicsSection()
        {
            GUILayout.BeginVertical(boxStyle);

            /*GUILayout.BeginHorizontal();

            bool prevPausePhysics = pausePhysics;
            pausePhysics = GUILayout.Toggle(pausePhysics, "Pause Physics");

            if (prevPausePhysics != pausePhysics)
                Physics.autoSimulation = !pausePhysics;

            if (GUILayout.Button("Step Physics"))
                Physics.Simulate(Time.fixedDeltaTime);

            GUILayout.EndHorizontal();*/

            GUILayout.BeginVertical(boxStyle);
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

                Debug.Log($"[CaptureTools]: Set interpolation of all Rigidbodies to {rbs[0].interpolation}.");
            }

            GUILayout.EndVertical();
        }

        private void AnimationSection()
        {
            GUILayout.BeginVertical(boxStyle);

            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("This plays a very simple building animation for the current vessel. " +
                "Can only be run while the game is paused or time is frozen.");
            GUILayout.EndVertical();

            if (Time.timeScale != 0f && buildCoroutine == null)
                GUI.enabled = false;

            string text = buildCoroutine == null ? "Play" : "Stop";
            if (GUILayout.Button(text))
            {
                if (buildCoroutine != null)
                    StopBuild();
                else
                    buildCoroutine = StartCoroutine(StartBuild(buildTime));
            }

            GUI.enabled = true;

            //GUILayout.Label(buildTime.ToString("0.00"));
            //buildTime = GUILayout.HorizontalSlider(buildTime, 1, 60);

            SettingSlider("Build Duration", ref buildTime, 1, 60, 2);

            //float partSpeed = (61 - partAnimateTime) / 60;
            //SettingSlider("Part Speed", ref partSpeed, 0, 1, 2);
            //partAnimateTime = 61 - (partSpeed * 60);

            SettingSlider("Move Time", ref buildPartSpeed, 0.001f, 3f, 3);
            //SettingSlider("Part Max Speed", ref buildPartMaxSpeed, 1f, 500f, 0);

            //GUILayout.Label(partAnimateTime.ToString("0.00"));
            ////partAnimateTime = GUILayout.HorizontalSlider(partAnimateTime, 1, 30);
            //partAnimateTime = GUILayout.HorizontalSlider(partAnimateTime, 0.1f, 20);

            GUILayout.EndVertical();
        }

        private void HDRISection()
        {
            GUILayout.BeginVertical(boxStyle);

            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Capture a high dynamic range image (HDRI) from the" +
                " game camera as an EXR for use in programs like Blender." +
                " This can take several seconds depending on the size of the image.");
            GUILayout.EndVertical();


            // Setting slider for powers of two.

            GUILayout.BeginHorizontal();
            GUILayout.Space(3);
            GUILayout.Label("Size", GUILayout.Width(70));

            // Slider
            HDRIWidthPower = Mathf.RoundToInt(Mathf.Log(HDRIWidth, 2));
            HDRIWidthPower = Mathf.RoundToInt(GUILayout.HorizontalSlider(HDRIWidthPower, 8, 12f));
            HDRIWidth = Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(Mathf.Pow(2f, HDRIWidthPower)));

            // Box
            string text = GUILayout.TextField(HDRIWidth.ToString(), textBoxStyle, GUILayout.Width(38));
            if (int.TryParse(text, out int result))
                HDRIWidth = result;
            else if (text == "")
                HDRIWidth = 2048;

            GUILayout.Space(3);
            GUILayout.EndHorizontal();


            SettingSlider("Sun Brightness", ref HDRISunBrightness, 0, 500, 1);
            HDRIHideKerbalsInEditor = GUILayout.Toggle(HDRIHideKerbalsInEditor, "Hide Kerbals in Editor");

            // Capture HDRI.

            if (GUILayout.Button("Capture HDRI"))
                CaptureHDRI(HDRIWidth, HDRISunBrightness, HDRIHideKerbalsInEditor);

            GUILayout.EndVertical();
        }

        void VerticalSeparator()
        {
            //GUILayout.BeginHorizontal();
            //GUILayout.FlexibleSpace();
            //GUI.color = Color.grey;
            //GUILayout.Label("--------------");
            //GUI.color = Color.white;
            //GUILayout.FlexibleSpace();
            //GUILayout.EndHorizontal();
        }

        // todo: replace all uses of SettingSlider and remove SettingSlider.

        public class SettingSliderType
        {
            public string name = string.Empty;
            public float min = 0f;
            public float max = 1f;
            public int rounding = 2;
            public bool softClamp = false;
            public bool useToggle = false;

            public static float sliderWidth = 220f;

            private string text;
            private float sliderMin;
            private float sliderMax;

            public float Update(float setting)
            {
                bool update = false;
                return Update(setting, false, ref update).setting;
            }

            public (float setting, bool toggle) Update(float setting, bool toggle)
            {
                bool update = false;
                return Update(setting, toggle, ref update);
            }

            public void Update(ref float setting, ref bool toggle)
            {
                (setting, toggle) = Update(setting, toggle);
            }

            public (float setting, bool toggle) Update(float setting, bool toggle, ref bool updated)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(3);

                // Toggle.
                if (useToggle)
                {
                    toggle = GUILayout.Toggle(toggle, "", GUILayout.Width(12));
                    bool guiEnabled = GUI.enabled;

                    if (!toggle)
                        GUI.enabled = false;
                }

                // Label.
                if (name != "")
                    GUILayout.Label(name);

                // Slider
                sliderMin = !softClamp ? min : Mathf.Min(setting, min);
                sliderMax = !softClamp ? max : Mathf.Max(setting, max);
                setting = (float)Math.Round(GUILayout.HorizontalSlider(setting, sliderMin, sliderMax, GUILayout.Width(sliderWidth)), rounding);

                // Box
                text = GUILayout.TextField(setting.ToString("N" + rounding.ToString()), textBoxStyle, GUILayout.Width(38));
                if (float.TryParse(text, out float result))
                    setting = result;
                else if (text == "")
                    setting = 0;

                // Toggle GUI enabled reset.
                if (useToggle && !toggle)
                    GUI.enabled = guiEnabled;

                GUILayout.Space(3);
                GUILayout.EndHorizontal();

                return (setting, toggle);
            }
        }

        public static float RoundFramerate(float framerate)
        {
            if (framerate > 30)
                return Mathf.Round(framerate / 10) * 10;
            else
                return Mathf.Round(framerate);
        }

        void SettingSlider(string name, ref float setting, float min, float max, int rounding, bool softMin = false)
        {
            bool update = false;
            SettingSlider(name, ref setting, min, max, rounding, ref update, softMin);
        }

        void SettingSlider(string name, ref float setting, float min, float max, int rounding, ref bool update, bool softMin = false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(3);

            if (name != "")
                GUILayout.Label(name);

            float old = setting;

            // Slider
            float sliderMin = !softMin ? min : Mathf.Min(setting, min);
            setting = (float)Math.Round(GUILayout.HorizontalSlider(setting, sliderMin, max, GUILayout.Width(sliderWidth)), rounding);

            // Box
            string text = GUILayout.TextField(setting.ToString("N" + rounding.ToString()), textBoxStyle, GUILayout.Width(38));
            if (float.TryParse(text, out float result))
                setting = result;
            else if (text == "")
                setting = 0;

            update = update || (old != setting);

            GUILayout.Space(3);
            GUILayout.EndHorizontal();
        }

        void SettingSliderToggle(string name, ref float setting, float min, float max, int rounding, ref bool toggle, bool softMin = false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(3);

            toggle = GUILayout.Toggle(toggle, "", GUILayout.Width(12));

            bool guiEnabled = GUI.enabled;
            if (!toggle)
                GUI.enabled = false;

            if (name != "")
                GUILayout.Label(name);

            // Slider
            float sliderMin = !softMin ? min : Mathf.Min(setting, min);
            setting = (float)Math.Round(GUILayout.HorizontalSlider(setting, sliderMin, max, GUILayout.Width(sliderWidth)), rounding);

            // Box
            string text = GUILayout.TextField(setting.ToString("N" + rounding.ToString()), textBoxStyle, GUILayout.Width(38));
            if (float.TryParse(text, out float result))
                setting = result;
            else if (text == "")
                setting = 0;

            GUI.enabled = guiEnabled;

            GUILayout.Space(3);
            GUILayout.EndHorizontal();
        }

        private static bool SectionButton(string name, ref bool value)
        {
            value = GUILayout.Toggle(value, name, buttonStyle);
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

            string colour = !preview ? "red" : "white";
            string redlight = recording ? $"<color={colour}>●</color>" : "○";

            string recordingText = !preview ? $"<b>{recordingTimeMinutes:00}:{recordingTimeSeconds:00}</b>" : "<b>PREVIEW</b>";
            string buttonText = recording ? recordingText : "Record";

            string recordingButtonText = redlight + " " + buttonText;

            return GUILayout.Button(recordingButtonText);
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

        private void UpdateFramerateSlider(SettingSliderType slider, Constrained framerate) =>
            framerate.Value = RoundFramerate(slider.Update(framerate));

        private void UpdateFramerateSlider(SettingSliderType slider, Constrained framerate, ref bool toggle)
        {
            float framerateValue = framerate.Value;
            slider.Update(ref framerateValue, ref toggle);
            framerate.Value = RoundFramerate(framerateValue);
        }

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
            SaveSettings();
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
