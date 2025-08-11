using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CaptureTools.Trace
{
    public class Tracer : MonoBehaviour
    {
        public static ReferenceFrame frameOfReference = ReferenceFrame.World;
        public static float framerate = 24;
        public static readonly string[] excludedParts = new string[] { "strutConnector", "fuelLine", "parachuteRadial" };

        private readonly List<PartRecorder> recorders = new List<PartRecorder>();
        private bool recording = false;

        public float StartTime { private set; get; }
        public static string CurrentPath { get; private set; }
        public static Transform Centre { get; private set; }

        public enum ReferenceFrame
        {
            World,
            Vessel
        }

        protected void Awake()
        {
            enabled = false;
        }

        protected void OnEnable()
        {
            PartRecorder.recordedFrames = 0;
            StartTime = Time.time;
            recorders.Clear();
            recording = true;

            // Centre. Trace is recorded relative to this.
            Centre = new GameObject().transform;
            Centre.gameObject.name = "Trace Centre";
            Centre.position = Vector3.zero;
            Centre.rotation = FlightCamera.fetch.getReferenceFrame();

            // Centre marker.
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Material sphereMat = new Material(Shader.Find("Unlit/Color"));
            sphereMat.color = Color.magenta;
            capsule.GetComponent<MeshRenderer>().material = sphereMat;
            capsule.transform.localScale *= 6;
            Destroy(capsule.GetComponent<CapsuleCollider>());
            capsule.transform.SetParent(Centre, false);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;

            // Add a recorder to every part.
            var parts = FlightGlobals.VesselsLoaded.SelectMany(v => v.Parts)
                .Where(p => !excludedParts.Contains(p.name));

            foreach (Part p in parts)
                recorders.Add(PartRecorder.Create(p, Centre));

            // Register events.
            GameEvents.onPartDie.Add(OnPartDie);
            GameEvents.onFloatingOriginShift.Add(OnFloatingOriginShift);

            if (frameOfReference == ReferenceFrame.World && ActiveVesselInSpace())
            {
                Part part = FlightGlobals.ActiveVessel.rootPart;

                physicalObject po = physicalObject.ConvertToPhysicalObject(part, Centre.gameObject);
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

            // Make a new folder with a timestamp.
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            CurrentPath = Path.GetFullPath(Path.Combine(CaptureTools.FilePath, "Traces", timestamp));

            if (!Directory.Exists(CurrentPath))
                Directory.CreateDirectory(CurrentPath);
        }

        protected void FixedUpdate()
        {
            if (frameOfReference == ReferenceFrame.Vessel && FlightGlobals.ActiveVessel != null)
                Centre.position = FlightGlobals.ActiveVessel.transform.position;
        }

        protected void OnDisable()
        {
            if (!recording)
                return;

            // Stop each recorder.
            foreach (PartRecorder r in recorders)
                Destroy(r);

            recorders.Clear();
            recording = false;

            // Unregister events.
            GameEvents.onPartDie.Remove(OnPartDie);
            GameEvents.onFloatingOriginShift.Remove(OnFloatingOriginShift);

            // Destroy the centre.
            if (Centre != null)
                Destroy(Centre.gameObject);
        }

        private void OnFloatingOriginShift(Vector3d offset, Vector3d nonFrame)
        {
            if (!ActiveVesselInSpace())
            {
                Centre.position -= offset;
                Centre.position -= nonFrame;
            }
        }

        private void OnPartDie(Part p)
        {
            PartRecorder rec = p.gameObject.GetComponent<PartRecorder>();

            if (rec == null)
                return;

            // This flag says to hide the part at the end of the trace.
            rec.hidePartAtEnd = true;
        }

        private bool ActiveVesselInSpace()
        {
            if (FlightGlobals.ActiveVessel == null)
                return false;

            Vessel.Situations[] groundSituations = new Vessel.Situations[] { Vessel.Situations.FLYING, Vessel.Situations.LANDED, Vessel.Situations.SPLASHED, Vessel.Situations.PRELAUNCH };
            return !groundSituations.Contains(FlightGlobals.ActiveVessel.situation);
        }
    }
}