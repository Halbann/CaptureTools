using System;
using UnityEditor.Recorder;
using UnityEngine;
using CaptureTools.Utils;

namespace CaptureTools
{
    public class CaptureAudioUnity : MonoBehaviour
    {
        public string path = "";
        public bool debug = false;
        public bool record = true;

        private readonly AudioRecorder recorder = new AudioRecorder();
        private bool recording = false;
        private static double dspStartTime = 0f;
        private static float startTime = 0f;
        private static float unscaledStartTime = 0f;

        // Start is called before the first frame update
        internal void Start()
        {
            dspStartTime = AudioSettings.dspTime;
            startTime = Time.time;
            unscaledStartTime = Time.unscaledTime;
        }

        // Update is called once per frame
        internal void Update()
        {
            if (record != recording)
            {
                if (record)
                {
                    if (path?.Length == 0)
                        path = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

                    recording = true;

                    try
                    {
                        recorder.BeginRecording(path);
                    }
                    catch (Exception e)
                    {
                        CTDebug.LogError("Couldn't create audio file. Probably permissions related.");
                        CTDebug.LogError(e.ToString());
                    }
                }
                else
                {
                    recording = false;
                    recorder.EndRecording();
                }
            }
        }

        internal void LateUpdate()
        {
            if (recording)
            {
                recorder.RecordFrame();

                if (debug)
                {
                    CTDebug.Log(
                        $"Count: {Time.frameCount} " +
                        $"DSP: {AudioSettings.dspTime - dspStartTime} " +
                        $"Time: {Time.time - startTime} " +
                        $"Unscaled: {Time.unscaledTime - unscaledStartTime}");
                }
            }
        }

        internal void OnDestroy()
        {
            recording = false;
            record = false;
            recorder.EndRecording();
        }
    }
}
