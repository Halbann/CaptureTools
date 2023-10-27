// FFmpegOut - FFmpeg video encoding plugin for Unity
// https://github.com/keijiro/KlakNDI

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;
using UnityEngine.Playables;
using System.IO;

namespace FFmpegOut
{
    public sealed class FFmpegSession : System.IDisposable
    {
        #region Factory methods

        public static FFmpegSession Create(
            string name,
            int width, int height, float frameRate,
            FFmpegPreset preset, int CRF, string path = ""
        )
        {
            name = DateTime.Now.ToString("yyyy MM dd HHmmss") + (name == "" ? "" : (" " + name));

            if (path == "")
                path = name.Replace(" ", "_") + preset.GetSuffix();
            else
                path = Path.Combine(path, name.Replace(" ", "_") + preset.GetSuffix());

            return CreateWithOutputPath(path, width, height, frameRate, preset, CRF);
        }

        public static FFmpegSession CreateWithOutputPath(
            string outputPath,
            int width, int height, float frameRate,
            FFmpegPreset preset, int CRF
        )
        {
            TimeSpan timespan = TimeSpan.FromSeconds(Time.time);
            int ff = (int)Mathf.Floor(frameRate * (timespan.Milliseconds / 1000f));
            string timecode = string.Format(
                "{0:00}:{1:00}:{2:00}:{3:00}",
                timespan.Hours, timespan.Minutes, 
                timespan.Seconds, ff);

            string pathString = " \"" + outputPath + "\"";

            return new FFmpegSession(
                "-r " + (int)frameRate
                + " -y -f rawvideo -vcodec rawvideo -pixel_format rgba"
                + " -colorspace bt709"
                + " -video_size " + width + "x" + height
                + " -framerate " + (int)frameRate
                + " -loglevel warning -i - " + preset.GetOptions()
                //+ " -metadata creation_time=\"$(date +'%F %T')\""
                + " -timecode " + timecode
                + " -crf " + CRF.ToString()
                + pathString
            );
        }

        public static FFmpegSession CreateWithArguments(string arguments)
        {
            return new FFmpegSession(arguments);
        }

        #endregion

        #region Public properties and members

        public void PushFrame(Texture source)
        {
            if (_pipe != null)
            {
                ProcessQueue();
                if (source != null) QueueFrame(source);
            }
        }

        public void CompletePushFrames()
        {
            _pipe?.SyncFrameData();
        }

        public void Close()
        {
            if (_pipe != null)
            {
                var error = _pipe.CloseAndGetOutput();

                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning(
                        "FFmpeg returned with warning/error messages. " +
                        "See the following lines for details:\n" + error
                    );

                _pipe.Dispose();
                _pipe = null;
            }

            if (_blitMaterial != null)
            {
                UnityEngine.Object.Destroy(_blitMaterial);
                _blitMaterial = null;
            }
        }

        public void Dispose()
        {
            Close();
        }

        #endregion

        #region Private objects and constructor/destructor

        FFmpegPipe _pipe;
        Material _blitMaterial;

        FFmpegSession(string arguments)
        {
            if (!FFmpegPipe.IsAvailable)
                Debug.LogWarning(
                    "Failed to initialize an FFmpeg session due to missing " +
                    "executable file. Please check FFmpeg installation."
                );
            else if (!UnityEngine.SystemInfo.supportsAsyncGPUReadback)
                Debug.LogWarning(
                    "Failed to initialize an FFmpeg session due to lack of " +
                    "async GPU readback support. Please try changing " +
                    "graphics API to readback-enabled one."
                );
            else
                _pipe = new FFmpegPipe(arguments);
        }

        ~FFmpegSession()
        {
            if (_pipe != null)
                Debug.LogError(
                    "An unfinalized FFmpegCapture object was detected. " +
                    "It should be explicitly closed or disposed " +
                    "before being garbage-collected."
                );
        }

        #endregion

        #region Frame readback queue

        List<AsyncGPUReadbackRequest> _readbackQueue =
            new List<AsyncGPUReadbackRequest>(4);

        void QueueFrame(Texture source)
        {
            if (_readbackQueue.Count > 6)
            {
                Debug.LogWarning("Too many GPU readback requests.");
                return;
            }

            // Lazy initialization of the preprocessing blit shader
            if (_blitMaterial == null)
            {
                var shader = FFmpegOutAssets.preprocessShader;
                _blitMaterial = new Material(shader);
            }

            // Blit to a temporary texture and request readback on it.
            var rt = RenderTexture.GetTemporary
                (source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt, _blitMaterial, 0);
            _readbackQueue.Add(AsyncGPUReadback.Request(rt));
            RenderTexture.ReleaseTemporary(rt);
        }

        void ProcessQueue()
        {
            while (_readbackQueue.Count > 0)
            {
                // Check if the first entry in the queue is completed.
                if (!_readbackQueue[0].done)
                {
                    // Detect out-of-order case (the second entry in the queue
                    // is completed before the first entry).
                    if (_readbackQueue.Count > 1 && _readbackQueue[1].done)
                    {
                        // We can't allow the out-of-order case, so force it to
                        // be completed now.
                        _readbackQueue[0].WaitForCompletion();
                    }
                    else
                    {
                        // Nothing to do with the queue.
                        break;
                    }
                }

                // Retrieve the first entry in the queue.
                var req = _readbackQueue[0];
                _readbackQueue.RemoveAt(0);

                // Error detection
                if (req.hasError)
                {
                    Debug.LogWarning("GPU readback error was detected.");
                    continue;
                }

                // Feed the frame to the FFmpeg pipe.
                _pipe.PushFrameData(req.GetData<byte>());
            }
        }

        #endregion
    }
}
