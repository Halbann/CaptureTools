using System;
using UnityEngine;
using System.Reflection;
using Unity.Collections;
using static UnityEngine.AudioRenderer;

namespace UnityEditor.Recorder.Input
{
    sealed class MyAudioRendererWrapper
    {
        MyAudioRendererWrapper()
        {
        }

        public static void Start()
        {
            AudioRenderer.Start();
        }

        public static void Stop()
        {
            AudioRenderer.Stop();
        }

        public static uint GetSampleCountForCaptureFrame()
        {
            return (uint)AudioRenderer.GetSampleCountForCaptureFrame();
        }

        public static void Render(NativeArray<float> buffer)
        {
            AudioRenderer.Render(buffer);
        }

        public static bool IsInstanceActive()
        {
            return true;
        }
    }

    /// <summary>
    /// Use this class to record audio from the built-in Unity audio system.
    /// </summary>
    public class AudioInput
    {
        class BufferManager : IDisposable
        {
            readonly NativeArray<float>[] m_Buffers;

            public BufferManager(ushort bufferCount, uint sampleFrameCount, ushort channelCount)
            {
                m_Buffers = new NativeArray<float>[bufferCount];
                for (int i = 0; i < m_Buffers.Length; ++i)
                    m_Buffers[i] = new NativeArray<float>((int)sampleFrameCount * channelCount, Allocator.Persistent);
            }

            public NativeArray<float> GetBuffer(int index)
            {
                return m_Buffers[index];
            }

            public void Dispose()
            {
                foreach (var a in m_Buffers)
                    a.Dispose();
            }
        }

        ushort m_ChannelCount;

        /// <summary>
        /// The number of channels in the audio input.
        /// </summary>
        public ushort ChannelCount
        {
            get { return m_ChannelCount; }
        }

        /// <summary>
        /// The sampling rate, in hertz.
        /// </summary>
        public int SampleRate
        {
            get { return UnityEngine.AudioSettings.outputSampleRate; }
        }

        /// <summary>
        /// Get the size of the buffer of audio samples (including all channels).
        /// </summary>
        /// <returns></returns>
        public int GetBufferSize()
        {
            return MainBuffer.Length;
        }

        /// <summary>
        /// Get the buffer of audio samples.
        /// </summary>
        /// <param name="userArray">A native array of float that is supplied and managed by the user.</param>
        /// <param name="writtenSize">The number of values that were written to the supplied array.</param>
        /// <exception cref="ArgumentException">Throws an exception if the passed array is too small to hold the buffer data.</exception>
        public void GetBuffer(ref NativeArray<float> userArray, out int writtenSize)
        {
            var buff = MainBuffer;
            if (userArray.Length < buff.Length)
                throw new ArgumentException(
                    $"The supplied array (size {userArray.Length}) must be larger than or of the same size as the audio sample buffer (size {buff.Length})");

            userArray.GetSubArray(0, buff.Length).CopyFrom(buff);
            writtenSize = buff.Length;
        }

        internal NativeArray<float> MainBuffer
        {
            get { return s_BufferManager.GetBuffer(0); }
        }

        static AudioInput s_Handler;
        static BufferManager s_BufferManager;

        protected internal void BeginRecording()
        {
            //m_ChannelCount = new Func<ushort>(() => {
            //    switch (UnityEngine.AudioSettings.speakerMode)
            //    {
            //        case AudioSpeakerMode.Mono: return 1;
            //        case AudioSpeakerMode.Stereo: return 2;
            //        case AudioSpeakerMode.Quad: return 4;
            //        case AudioSpeakerMode.Surround: return 5;
            //        case AudioSpeakerMode.Mode5point1: return 6;
            //        case AudioSpeakerMode.Mode7point1: return 7;
            //        case AudioSpeakerMode.Prologic: return 2;
            //        default: return 1;
            //    }
            //})();

            m_ChannelCount = 2;

            //Debug.Log(string.Format("AudioInput.BeginRecording for capture frame rate {0}", Time.captureFramerate));

            if (ShouldCaptureAudio())
                MyAudioRendererWrapper.Start();
        }

        protected internal void NewFrameReady()
        {
            if (!ShouldCaptureAudio())
                return;

            if (s_Handler == null)
                s_Handler = this;

            if (s_Handler == this)
            {
                var sampleFrameCount = MyAudioRendererWrapper.GetSampleCountForCaptureFrame();
                //Debug.Log(string.Format("AudioInput.NewFrameReady {0} audio sample frames @ {1} ch",
                //    sampleFrameCount, m_ChannelCount));

                const ushort bufferCount = 1;

                s_BufferManager?.Dispose();
                s_BufferManager = new BufferManager(bufferCount, sampleFrameCount, m_ChannelCount);

                MyAudioRendererWrapper.Render(MainBuffer);
            }
        }

        internal void SkipFrame()
        {
            // Audio input must render the audio frame when a frame is skipped
            NewFrameReady();
        }

        protected internal void EndRecording()
        {
            if (ShouldCaptureAudio() && MyAudioRendererWrapper.IsInstanceActive())
                MyAudioRendererWrapper.Stop();

            if (s_BufferManager != null)
            {
                s_BufferManager.Dispose();
                s_BufferManager = null;
            }

            s_Handler = null;
        }

        // This is a workaround for the fact that the input wants to capture the audio, but the (movie)recorder does not support it.
        // This is a non-persistant way to disable the audio capture.
        bool ShouldCaptureAudio()
        {
            return true;
        }
    }
}