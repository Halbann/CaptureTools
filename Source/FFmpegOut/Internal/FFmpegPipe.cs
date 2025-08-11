// FFmpegOut - FFmpeg video encoding plugin for Unity
// https://github.com/keijiro/KlakNDI

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.Collections;
using CaptureTools.Utils;

namespace FFmpegOut
{
    public sealed class FFmpegPipe : IDisposable
    {
        public static string[] errorKeywords = { "error", "failed", "invalid", "unable", "not found" };
        private readonly ConcurrentQueue<string> errorQueue = new ConcurrentQueue<string>();

        #region Public methods

        public static bool IsAvailable
        {
            get { return File.Exists(ExecutablePath); }
        }

        public FFmpegPipe(string arguments)
        {
            CTDebug.Log("Creating new FFmpeg pipe. Arguments: " + arguments);

            // Start FFmpeg subprocess.
            _subprocess = Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (_subprocess == null || _subprocess.HasExited)
                throw new Exception("Failed to start FFmpeg subprocess.");

            _subprocess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorQueue.Enqueue(e.Data);
            };

            _subprocess.BeginErrorReadLine();

            // Start copy/pipe subthreads.
            _copyThread = new Thread(CopyThread);
            _pipeThread = new Thread(PipeThread);
            _copyThread.Start();
            _pipeThread.Start();
        }

        private void ReadError()
        {
            while (errorQueue.TryDequeue(out string error))
            {
                if (string.IsNullOrEmpty(error))
                    continue;

                if (!errorKeywords.Any(k => error.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    CTDebug.Log("FFmpeg output: " + error);
                    continue;
                }

                CTDebug.LogError("FFmpeg error detected: " + error);

                throw new Exception("Encountered an error in the FFmpeg subprocess. " +
                                    "Please check the log for details.");
            }
        }

        public void PushFrameData(NativeArray<byte> data)
        {
            ReadError();

            // Update the copy queue and notify the copy thread with a ping.
            lock (_copyQueue) _copyQueue.Enqueue(data);
            _copyPing.Set();
        }

        public void SyncFrameData()
        {
            ReadError();

            // Wait for the copy queue to get emptied with using pong
            // notification signals sent from the copy thread.
            while (_copyQueue.Count > 0) _copyPong.WaitOne();

            // When using a slower codec (e.g. HEVC, ProRes), frames may be
            // queued too much, and it may end up with an out-of-memory error.
            // To avoid this problem, we wait for pipe queue entries to be
            // comsumed by the pipe thread.
            while (_pipeQueue.Count > 4) _pipePong.WaitOne();
        }

        public void Close()
        {
            // Terminate the subthreads.
            _terminate = true;

            _copyPing.Set();
            _pipePing.Set();

            _copyThread.Join();
            _pipeThread.Join();

            // Close FFmpeg subprocess.
            _subprocess.CancelErrorRead();
            _subprocess.StandardInput.Close();
            _subprocess.WaitForExit();

            _subprocess.Close();
            _subprocess.Dispose();

            // Nullify members (just for ease of debugging).
            _subprocess = null;
            _copyThread = null;
            _pipeThread = null;
            _copyQueue = null;
            _pipeQueue = _freeBuffer = null;
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            if (!_terminate) Close();
        }

        ~FFmpegPipe()
        {
            if (!_terminate)
            {
                CTDebug.LogError(
                    "An unfinalized FFmpegPipe object was detected. " +
                    "It should be explicitly closed or disposed " +
                    "before being garbage-collected."
                );
            }
        }

        #endregion

        #region Private members

        Process _subprocess;
        Thread _copyThread;
        Thread _pipeThread;

        readonly AutoResetEvent _copyPing = new AutoResetEvent(false);
        readonly AutoResetEvent _copyPong = new AutoResetEvent(false);
        readonly AutoResetEvent _pipePing = new AutoResetEvent(false);
        readonly AutoResetEvent _pipePong = new AutoResetEvent(false);
        bool _terminate;

        Queue<NativeArray<byte>> _copyQueue = new Queue<NativeArray<byte>>();
        Queue<byte[]> _pipeQueue = new Queue<byte[]>();
        Queue<byte[]> _freeBuffer = new Queue<byte[]>();

        public static string ExecutablePath
        {
            get
            {
                // todo: support arbitrary locations and system/user PATH.

                string basePath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "CaptureTools", "FFmpeg");
                UnityEngine.RuntimePlatform platform = UnityEngine.Application.platform;

                if (platform == UnityEngine.RuntimePlatform.OSXPlayer || platform == UnityEngine.RuntimePlatform.OSXEditor)
                    return basePath + "/macOS/ffmpeg";

                if (platform == UnityEngine.RuntimePlatform.LinuxPlayer || platform == UnityEngine.RuntimePlatform.LinuxEditor)
                    return basePath + "/Linux/ffmpeg";

                return basePath + "/Windows/ffmpeg.exe";
            }
        }

        #endregion

        #region Subthread entry points

        // CopyThread - Copies frames given from the readback queue to the pipe
        // queue. This is required because readback buffers are not under our
        // control -- they'll be disposed before being processed by us. They
        // have to be buffered by end-of-frame.
        void CopyThread()
        {
            while (!_terminate)
            {
                // Wait for ping from the main thread.
                _copyPing.WaitOne();

                // Process all entries in the copy queue.
                while (_copyQueue.Count > 0)
                {
                    // Retrieve an copy queue entry without dequeuing it.
                    // (We don't want to notify the main thread at this point.)
                    NativeArray<byte> source;
                    lock (_copyQueue) source = _copyQueue.Peek();

                    // Try allocating a buffer from the free buffer list.
                    byte[] buffer = null;
                    if (_freeBuffer.Count > 0)
                        lock (_freeBuffer) buffer = _freeBuffer.Dequeue();

                    // Copy the contents of the copy queue entry.
                    if (buffer == null || buffer.Length != source.Length)
                        buffer = source.ToArray();
                    else
                        source.CopyTo(buffer);

                    // Push the buffer entry to the pipe queue.
                    lock (_pipeQueue) _pipeQueue.Enqueue(buffer);
                    _pipePing.Set(); // Ping the pipe thread.

                    // Dequeue the copy buffer entry and ping the main thread.
                    lock (_copyQueue) _copyQueue.Dequeue();
                    _copyPong.Set();
                }
            }
        }

        // PipeThread - Receives frame entries from the copy thread and push
        // them into the FFmpeg pipe.
        void PipeThread()
        {
            Stream pipe = _subprocess.StandardInput.BaseStream;

            while (!_terminate)
            {
                // Wait for the ping from the copy thread.
                _pipePing.WaitOne();

                // Process all entries in the pipe queue.
                while (_pipeQueue.Count > 0)
                {
                    // Retrieve a frame entry.
                    byte[] buffer;
                    lock (_pipeQueue) buffer = _pipeQueue.Dequeue();

                    // Write it into the FFmpeg pipe.
                    try
                    {
                        pipe.Write(buffer, 0, buffer.Length);
                        pipe.Flush();
                    }
                    catch
                    {
                        // Pipe.Write could raise an IO exception when ffmpeg
                        // is terminated for some reason. We just ignore this
                        // situation and assume that it will be resolved in the
                        // main thread. #badcode
                    }

                    // Add the buffer to the free buffer list to reuse later.
                    lock (_freeBuffer) _freeBuffer.Enqueue(buffer);
                    _pipePong.Set();
                }
            }
        }

        #endregion
    }
}
