using System;
using System.IO;
using Unity.Collections;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace UnityEditor.Recorder
{
    class AudioRecorder
    {
        internal WAVEncoder m_Encoder;
        internal AudioInput m_AudioInput;

        protected internal bool BeginRecording(string path)
        {
            //string dateTime = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            m_Encoder = new WAVEncoder(path + ".wav");
            m_AudioInput = new AudioInput();

            m_AudioInput.BeginRecording();
            return true;
        }

        protected internal void RecordFrame()
        {
            m_AudioInput.NewFrameReady();

            //var audioInput = (AudioInput)m_Inputs[0];
            var audioInput = m_AudioInput;

            m_Encoder.AddSamples(audioInput.MainBuffer);
        }

        protected internal void EndRecording()
        {
            m_AudioInput.EndRecording();

            if (m_Encoder != null)
            {
                m_Encoder.Dispose();
                m_Encoder = null;
            }
        }
    }

    /// <summary>
    /// An encoder for the WAV format.
    /// </summary>
    public class WAVEncoder : IDisposable
    {
        BinaryWriter _binwriter;

        /// <summary>
        /// The constructor of a WAV encoder.
        /// </summary>
        /// <param name="filename">The path of the WAV file to create.</param>
        public WAVEncoder(string filename)
        {
            var stream = new FileStream(filename, FileMode.Create);
            _binwriter = new BinaryWriter(stream);
            for (int n = 0; n < 44; n++)
                _binwriter.Write((byte)0);
        }

        /// <summary>
        /// Stop the encoder and close the file.
        /// </summary>
        public void Stop()
        {
            var closewriter = _binwriter;
            _binwriter = null;
            int subformat = 3; // float
            //uint numchannels = UnityHelpers.GetNumAudioChannels();
            uint numchannels = 2;
            int numbits = 32;
            int samplerate = AudioSettings.outputSampleRate;

            //Debug.Log("Closing file");

            long pos = closewriter.BaseStream.Length;
            closewriter.Seek(0, SeekOrigin.Begin);
            closewriter.Write((byte)'R'); closewriter.Write((byte)'I'); closewriter.Write((byte)'F'); closewriter.Write((byte)'F');
            closewriter.Write((uint)(pos - 8));
            closewriter.Write((byte)'W'); closewriter.Write((byte)'A'); closewriter.Write((byte)'V'); closewriter.Write((byte)'E');
            closewriter.Write((byte)'f'); closewriter.Write((byte)'m'); closewriter.Write((byte)'t'); closewriter.Write((byte)' ');
            closewriter.Write((uint)16);
            closewriter.Write((ushort)subformat);
            closewriter.Write((ushort)numchannels);
            closewriter.Write((uint)samplerate);
            closewriter.Write((uint)((samplerate * numchannels * numbits) / 8));
            closewriter.Write((ushort)((numchannels * numbits) / 8));
            closewriter.Write((ushort)numbits);
            closewriter.Write((byte)'d'); closewriter.Write((byte)'a'); closewriter.Write((byte)'t'); closewriter.Write((byte)'a');
            closewriter.Write((uint)(pos - 36));
            closewriter.Seek((int)pos, SeekOrigin.Begin);
            closewriter.Flush();
            closewriter.Close();
        }

        /// <summary>
        /// Add audio samples to the WAV file.
        /// </summary>
        /// <param name="data">The buffer of audio samples to add.</param>
        public void AddSamples(NativeArray<float> data)
        {
            //Debug.Log("Writing wav chunk " + data.Length);

            if (_binwriter == null)
                return;

            for (int n = 0; n < data.Length; n++)
                _binwriter.Write(data[n]);
        }

        /// <summary>
        /// Stop the encoder.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}