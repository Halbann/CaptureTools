// FFmpegOut - FFmpeg video encoding plugin for Unity
// https://github.com/keijiro/KlakNDI

using System;
using System.Collections;
using UnityEngine;
using KSP.UI;
using CaptureTools.Utils;

namespace FFmpegOut
{
    [AddComponentMenu("FFmpegOut/Camera Capture")]
    public sealed class CameraCapture : MonoBehaviour
    {
        #region Public properties

        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public float Framerate { get; set; } = 60;

        // Capture Tools additions.

        public string outputName = "";
        public int CRF = 15;
        public string path = "";
        public bool drawMainUI = false;
        public event Action OnError;

        #endregion

        #region Private members

        FFmpegSession _session;
        RenderTexture _tempRT;
        GameObject _blitter;

        public static RenderTextureFormat GetTargetFormat(Camera camera)
        {
            return camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        }

        int GetAntiAliasingLevel(Camera camera)
        {
            return camera.allowMSAA ? QualitySettings.antiAliasing : 1;
        }

        #endregion

        #region Time-keeping variables

        int _frameCount;
        float _startTime;
        int _frameDropCount;

        float FrameTime {
            get { return _startTime + ((_frameCount - 0.5f) / Framerate); }
        }

        void WarnFrameDrop()
        {
            if (++_frameDropCount != 10) return;

            CTDebug.LogWarning(
                "Significant frame droppping was detected. This may introduce " +
                "time instability into output video. Decreasing the recording " +
                "frame rate is recommended."
            );
        }

        #endregion

        #region MonoBehaviour implementation

        void OnValidate()
        {
            Width = Mathf.Max(8, Width);
            Height = Mathf.Max(8, Height);
        }

        void OnDisable()
        {
            if (_session != null)
            {
                // Close and dispose the FFmpeg session.
                _session.Close();
                _session.Dispose();
                _session = null;
            }

            if (_tempRT != null)
            {
                // Dispose the frame texture.
                GetComponent<Camera>().targetTexture = null;
                Destroy(_tempRT);
                _tempRT = null;
            }

            if (_blitter != null)
            {
                // Destroy the blitter game object.
                Destroy(_blitter);
                _blitter = null;
            }
        }

        IEnumerator Start()
        {
            // Sync with FFmpeg pipe thread at the end of every frame.
            for (var eof = new WaitForEndOfFrame(); ;)
            {
                yield return eof;

                try
                {
                    _session.CompletePushFrames();
                }
                catch (Exception e)
                {
                    CTDebug.LogError("FFmpeg session failed while waiting to sync : " + e.Message);
                    Error();
                    break;
                }
            }
        }

        private void Update()
        {
            if (!enabled)
                return;

            var camera = GetComponent<Camera>();

            // Lazy initialization
            if (_session == null)
                CreateSession(camera);

            try
            {
                UpdateSession(camera);
            }
            catch (Exception e)
            {
                CTDebug.LogError("FFmpeg session failed during an update: " + e.Message);
                Error();
                return;
            }
        }

        private void CreateSession(Camera camera)
        {
            // Give a newly created temporary render texture to the camera
            // if it's set to render to a screen. Also create a blitter
            // object to keep frames presented on the screen.
            if (camera.targetTexture == null)
            {
                _tempRT = new RenderTexture(Width, Width, 24, GetTargetFormat(camera));
                _tempRT.antiAliasing = GetAntiAliasingLevel(camera);
                camera.targetTexture = _tempRT;
                _blitter = Blitter.CreateInstance(camera);
            }

            try
            {
                // Start an FFmpeg session.
                _session = FFmpegSession.Create(
                    outputName,
                    camera.targetTexture.width,
                    camera.targetTexture.height,
                    Framerate, CRF, path
                );
            }
            catch (Exception e)
            {
                CTDebug.LogError("Failed to create FFmpeg session: " + e.Message);
                Error();

                return;
            }

            _startTime = Time.time;
            //_startTime = Time.unscaledTime;
            _frameCount = 0;
            _frameDropCount = 0;
        }

        private void UpdateSession(Camera camera)
        {
            var gap = Time.time - FrameTime;
            //var gap = Time.unscaledTime - FrameTime;
            var delta = 1 / Framerate;

            if (gap < 0)
            {
                // Update without frame data.
                _session.PushFrame(null);
            }
            else if (gap < delta)
            {
                // Single-frame behind from the current time:
                // Push the current frame to FFmpeg.
                _session.PushFrame(camera.targetTexture);
                _frameCount++;
            }
            else if (gap < delta * 2)
            {
                // Two-frame behind from the current time:
                // Push the current frame twice to FFmpeg. Actually this is not
                // an efficient way to catch up. We should think about
                // implementing frame duplication in a more proper way. #fixme
                _session.PushFrame(camera.targetTexture);
                _session.PushFrame(camera.targetTexture);
                _frameCount += 2;
            }
            else
            {
                // Show a warning message about the situation.
                WarnFrameDrop();

                // Push the current frame to FFmpeg.
                _session.PushFrame(camera.targetTexture);

                // Compensate the time delay.
                _frameCount += Mathf.FloorToInt(gap * Framerate);
            }
        }

        private void Error()
        {
            OnError?.Invoke();
            enabled = false;
            CTDebug.LogError("Ended camera capture due to an error.");
        }

        #endregion

        #region Capture Tools Flight GUI

        private static int lastUIRenderFrame = -1;

        void OnGUI()
        {
            if (!drawMainUI)
                return;

            if (Event.current.type != EventType.Repaint)
                return;

            if (!name.Contains("Camera 00"))
                return;

            UpdateDrawUI();

            if (lastUIRenderFrame != Time.frameCount && UIMasterController.Instance.IsUIShowing)
            {
                lastUIRenderFrame = Time.frameCount;
                UIMainCamera.Camera.Render();
            }
        }

        void UpdateDrawUI()
        {
            if (!name.Contains("Camera 00"))
                return;

            if (UIMasterController.Instance.IsUIShowing)
            {
                var camera = GetComponent<Camera>();
                UIMainCamera.Camera.enabled = false;
                UIMainCamera.Camera.targetTexture = camera.targetTexture;
                UIMainCamera.Camera.Render();
                UIMainCamera.Camera.targetTexture = null;
            }
        }

        #endregion
    }
}
