using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;

using UnityEngine;

using FFmpegOut;
using CaptureTools.Integration;

namespace CaptureTools
{
    partial class CaptureTools
    {
        #region Fields

        // Control.

        private bool captureMulti = false;
        private bool CaptureMulti
        {
            get => captureMulti;
            set
            {
                if (value != captureMulti)
                {
                    captureMulti = value;
                    if (captureMulti) StartMultiCapture();
                    else StopMultiCapture();
                }
            }
        }

        private bool captureMain = false;
        private bool CaptureMain
        {
            get => captureMain;
            set
            {
                if (value != captureMain)
                {
                    captureMain = value;
                    if (captureMain) StartMainCapture();
                    else StopMainCapture();
                }
            }
        }


        // Shared.

        private static readonly List<string> cameraNames = new List<string> { "GalaxyCamera", "Camera ScaledSpace", "Camera 00" };
        private Material renderTextureMaterial;

        public static float CRF = 15;
        public static float playbackFramerate = 60f;
        public static bool previewOnly = false;
        public static bool fullRes = false;
        public static bool useFixedUpdate = true;

        // Multi

        private List<GameObject> cameras = new List<GameObject>();
        private static List<RenderTexture> renderTextures = new List<RenderTexture>();
        private int renderWidth;
        private int renderHeight;
        private int multicamCaptureStartFrame;
        private Vector3 multicamUp;

        public static bool showPreview = true;
        public static float cameraFov = 15;
        public static float cameraHeight = 10f;
        public static float cameraDistance = 100;
        public static float cameraSlide = 15f;
        public static float multicamSmoothing = 0.3f;
        public static float shipLimit = 4;
        public static float bdTargetDelay = 3;

        public class MultiSetup
        {
            public Vessel vessel;
            public List<Camera> cameras;
            public Vector3 comOffset;
            public RenderTexture rt;
            public Quaternion smoothingVelocity;
            public bool captureInitialised = false;

            public Vector3 targetVectorLerped;
            public Vessel vesselTarget;

            public PartModule BDAIModule;
            public bool hasBDAI = false;
            public float BDTargetTime = 0;
            public Vessel BDTarget;

            public Transform debugTransform;
        }

        private List<MultiSetup> multiSetups = new List<MultiSetup>();


        // Main

        private int mainCaptureStartFrame;
        private bool captureSceneIsFlight;
        private CaptureAudioUnity mainAudioCapture;
        private Transform audioListenerParent;
        private AudioListener audioListener;
        private List<Camera> mainCameras = new List<Camera>();
        private RenderTexture mainRenderTexture;
        private GameObject mainCameraParent;
        private GameObject mainCameraPivot;
        private bool usingPivot = false;
        private bool mainCaptureInitialised = false;

        public static int mainHeight = 1080;
        public static Vector2 mainAspectRatio = new Vector2(16f, 9f);
        public static bool mainCaptureAudio = true;
        public static bool audioOnly = false;
        public static bool drawUIOnMain = false;

        private static float minSmoothTime = 0.025f;
        private Quaternion lastRotation = Quaternion.identity;

        // Position.
        private Vessel lastVessel;
        private Vector3 smoothedPosition;
        public static float positionSmoothing = 1f;
        public static bool positionSmoothingEnabled = true;

        public static float mainMaxSpeed = 100f;
        public static float positionMaxSpeed = 100f;
        public static float mainFOVmaxSpeed = 1000f;
        public static float mainDistanceMaxSpeed = 1000f;
        public static float transitionalSpeedMaxSpeed = 100f;

        // Integral catchup.
        private float speedIntegral = 0;
        public static float speedIntegralGain = 0.5f;

        // Velocity transition.
        private float transitionalSpeed = 0;

        // Smooth damp times.
        public static float mainSmoothTime = 1.7f;
        public static float mainPositionSmoothTime = 1f;
        public static float pivotSmoothTime = 1f;
        public static float panSmoothTime = 1.3f;
        public static float distanceSmoothTime = 1.2f;
        public static float mainFOVsmoothTime = 1.2f;
        public static float transitionalSpeedSmoothTime = 1.15f;

        // Smooth damp speeds.
        private Vector3 mainVelocity = Vector3.zero;
        private Quaternion smoothPivotSpeed = new Quaternion(0, 0, 0, 0);
        private Quaternion smoothParentSpeed = new Quaternion(0, 0, 0, 0);
        private float smoothDistanceSpeed = 0;
        private float mainFOVvelocity = 0f;
        private float transitionalSpeedVelocity = 0;

        // Pivot transition.
        private Vector3 pivotTransitionPosVelocity = Vector3.zero;
        private Quaternion pivotTransitionRotVelocity = new Quaternion(0, 0, 0, 0);

        #endregion
        #region Main capture

        private void StartMainCapture()
        {
            // Same idea as multicapture. But we do create a single setup that smoothly follows the main flight camera.

            bool isEditor = HighLogic.LoadedSceneIsEditor;
            bool isFlight = HighLogic.LoadedSceneIsFlight;
            captureSceneIsFlight = isFlight;

            mainCameras.Clear();

            int renderHeight = fullRes ? Screen.height : mainHeight;

            float aspectRatio = mainAspectRatio.x / mainAspectRatio.y;
            int renderWidth = fullRes ? Screen.width : Mathf.RoundToInt(mainHeight * aspectRatio);

            // Setup render texture.
            mainRenderTexture = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.Default);
            mainRenderTexture.antiAliasing = QualitySettings.antiAliasing;

            var cameraNames = isEditor ? new List<string> { "Main Camera" } : CaptureTools.cameraNames;

            // Create clones of the main, scaled-space and galaxy cameras.
            foreach (string cameraName in cameraNames)
            {
                var camObject = new GameObject();
                camObject.name = "Capture Tools Main - " + cameraName;
                camObject.transform.position = Vector3.zero;

                Camera cam = camObject.AddComponent<Camera>();
                mainCameras.Add(cam);

                var template = Camera.allCameras.FirstOrDefault(c => c.name == cameraName);
                cam.CopyFrom(template);

                cam.fieldOfView = 20;
                cam.allowMSAA = true;
                cam.cullingMask &= ~(1 << 8);

                if (isEditor)
                {
                    cam.cullingMask = -1;
                    cam.clearFlags = CameraClearFlags.Skybox;
                }

                // All three cameras write to the same render rexture.
                cam.targetTexture = mainRenderTexture;

                CopyCameraPostProcess(template, cam);
            }

            if (isFlight)
            {
                // Add a flare layer to the scaled space camera.
                mainCameras[1].gameObject.AddComponent<FlareLayer>();
            }


            // Place the main camera in its starting position.
            Camera main = isFlight ? mainCameras[2] : mainCameras.First();
            Camera flightCamera = isFlight ? FlightCamera.fetch.mainCamera : GetEditorCamera();

            // Controls centre and rotation.
            mainCameraPivot = new GameObject();
            mainCameraPivot.transform.position = flightCamera.transform.parent.parent.position;
            mainCameraPivot.transform.rotation = flightCamera.transform.parent.parent.rotation;
            smoothedPosition = mainCameraPivot.transform.position;

            // Controls distance.
            mainCameraParent = new GameObject();
            mainCameraParent.transform.parent = mainCameraPivot.transform;
            mainCameraParent.transform.localRotation = flightCamera.transform.parent.localRotation;
            mainCameraParent.transform.localPosition = flightCamera.transform.parent.localPosition;

            main.transform.parent = mainCameraParent.transform;
            main.transform.localPosition = Vector3.zero;
            main.transform.localRotation = Quaternion.identity;

            if (isFlight)
            {
                mainCameras[0].transform.rotation = main.transform.rotation;
                mainCameras[1].transform.rotation = main.transform.rotation;
            }

            if (isEditor)
            {
                mainCameras.First().usePhysicalProperties = true;
                mainCameras.First().usePhysicalProperties = false;
            }

            mainCameras.ForEach(c => c.fieldOfView = flightCamera.fieldOfView);

            // Reset smooth damp speeds.
            smoothDistanceSpeed = 0;
            mainVelocity = Vector3.zero;
            transitionalSpeedVelocity = 0;
            smoothPivotSpeed = new Quaternion(0, 0, 0, 0);
            smoothParentSpeed = new Quaternion(0, 0, 0, 0);
            mainFOVvelocity = 0;
            lastRotation = mainCameraPivot.transform.rotation;

            BlockHighlighters(true);
            InitPreviewMaterial();

            lastVessel = FlightGlobals.ActiveVessel;
            mainCaptureStartFrame = Time.frameCount;
            mainCaptureInitialised = false;
            Time.captureFramerate = Mathf.RoundToInt(captureFramerate);

            if (isFlight)
            {
                GameEvents.onFloatingOriginShift.Add(OnFloatingOriginShift);
                GameEvents.onVesselChange.Add(OnVesselChange);
                //GameEvents.onVesselSituationChange.Add(OnVesselSituationChange);
                //GameEvents.onKrakensbaneDisengage.Add(OnKrakensbaneDisengage);
                //GameEvents.onKrakensbaneEngage.Add(OnKrakensbaneEngage);
            }

            if (useFixedUpdate)
                StartCoroutine(MainFixedUpdate());

            // debug transforms

            var pivotDebug = mainCameraPivot.AddComponent<DrawTransform>();
            pivotDebug.text = "Main Pivot";
            pivotDebug.scale = 6;

            var parentDebug = mainCameraParent.AddComponent<DrawTransform>();
            parentDebug.text = "Main Parent";
            parentDebug.scale = 3;

            var mainDebug = main.gameObject.AddComponent<DrawTransform>();
            mainDebug.text = "Main Camera";
            mainDebug.scale = 3;
        }

        private IEnumerator MainFixedUpdate()
        {
            // Use the timing of WaitForFixedUpdate to run main camera updates.
            // This is preferred to FixedUpdate because it runs after all FixedUpdate calls and physics movement.

            while (captureMain)
            {
                UpdateMainCamera();
                yield return new WaitForFixedUpdate();
            }
        }

        private void OnVesselChange(Vessel data)
        {
            //UpdateMainCamera(0f);

            if (useFixedUpdate && positionSmoothingEnabled)
                StartCoroutine(MainHandleVesselChange());
        }

        private IEnumerator MainHandleVesselChange()
        {
            UpdateMainCamera(0f);

            yield return null;

            UpdateMainCamera(0f);
        }

        private void StopMainCapture()
        {
            // Same idea as multicapture. But we do create a single setup that follows the main flight camera.

            mainCameras.ForEach(c => Destroy(c.gameObject));
            mainCameras.Clear();

            mainRenderTexture.Release();
            mainRenderTexture = null;

            Destroy(mainCameraPivot);

            BlockHighlighters(false);

            if (captureSceneIsFlight)
            {
                GameEvents.onFloatingOriginShift.Remove(OnFloatingOriginShift);
                GameEvents.onVesselChange.Remove(OnVesselChange);
                //GameEvents.onVesselSituationChange.Remove(OnVesselSituationChange);
                //GameEvents.onKrakensbaneDisengage.Remove(OnKrakensbaneDisengage);
                //GameEvents.onKrakensbaneEngage.Remove(OnKrakensbaneEngage);
            }

            Time.captureFramerate = 0;
            QualitySettings.vSyncCount = originalVSyncCount;
            Application.targetFrameRate = originalTargetFrameRate;


            // Audio

            if (mainCaptureAudio)
            {
                Destroy(mainAudioCapture);

                //var mainListener = Camera.allCameras.FirstOrDefault(c => c.name == "Camera 00")?.GetComponentInChildren<AudioListener>();
                //if (mainListener != null)
                //    mainListener.enabled = true;

                //AudioListener.volume = audioListenerVolumeDefault;

                if (audioListener != null)
                    audioListener.transform.parent = audioListenerParent;
            }

            //TimingManager.FixedUpdateRemove(TimingManager.TimingStage.BetterLateThanNever, UpdateMainCamera);
            //TimingManager.FixedUpdateRemove(TimingManager.TimingStage.BetterLateThanNever, UpdateMainCameraFixed);
        }

        private static Camera GetEditorCamera()
        {
            return EditorCamera.Instance.cam.transform.GetChild(0).GetComponent<Camera>();
        }

        private void UpdateMainCamera(float deltaTime = -1f)
        {
            Time.captureFramerate = Mathf.RoundToInt(captureFramerate);

            if (Application.targetFrameRate != Time.captureFramerate)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Time.captureFramerate;
            }

            if (deltaTime == -1)
                deltaTime = useFixedUpdate ? Time.fixedDeltaTime : 1f / Time.captureFramerate;

            bool isFlight = HighLogic.LoadedSceneIsFlight;

            Camera flightCamera = isFlight ? FlightCamera.fetch.mainCamera : GetEditorCamera();
            if (flightCamera == null)
                return;

            Camera main = isFlight ? mainCameras[2] : mainCameras.First();
            Vessel active = FlightGlobals.ActiveVessel;

            bool usePivot = flightCamera.transform.parent?.parent?.name == "main camera pivot";
            bool pivotChanged = usePivot != usingPivot;
            usingPivot = usePivot;

            if (usePivot)
            {
                // Re-attach to pivot.

                if (pivotChanged)
                    main.transform.SetParent(mainCameraParent.transform, true);

                // Gradually push the camera back to the parent for when it transitions onto the pivot.

                main.transform.localPosition = Vector3.SmoothDamp(main.transform.localPosition, Vector3.zero, 
                    ref pivotTransitionPosVelocity, mainSmoothTime, mainMaxSpeed, deltaTime);
                main.transform.localRotation = SmoothDampQ(main.transform.localRotation, Quaternion.identity, 
                    ref pivotTransitionRotVelocity, mainSmoothTime, mainMaxSpeed, deltaTime);


                // Distance. Scroll in and out.

                float currentDistance = mainCameraParent.transform.localPosition.z;
                float targetDistance = flightCamera.transform.parent.localPosition.z;
                float smoothedDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref smoothDistanceSpeed,
                                       Mathf.Max(mainSmoothTime * distanceSmoothTime, minSmoothTime), mainDistanceMaxSpeed, deltaTime);
                mainCameraParent.transform.localPosition = new Vector3(0, 0, smoothedDistance);


                // Pivot position. Follows the centre of the active vessel.

                Vector3 current = smoothedPosition;
                Vector3 target = flightCamera.transform.parent.parent.position;
                Vector3 toTarget = target - current;
                Vector3 toTargetActual = target - mainCameraPivot.transform.position;

                if (positionSmoothingEnabled)
                {
                    transitionalSpeed = Mathf.SmoothDamp(transitionalSpeed, 0, ref transitionalSpeedVelocity, 
                        transitionalSpeedSmoothTime * mainSmoothTime, transitionalSpeedMaxSpeed, deltaTime);

                    float error = toTarget.magnitude;
                    speedIntegral += error * deltaTime;

                    if (error < 5f)
                        speedIntegral = 0;

                    if (isFlight)
                    {
                        bool flying = active.situation == Vessel.Situations.FLYING || active.situation == Vessel.Situations.LANDED;
                        float activeSpeed = flying ? (float)active.srf_velocity.magnitude : active.rb_velocity.magnitude;
                        positionMaxSpeed = Mathf.Max(speedIntegral * speedIntegralGain, 100, transitionalSpeed, activeSpeed * 1.5f, mainVelocity.magnitude);
                    }

                    // Scale with velocity such that the error is never more than currentDistance
                    //float mainPosSmoothTimeActual = mainPositionSmoothTime / Mathf.Max(toTargetActual.magnitude, 1);

                    //float mainPosSmoothTimeActual = mainPositionSmoothTime / Mathf.Max(activeSpeed, 1);
                    //float mainPosSmoothTimeActual = Mathf.Abs(currentDistance) * mainPositionSmoothTime / activeSpeed;

                    smoothedPosition = Vector3.SmoothDamp(current, target, ref mainVelocity,
                        Mathf.Max(mainPositionSmoothTime * mainSmoothTime, minSmoothTime), positionMaxSpeed, deltaTime);

                    mainCameraPivot.transform.position = smoothedPosition;
                    //mainCameraPivot.transform.position = Vector3.Lerp(smoothedPosition, target, 1f - positionSmoothing);
                }
                else
                {
                    mainCameraPivot.transform.position = target;
                }


                // Orbit around the pivot. Right click and drag.

                //Quaternion currentRot = mainCameraPivot.transform.rotation;
                Quaternion currentRot = lastRotation;
                Quaternion targetRot = flightCamera.transform.parent.parent.rotation;

                if (isFlight && positionSmoothingEnabled)
                {
                    Vector3 up = active.situation == Vessel.Situations.ORBITING ? main.transform.up : FlightCamera.fetch.upAxis;
                    Vector3 toTargetVelocity = Vector3.Slerp(toTargetActual.normalized, active.rb_velocity.normalized, 0.5f);
                    Quaternion lookAtTarget = Quaternion.LookRotation(toTargetVelocity, up);

                    targetRot = Quaternion.Slerp(targetRot, lookAtTarget, Mathf.InverseLerp(5, 50, toTargetActual.magnitude));
                }

                mainCameraPivot.transform.rotation = SmoothDampQ(currentRot, targetRot, 
                    ref smoothPivotSpeed, Mathf.Max(mainSmoothTime * pivotSmoothTime, minSmoothTime), mainMaxSpeed, deltaTime);

                lastRotation = mainCameraPivot.transform.rotation;


                // Pan. Middle mouse.

                Quaternion currentParentRot = mainCameraParent.transform.localRotation;
                Quaternion targetParentRot = flightCamera.transform.parent.localRotation;
                Quaternion smoothedParentRot = SmoothDampQ(currentParentRot, targetParentRot, 
                    ref smoothParentSpeed, Mathf.Max(mainSmoothTime * panSmoothTime, minSmoothTime), mainMaxSpeed, deltaTime);

                mainCameraParent.transform.localRotation = smoothedParentRot;
            }
            else
            {
                // Detach from pivot.
                if (pivotChanged)
                {
                    main.transform.SetParent(null, true);
                }

                // Instant reset when starting a path in Camera Tools.
                if (cameraToolsLoaded 
                    && (Input.GetKeyDown(cameraToolsCameraKey)))
                {
                    var toolModeField = camToolsInstance.GetType().GetField("toolMode", BindingFlags.Public | BindingFlags.Instance);
                    int toolMode = (int)toolModeField.GetValue(camToolsInstance);

                    // Only in pathing mode.
                    if (toolMode == 2)
                        ResetCamera();
                }
                else
                {
                    // Follow the absolute position and rotation of the flight camera.

                    main.transform.position = Vector3.SmoothDamp(main.transform.position, flightCamera.transform.position,
                        ref mainVelocity, Mathf.Max(mainSmoothTime * mainPositionSmoothTime, minSmoothTime), positionMaxSpeed, deltaTime);

                    main.transform.rotation = SmoothDampQ(main.transform.rotation, flightCamera.transform.rotation,
                        ref smoothParentSpeed, Mathf.Max(mainSmoothTime * panSmoothTime, minSmoothTime), mainMaxSpeed, deltaTime);
                }
            }

            if (isFlight)
            {
                mainCameras[0].transform.rotation = main.transform.rotation;
                mainCameras[1].transform.rotation = main.transform.rotation;
            }

            foreach (var cam in mainCameras)
            {
                cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, flightCamera.fieldOfView, 
                    ref mainFOVvelocity, Mathf.Max(mainFOVsmoothTime * mainSmoothTime, minSmoothTime), mainFOVmaxSpeed, deltaTime);
            }


            // Lazy initialisation of Camera Capture.

            if (!mainCaptureInitialised)
            {
                mainCaptureInitialised = true;

                if (Scatterer.loaded)
                    Scatterer.SetupCameras(main, mainCameras[1], false);

                if (!previewOnly)
                    InitialiseCameraCapture(main, true, "");
            }
        }

        private void UpdateMainCameraFixed()
        {
            //mainCameraPivot.transform.position -= FloatingOrigin.fetch.offset;

            if (lastVessel != FlightGlobals.ActiveVessel)
            {
                Vector3 velocity = lastVessel.rb_velocity - FlightGlobals.ActiveVessel.rb_velocity;
                mainVelocity += velocity;
                transitionalSpeed = mainVelocity.magnitude;
                speedIntegral = 0;
            }

            lastVessel = FlightGlobals.ActiveVessel;
        }

        private void ResetCamera()
        {
            Camera main = mainCameras[2];
            Camera flightCamera = FlightCamera.fetch.mainCamera;

            main.transform.position = flightCamera.transform.position;
            main.transform.rotation = flightCamera.transform.rotation;

            //mainCameraParent.transform.localPosition = new Vector3(0, 0, 0);
            //mainCameraPivot.transform.position = flightCamera.transform.position;
            //mainCameraPivot.transform.rotation = flightCamera.transform.rotation;
            //mainCameraParent.transform.localRotation = Quaternion.identity;

            foreach (var cam in mainCameras)
            {
                cam.fieldOfView = flightCamera.fieldOfView;
            }

            mainVelocity = Vector3.zero;
            smoothPivotSpeed = new Quaternion(0, 0, 0, 0);
            smoothParentSpeed = new Quaternion(0, 0, 0, 0);
            smoothDistanceSpeed = 0;
            mainFOVvelocity = 0f;
            transitionalSpeedVelocity = 0;
        }

        private void OnFloatingOriginShift(Vector3d offset, Vector3d nonFrame)
        {
            if (mainCameraPivot == null)
                return;

            if (!positionSmoothingEnabled)
                return;

            switch (FlightGlobals.ActiveVessel.situation)
            {
                case Vessel.Situations.FLYING:
                case Vessel.Situations.LANDED:
                    smoothedPosition -= offset;
                    smoothedPosition -= nonFrame;
                    break;

                default:
                    smoothedPosition -= offset;
                    break;
            }

            //if (useFixedUpdate)
            //    UpdateMainCamera(0f);

            //mainCameraPivot.transform.position -= offset;
            //Debug.Log($"[CaptureTools]: OnFloatingOriginShift - offset: {offset} - nonframe: {nonFrame}");
        }

        /*Vector3 debugKrakensbaneLatestEngage = Vector3.zero;
        Vector3 debugKrakensbaneLatestDisengage = Vector3.zero;

        private void OnKrakensbaneEngage(Vector3d data)
        {
            Debug.Log($"[CaptureTools]: OnKrakensbaneEngage {data}");

            debugKrakensbaneLatestEngage = data;

            //mainVelocity += data;
            //positionMaxSpeed = Mathf.Max(mainVelocity.magnitude, mainMaxSpeed);
        }

        private void OnKrakensbaneDisengage(Vector3d data)
        {
            Debug.Log($"[CaptureTools]: OnKrakensbaneDisengage {data}");

            debugKrakensbaneLatestDisengage = data;

            //mainVelocity += data;
            //positionMaxSpeed = Mathf.Max(mainVelocity.magnitude, mainMaxSpeed);
        }

        private void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> data)
        {
            Debug.Log($"[CaptureTools]: OnVesselSituationChange host: {data.host} from: {data.from} to: {data.to}");
        }*/

        #endregion

        #region Multi-capture

        private void StartMultiCapture()
        {
            cameras.Clear();
            //vesselCameraSetups.Clear();
            //comOffsets.Clear();
            //vesselTargets.Clear();
            //targetVectorsLerped.Clear();
            renderTextures.Clear();
            multiSetups.Clear();

            // Determine the render texture size by maximum number of vessels on the grid.
            float sqrt = Mathf.Floor(Mathf.Sqrt(shipLimit - 0.1f)) + 1;
            renderWidth = fullRes ? Screen.width : (int)Mathf.Ceil(Screen.width / sqrt);
            renderHeight = fullRes ? Screen.height : (int)Mathf.Ceil(Screen.height / sqrt);

            // Take the top N vessels by part count.
            List<Vessel> vessels = FlightGlobals.VesselsLoaded.ToList();
            vessels.RemoveAll(v => !VesselValid(v));
            vessels.Remove(FlightGlobals.ActiveVessel);
            vessels = vessels.OrderByDescending(v => v.parts.Count).ToList();

            // Add the active vessel to the front of the list.
            if (VesselValid(FlightGlobals.ActiveVessel))
                vessels.Insert(0, FlightGlobals.ActiveVessel);

            vessels = vessels.Take(Mathf.Min((int)shipLimit, vessels.Count)).ToList();

            // Setup each vessel.
            vessels.ForEach(v => SetupVessel(v));

            // Finish setup.
            BlockHighlighters(true);
            InitPreviewMaterial();
            multicamCaptureStartFrame = Time.frameCount;
            Time.captureFramerate = Mathf.RoundToInt(captureFramerate);
            multicamUp = FlightCamera.fetch == null ? Vector3.up : FlightCamera.fetch.getReferenceFrame() * Vector3.up;


            if (useFixedUpdate)
                StartCoroutine(MultiFixedUpdate());
        }

        private void StopMultiCapture()
        {
            cameras.RemoveAll(c => c == null);
            cameras.ForEach(c => Destroy(c));
            cameras.Clear();

            renderTextures.RemoveAll(t => t == null);
            renderTextures.ForEach(t => t.Release());
            renderTextures.Clear();

            BlockHighlighters(false);
            Time.captureFramerate = 0;

            QualitySettings.vSyncCount = originalVSyncCount;
            Application.targetFrameRate = originalTargetFrameRate;

            multiSetups.ForEach(s => Destroy(s.debugTransform.gameObject));
        }

        void SetupVessel(Vessel vessel)
        {
            var vesselCameras = new List<Camera>();

            var format = FlightCamera.fetch.mainCamera.allowHDR ? RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR;
            // idk bro. don't ask me. rt needs to be a different format from the camera when using TUFX or it draws over the main camera rt for each vessel.

            // Setup render texture.
            RenderTexture renderTexture = new RenderTexture(renderWidth, renderHeight, 24, format); 
            renderTexture.antiAliasing = QualitySettings.antiAliasing;
            renderTextures.Add(renderTexture);

            // Create clones of the main, scaled-space and galaxy cameras.
            foreach (string cameraName in cameraNames)
            {
                var camObject = new GameObject();
                camObject.name = vessel.GetDisplayName() + cameraName;
                cameras.Add(camObject);
                Camera cam = camObject.AddComponent<Camera>();
                vesselCameras.Add(cam);

                var template = Camera.allCameras.FirstOrDefault(c => c.name == cameraName);
                cam.CopyFrom(template);

                cam.fieldOfView = 20;
                cam.cullingMask &= ~(1 << 8);
                cam.allowHDR = template.allowHDR;
                cam.allowMSAA = template.allowMSAA;

                // All three cameras write to the same render rexture.
                cam.targetTexture = renderTexture;

                CopyCameraPostProcess(template, cam);
            }

            // Galaxy cam should clear to black.
            vesselCameras[0].clearFlags = CameraClearFlags.SolidColor;
            vesselCameras[0].backgroundColor = Color.black;

            // Add a flare layer to the scaled space camera.
            vesselCameras[1].gameObject.AddComponent<FlareLayer>();

            // Place the main camera in its starting position.
            Camera main = vesselCameras[2];

            //main.transform.SetParent(vessel.ReferenceTransform);
            //main.transform.localPosition = vessel.CoM - vessel.ReferenceTransform.position + new Vector3(0, -30, -10); // 30 metres back and 10 metres up for an SPH vehicle.

            //main.transform.position = vessel.CoM + new Vector3(0, -30, -10); // 30 metres back and 10 metres up for an SPH vehicle.
            //main.transform.LookAt(vessel.CoM, vessel.ReferenceTransform.forward * -1);

            main.transform.position = Vector3.zero;
            main.transform.rotation = Quaternion.identity;

            vesselCameras[0].transform.rotation = main.transform.rotation;
            vesselCameras[1].transform.rotation = main.transform.rotation;

            // Add setup to lists.
            //vesselCameraSetups.Add(new Tuple<Vessel, List<Camera>>(vessel, vesselCameras));
            //comOffsets.Add(vessel.ReferenceTransform.InverseTransformPoint(vessel.CoM));
            //vesselTargets.Add(null);
            //targetVectorsLerped.Add(Vector3.zero);

            var setup = new MultiSetup
            {
                vessel = vessel,
                cameras = vesselCameras,
                comOffset = vessel.ReferenceTransform.InverseTransformPoint(vessel.CoM),
                targetVectorLerped = vessel.ReferenceTransform.forward,
                vesselTarget = null,
                rt = renderTexture,
            };

            if (BD.BDLoaded)
            {
                // Get the AI module so we can check the target.
                PartModule BDAIModule;
                setup.hasBDAI = BD.TryGetBDAI(vessel, out BDAIModule);
                setup.BDAIModule = BDAIModule;

                // Add the competition overlay to the main camera.
                //BD.AddTestCanvas(main);
            }

            if (Scatterer.loaded)
                Scatterer.SetupCameras(main, vesselCameras[1]);

            multiSetups.Add(setup);
        }

        private void InitialiseCameraCapture(Camera cam, bool mainCamera, string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(FilePath, mainCamera ? "Main" : "Multicam"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            if (!mainCaptureAudio || !audioOnly)
            {
                // Start recording.
                CameraCapture camCap = cam.gameObject.AddComponent<CameraCapture>();
                camCap.frameRate = Mathf.RoundToInt(playbackFramerate);
                camCap.width = cam.targetTexture.width;
                camCap.height = cam.targetTexture.height;
                camCap.outputName = fileName;
                camCap.CRF = Mathf.RoundToInt(CRF);
                camCap.drawMainUI = mainCamera && drawUIOnMain;

                camCap.path = path;
            }

            if (mainCamera && mainCaptureAudio)
            {
                if (audioOnly)
                {
                    StartCoroutine(Clapper());
                }

                audioListener = FlightCamera.fetch.mainCamera.GetComponentInChildren<AudioListener>();
                if (audioListener != null)
                {
                    audioListenerParent = audioListener.transform.parent;
                    audioListener.transform.parent = cam.transform;
                }

                mainAudioCapture = cam.gameObject.AddComponent<CaptureAudioUnity>();
                mainAudioCapture.path = Path.Combine(path, DateTime.Now.ToString("yyyy_MM_dd_HHmmss"));
            }
        }

        void RemoveVessel(Vessel vessel)
        {
            var setup = multiSetups.Find(s => s.vessel == vessel);

            // Stop the recording.
            try
            {
                setup.cameras[2].GetComponent<CameraCapture>().enabled = false;
            }
            catch { }


            // Destroy cameras.

            foreach (var cam in setup.cameras)
            {
                if (cam == null) continue;
                Destroy(cam.gameObject);
            }

            // Remove each element of the setup from the lists.
            //vesselCameraSetups.RemoveAt(index);
            //comOffsets.RemoveAt(index);
            //vesselTargets.RemoveAt(index);
            //targetVectorsLerped.RemoveAt(index);

            renderTextures.Remove(setup.rt);
            setup.rt.Release();

            multiSetups.Remove(setup);

            // The objects should be destroyed by the garbage collector now?
        }

        bool VesselValid(Vessel v)
        {
            return v != null
                && v.vesselType != VesselType.Debris
                && v.IsControllable;
        }

        private IEnumerator MultiFixedUpdate()
        {
            while (captureMulti)
            {
                UpdateMultiCaptureFixed();
                UpdateMultiCapture();

                yield return new WaitForFixedUpdate();
            }
        }

        private void UpdateMultiCapture()
        {
            Time.captureFramerate = Mathf.RoundToInt(captureFramerate);

            if (Application.targetFrameRate != Time.captureFramerate)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Time.captureFramerate;
            }

            //https://docs.unity3d.com/ScriptReference/Physics.SyncTransforms.html

            Vessel vessel;
            List<Camera> vesselCameras;
            Camera main;
            Vessel target;
            Vector3 targetVec;
            Vector3 vesselCoM;
            Vector3 toVessel;
            Vector3 toTarget;
            Vector3 targetRaw;

            List<Vessel> invalids = multiSetups.Select(s => s.vessel).ToList().FindAll(v => !VesselValid(v));
            invalids.ForEach(v => RemoveVessel(v));

            int slotsAvailable = (int)shipLimit - multiSetups.Count;
            if (slotsAvailable > 0)
            {
                var missingVessels = FlightGlobals.VesselsLoaded;
                missingVessels = missingVessels.FindAll(v => multiSetups.Find(s => s.vessel == v) == null);
                missingVessels.RemoveAll(v => !VesselValid(v));

                if (missingVessels.Count > 0)
                {
                    missingVessels = missingVessels.OrderByDescending(v => v.parts.Count).ToList();
                    missingVessels = missingVessels.Take(Mathf.Min(slotsAvailable, missingVessels.Count)).ToList();
                    missingVessels.ForEach(v => SetupVessel(v));
                }
            }

            foreach (MultiSetup setup in multiSetups)
            {
                vessel = setup.vessel;
                vesselCameras = setup.cameras;
                main = vesselCameras[2];

                if (main == null) continue;

                vesselCameras.ForEach(c => c.cullingMask &= ~(1 << 8));

                target = GetMulticamTarget(setup);

                vesselCoM = vessel.vesselTransform.TransformPoint(vessel.localCoM);

                if (target != null)
                {
                    setup.vesselTarget = target;

                    // Smooth target tracking.

                    if (setup.targetVectorLerped == Vector3.zero)
                        setup.targetVectorLerped = main.transform.forward;

                    targetVec = setup.targetVectorLerped.normalized;
                    targetRaw = target.CoM - vesselCoM;

                    main.transform.position = vesselCoM - (targetVec.normalized * cameraDistance);
                    main.transform.position += multicamUp * cameraHeight;
                    main.transform.position += Vector3.Cross(targetVec.normalized, multicamUp).normalized * cameraSlide * -1;

                    // Point the camera inbetween the vessel and the target.

                    toTarget = vesselCoM + targetVec * targetRaw.magnitude - main.transform.position;
                    toVessel = vesselCoM - main.transform.position;

                    main.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(toVessel.normalized, toTarget.normalized, 0.5f), multicamUp);
                }
                else
                {
                    // Alternatively, follow the vessel's velocity vector when there is no target.

                    float t = Mathf.Clamp01(((float)vessel.velocityD.magnitude - 5) / 5f);
                    Vector3 velocity = Vector3.Lerp(vessel.ReferenceTransform.up, vessel.velocityD.normalized, t);

                    targetVec = (vessel.situation == Vessel.Situations.ORBITING) ? vessel.ReferenceTransform.up : velocity;
                    Vector3 currentTargetVector = main.transform.position == Vector3.zero ? targetVec : setup.targetVectorLerped.normalized;

                    // TODO MAKE SURE THIS LAZY INIT IS WORKING. I DON'T THINK IT IS.

                    setup.targetVectorLerped = Vector3.Slerp(currentTargetVector, targetVec, multicamSmoothing * 50 * Time.deltaTime * 0.3f);

                    targetVec = setup.targetVectorLerped;

                    main.transform.position = vesselCoM - (targetVec.normalized * cameraDistance);
                    main.transform.position += multicamUp * Mathf.Max(cameraHeight * 0.5f, 3f);
                    //main.transform.position += Vector3.Cross(targetVec.normalized, multicamUp).normalized * cameraSlide * -1;

                    toVessel = vesselCoM - main.transform.position;

                    main.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(toVessel.normalized, targetVec.normalized, 0.5f), multicamUp);
                }

                vesselCameras[0].transform.rotation = main.transform.rotation;
                vesselCameras[1].transform.rotation = main.transform.rotation;

                foreach (var cam in vesselCameras)
                {
                    cam.fieldOfView = cameraFov;
                }

                //debug transform
                if (setup.debugTransform == null)
                {
                    setup.debugTransform = new GameObject("Multicam Transform - " + setup.vessel.name).transform;
                    var debug = setup.debugTransform.gameObject.AddComponent<DrawTransform>();
                    debug.text = setup.vessel.name;
                    debug.scale = 3;
                    debug.enabled = false;
                }

                setup.debugTransform.position = vesselCoM;

                // Lazy initialisation for the Camera Capture.

                if (!setup.captureInitialised)
                {
                    setup.captureInitialised = true;
                    InitialiseCameraCapture(main, false, vessel.persistentId.ToString());
                }
            }

            //Physics.SyncTransforms();
        }

        private void UpdateMultiCaptureFixed()
        {
            float deltaTime = useFixedUpdate ? Time.fixedDeltaTime : 1f / Time.captureFramerate;

            Vessel vessel;
            Vessel target;
            //Vector3 targetVec;
            Vector3 vesselCoM;
            Vector3 targetRaw;

            foreach (MultiSetup setup in multiSetups)
            {
                vessel = setup.vessel;
                target = setup.vesselTarget;

                //var targetObject = vessel.targetObject;
                //target = targetObject == null ? setup.vesselTarget : targetObject.GetVessel();

                if (target != null && vessel != null)
                {
                    //setup.vesselTarget = target;

                    // Constant COM.
                    vesselCoM = vessel.ReferenceTransform.TransformPoint(setup.comOffset);


                    // Extrapolate target CoM from last fixed update.
                    //Vector3 targetCoM = target.CoM

                    // Smooth target tracking.
                    targetRaw = target.CoM - vesselCoM;

                    //targetVec = Vector3.Slerp(setup.targetVectorLerped, targetRaw.normalized, multicamSmoothing).normalized;

                    Quaternion currentRot = Quaternion.LookRotation(setup.targetVectorLerped);
                    Quaternion targetRot = Quaternion.LookRotation(targetRaw.normalized);

                    // Initialisation.
                    if (setup.cameras[2].transform.position == Vector3.zero)
                        currentRot = targetRot;

                    Quaternion smoothed = SmoothDampQ(currentRot, targetRot, 
                        ref setup.smoothingVelocity, multicamSmoothing, mainMaxSpeed, deltaTime);

                    // Convert back to vector.
                    setup.targetVectorLerped = smoothed * Vector3.forward;

                    // Initialisation.
                    if (setup.vessel.ReferenceTransform.forward == setup.targetVectorLerped)
                        setup.targetVectorLerped = targetRaw.normalized;
                }
            }
        }

        private static Vessel GetMulticamTarget(MultiSetup setup)
        {
            Vessel target;

            if (BD.BDLoaded)
            {
                if (setup.hasBDAI && setup.BDAIModule != null)
                {
                    target = (Vessel)BD.bdTargetField.GetValue(setup.BDAIModule);

                    // We want to add a latency of 3 seconds to the BD target.
                    // This is so we can always see it for a few seconds after it's been destroyed.

                    if (target != setup.BDTarget)
                    {
                        setup.BDTarget = target;
                        setup.BDTargetTime = Time.time;
                    }

                    if (Time.time - setup.BDTargetTime < 3)
                        return setup.vesselTarget;
                    else if (target != null)
                        return target;
                }
            }

            if (setup.vessel.targetObject != null)
            {
                return setup.vessel.targetObject.GetVessel();
            }
            else
            {
                return setup.vesselTarget;
            }
        }

        #endregion

        #region Preview

        private void InitPreviewMaterial()
        {
            if (renderTextureMaterial != null)
                return;

            renderTextureMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            renderTextureMaterial.color = Color.white * 2;
        }

        private void DrawMulticamGUI()
        {
            if (!CaptureMulti)
                return;

            if (Event.current.type.Equals(EventType.Repaint) && showPreview)
            {
                RenderTexture tex;
                int count = renderTextures.Count;
                float r = 1 / (Mathf.Floor(Mathf.Sqrt(count - 0.1f)) + 1);

                for (int i = 0; i < count; i++)
                {
                    tex = renderTextures[i];

                    float x = Screen.width * (i * r) % Screen.width;
                    //float y = Screen.height * (i * 0.5f) % Screen.height;
                    //float y = 2 % (i + 1)
                    float y = Screen.height * r * Mathf.Floor(i * r);

                    Graphics.DrawTexture(new Rect(x, y, Screen.width * r, Screen.height * r), tex, renderTextureMaterial);
                }
            }
        }

        private void DrawMainCamGUI()
        {
            if (!CaptureMain)
                return;

            if (Event.current.type.Equals(EventType.Repaint) && showPreview
                && mainRenderTexture != null)
            {
                float scaleFactor = (Screen.height * 0.25f) / mainHeight;
                float width = mainRenderTexture.width * scaleFactor;
                float height = mainRenderTexture.height * scaleFactor;
                int margin = Mathf.RoundToInt(Screen.height * 0.1f);

                // 3/4 along the screen, 1/4 up.

                // Width - margin - width of texture.
                //float x = Screen.width - Screen.width * 0.1f * Screen.height / Screen.width - width;
                float x = Screen.width - margin - width;
                float y = margin;

                var rect = new Rect(x, y, width, height);
                Graphics.DrawTexture(rect, mainRenderTexture, renderTextureMaterial);
            }
        }

        #endregion

        #region Helpers

        private static void CopyCameraPostProcess(Camera template, Camera camera)
        {
            Component templateLayer = template.gameObject.GetComponent("PostProcessLayer");
            if (templateLayer != null)
            {
                Type layerType = templateLayer.GetType();
                Component layer = camera.gameObject.AddComponent(layerType);

                // set the volume layer.
                var volumeLayer = layerType.GetField("volumeLayer", BindingFlags.Public | BindingFlags.Instance);
                volumeLayer.SetValue(layer, volumeLayer.GetValue(templateLayer));

                // set depth falgs to motion and vector
                //var depthFlags = layerType.GetProperty("cameraDepthFlags", BindingFlags.Public | BindingFlags.Instance);
                //depthFlags.SetValue(layer, depthFlags.GetValue(templateLayer));

                // copy m_OldResources, m_Resources
                //var oldResources = layerType.GetField("m_OldResources", BindingFlags.NonPublic | BindingFlags.Instance);
                //oldResources.SetValue(layer, oldResources.GetValue(templateLayer));

                var resources = layerType.GetField("m_Resources", BindingFlags.NonPublic | BindingFlags.Instance);
                resources.SetValue(layer, resources.GetValue(templateLayer));
            }
        }

        private static bool GetCameraPostProcessEnabled(Camera cam)
        {
            Behaviour postProcessLayer = cam.gameObject.GetComponent("PostProcessLayer") as Behaviour;
            return postProcessLayer != null && postProcessLayer.enabled;
        }

        private static void ToggleCameraPostProcess(Camera cam, bool enabled)
        {
            Behaviour postProcessLayer = cam.gameObject.GetComponent("PostProcessLayer") as Behaviour;
            if (postProcessLayer != null)
            {
                postProcessLayer.enabled = false;
            }
        }

        private void BlockHighlighters(bool block)
        {
            var parts = HighLogic.LoadedSceneIsFlight ? FlightGlobals.VesselsLoaded.SelectMany(v => v.parts) : EditorLogic.SortedShipList;

            // set highlightBlocked to true via reflection.
            var highlightBlocked = typeof(Part).GetField("highlightBlocked", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var part in parts)
                highlightBlocked.SetValue(part, block);
        }

        public static Quaternion SmoothDampQ(Quaternion rot, Quaternion target, ref Quaternion deriv, float time, float maxSpeed, float deltaTime)
        {
            if (Time.unscaledDeltaTime < Mathf.Epsilon) return rot;
            // account for double-cover
            var Dot = Quaternion.Dot(rot, target);
            var Multi = Dot > 0f ? 1f : -1f;
            target.x *= Multi;
            target.y *= Multi;
            target.z *= Multi;
            target.w *= Multi;
            // smooth damp (nlerp approx)
            var Result = new Vector4(
                Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time, maxSpeed, deltaTime),
                Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time, maxSpeed, deltaTime),
                Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time, maxSpeed, deltaTime),
                Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time, maxSpeed, deltaTime)
            ).normalized;

            // ensure deriv is tangent
            var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
            deriv.x -= derivError.x;
            deriv.y -= derivError.y;
            deriv.z -= derivError.z;
            deriv.w -= derivError.w;

            return new Quaternion(Result.x, Result.y, Result.z, Result.w);
        }

        #endregion

        #region Clapper

        IEnumerator Clapper()
        {
            yield return null;

            showClapper = true;
            
            // Play a loud sound for syncing.
            var clip = GameDatabase.Instance.GetAudioClip("CaptureTools/Sounds/clapper");

            if (clip != null)
            {
                var audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.volume = 1f;
                audioSource.spatialBlend = 0f;
                audioSource.PlayOneShot(clip);
                Destroy(audioSource, clip.length);
            }

            yield return new WaitForSecondsRealtime(0.25f);

            showClapper = false;
        }

        #endregion

    }
}
