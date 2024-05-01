using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System;
using System.Globalization;
using Contracts.Predicates;
using static UnityEngine.UI.BoxSlider;

namespace CaptureTools
{
    partial class CaptureTools
    {
        private bool capturingTrace = false;

        internal static string tracesPath;
        private static string[] excludedParts = new string[] { "strutConnector", "fuelLine", "parachuteRadial" };

        //private static List<int> traceFramerates = new List<int>() { 1, 2, 5, 10, 25, 50 };
        //private static float traceFramerateSlider = 4;
        internal static float traceFramerate = 24;
        internal static float traceInterval;
        private static float traceStartTime;
        internal static int frameCount;

        private List<TraceRecorder> traceRecorders;

        internal static Transform traceTransform;
        //private Vector3 centreVelocity;

        public TraceFrame traceFrameOfReference = TraceFrame.World;
        public enum TraceFrame
        {
            World,
            Vessel
        }

        private void StartPartCapture()
        {
            if (capturingTrace)
                return;

            capturingTrace = true;

            TraceRecorder.recordedFrames = 0;
            frameCount = 0;
            traceStartTime = Time.time;
            traceInterval = Mathf.RoundToInt(1f / traceFramerate / Time.fixedUnscaledDeltaTime);

            traceRecorders = new List<TraceRecorder>();

            // Centre. Trace is recorded relative to this.
            traceTransform = new GameObject().transform;
            traceTransform.gameObject.name = "Trace Centre";
            traceTransform.position = Vector3.zero;
            traceTransform.rotation = FlightCamera.fetch.getReferenceFrame();

            // Centre marker.
            if (true)
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                var mr = capsule.GetComponent<MeshRenderer>();

                Material sphereMat = new Material(Shader.Find("Unlit/Color"));
                sphereMat.color = Color.magenta;

                mr.material = sphereMat;

                capsule.transform.localScale = capsule.transform.localScale * 6;
                Destroy(capsule.GetComponent<CapsuleCollider>());

                capsule.transform.SetParent(traceTransform, false);
                capsule.transform.localPosition = Vector3.zero;
                capsule.transform.localRotation = Quaternion.identity;
            }

            // Add a recorder to every part.
            List<Part> parts = FlightGlobals.VesselsLoaded.SelectMany(v => v.Parts).ToList();
            parts.RemoveAll(p => excludedParts.Contains(p.name));

            foreach (Part p in parts)
            {
                traceRecorders.Add(p.gameObject.AddComponent<TraceRecorder>());
            }

            // Register events.
            GameEvents.onPartDie.Add(TraceOnPartDie);
            GameEvents.onFloatingOriginShift.Add(TraceOnFloatingOriginShift);
            //TimingManager.FixedUpdateAdd(TimingManager.TimingStage.BetterLateThanNever, TraceFixedUpdate);

            if (traceFrameOfReference == TraceFrame.World && ActiveVesselInSpace())
            {
                Part part = FlightGlobals.ActiveVessel.rootPart;

                var po = physicalObject.ConvertToPhysicalObject(part, traceTransform.gameObject);
                po.origDrag = 0;
                po.maxDistance = 100000;

                Rigidbody rb = po.rb;
                rb.mass = 1;
                rb.velocity = Vector3.zero;
                rb.useGravity = false;
                rb.drag = 0;
                rb.angularDrag = 0;
                rb.detectCollisions = false;
            }

            // Create trace folder.

            // Make a new folder with a timestamp.
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

            //tracesPath = Path.GetFullPath(Path.Combine(FilePath, "Traces"));
            //if (!Directory.Exists(tracesPath))
            //    Directory.CreateDirectory(tracesPath);

            //tracesPath = Path.Combine(tracesPath, timestamp);
            //if (!Directory.Exists(tracesPath))
            //    Directory.CreateDirectory(tracesPath);

            tracesPath = Path.GetFullPath(Path.Combine(FilePath, "Traces", timestamp));
            if (!Directory.Exists(tracesPath))
                Directory.CreateDirectory(tracesPath);
        }

        private void TraceOnFloatingOriginShift(Vector3d offset, Vector3d nonFrame)
        {
            /*if (FlightGlobals.ActiveVessel == null)
                return;

            centreVelocity -= FlightGlobals.ActiveVessel.rb_velocity;
            offset += centreVelocity * Time.fixedDeltaTime;

            switch (FlightGlobals.ActiveVessel.situation)
            {
                case Vessel.Situations.LANDED:
                case Vessel.Situations.FLYING:
                case Vessel.Situations.SPLASHED:
                case Vessel.Situations.PRELAUNCH:
                    traceTransform.position -= offset;
                    traceTransform.position -= nonFrame;
                    break;
                default:
                    if (offset.magnitude > Time.fixedDeltaTime * 2)
                    {
                        Debug.Log("[CaptureTools] Trace: Large floating origin shift.");
                    }

                    traceTransform.position = traceTransform.position - (Vector3)offset;
                    break;
            }*/

            if (!ActiveVesselInSpace())
            {
                traceTransform.position -= offset;
                traceTransform.position -= nonFrame;
            }
        }

        private void TraceOnPartDie(Part p)
        {
            TraceRecorder rec = p.gameObject.GetComponent<TraceRecorder>();

            if (rec == null)
                return;

            // This flag says to hide the part at the end of the trace.
            rec.hidePart = true;
        }

        private void StopPartCapture()
        {
            // Stop each recorder.
            foreach (TraceRecorder r in traceRecorders)
            {
                r.StopRecording();
            }

            // Unregister events.
            GameEvents.onPartDie.Remove(TraceOnPartDie);
            GameEvents.onFloatingOriginShift.Remove(TraceOnFloatingOriginShift);

            // Destroy the centre.
            Destroy(traceTransform.gameObject);

            // Stop the frame counter and finish.
            capturingTrace = false;
        }

        internal void TraceFixedUpdate()
        {
            if (capturingTrace)
            {
                frameCount++;

                if (traceFrameOfReference == TraceFrame.Vessel && FlightGlobals.ActiveVessel != null)
                    traceTransform.position = FlightGlobals.ActiveVessel.transform.position;
            }
        }

        private bool ActiveVesselInSpace() {
            if (FlightGlobals.ActiveVessel == null)
                return false;

            var groundSituations = new Vessel.Situations[] { Vessel.Situations.FLYING, Vessel.Situations.LANDED, Vessel.Situations.SPLASHED, Vessel.Situations.PRELAUNCH };
            return !groundSituations.Contains(FlightGlobals.ActiveVessel.situation);
        }
    }

    public class TraceRecorder : MonoBehaviour
    {
        // Global frame counter for file size estimation.
        public static int recordedFrames = 0;

        bool recording = false;
        private List<Vector3> positions = new List<Vector3>();
        private List<Quaternion> rotations = new List<Quaternion>();
        private Vector3 posLast = Vector3.zero;
        private Quaternion rotLast = Quaternion.identity;
        internal bool hidePart = false;
        private float recordingTime = 0f;
        private float recordinginterval = 1 / CaptureTools.traceFramerate;

        private Transform centre;

        //internal static int recorderCount = 0;
        //private int id;
        //private Vector3 posDelta = Vector3.zero;
        //private Vector3 posDeltaFixed = Vector3.zero;

        // todo: this is should be writing per frame to a binary buffer with a header added after.

        internal void Start()
        {
            recording = true;
            centre = CaptureTools.traceTransform;

            //id = recorderCount;
            //recorderCount++;
        }

        internal void FixedUpdate()
        {
            //if (CaptureTools.frameCount % CaptureTools.traceInterval != 0)
            //    return;

            if (recordingTime == 0f)
            {
                // First frame.

                recordingTime = Time.fixedTime;
                posLast = centre.InverseTransformPoint(transform.position);
                rotLast = Quaternion.Inverse(centre.rotation) * transform.rotation;
            }
            else
            {
                if (recordingTime + recordinginterval > Time.fixedTime)
                {
                    if (recordingTime + recordinginterval < Time.fixedTime + Time.fixedDeltaTime)
                    {
                        posLast = centre.InverseTransformPoint(transform.position);
                        rotLast = Quaternion.Inverse(centre.rotation) * transform.rotation;
                    }

                    return;
                }

                recordingTime += recordinginterval;
            }

            recordedFrames++;

            Vector3 pos = centre.InverseTransformPoint(transform.position);
            Quaternion rot = Quaternion.Inverse(centre.rotation) * transform.rotation;

            // Interpolate between last and current position.

            float t = (recordingTime - (Time.fixedTime - Time.fixedDeltaTime)) / Time.fixedDeltaTime;
            t = Mathf.Clamp01(t);

            Vector3 interpolatedPos = Vector3.Lerp(posLast, pos, t);
            Quaternion interpolatedRot = Quaternion.Slerp(rotLast, rot, t);

            //posDelta = interpolatedPos - positions.LastOrDefault();
            //posDeltaFixed = pos - posLast;

            positions.Add(interpolatedPos);
            rotations.Add(interpolatedRot);

            posLast = pos;
            rotLast = rot;
        }

        public void StopRecording()
        {
            if (!recording)
                return;

            recording = false;

            Part part = gameObject.GetComponent<Part>();
            if (part == null)
            {
                Destroy(this);
                return;
            }

            string path = Path.Combine(CaptureTools.tracesPath, part.persistentId.ToString() + ".txt");

            // todo: multithread with parallel for
            // Write position data to a text file
            Task.Run(() =>
            {
                using (StreamWriter writer = new StreamWriter(path, false))
                {
                    writer.WriteLine(part.partInfo.name);
                    writer.WriteLine(hidePart ? 1 : 0);
                    writer.WriteLine((int)CaptureTools.traceFramerate);

                    Vector3 pos;
                    Quaternion rot;

                    for (int i = 0; i < positions.Count; i++)
                    {
                        pos = positions[i];
                        rot = rotations[i];

                        //writer.WriteLine(positions[i].x + " " + positions[i].y + " " + positions[i].z + " "
                        //    + rot.x + " " + rot.y + " " + rot.z + " " + rot.w);

                        // usign combine
                        writer.WriteLine(string.Join(" ", pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w));
                    }

                    writer.Close();
                }
            });

            // Clean up.
            Destroy(this);
        }

        internal void OnApplicationQuit()
        {
            StopRecording();
        }

        internal void OnDestroy()
        {
            StopRecording();
        }
    }
}
