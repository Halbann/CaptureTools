using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CaptureTools.Trace
{
    public class PartRecorder : MonoBehaviour
    {
        // Global frame counter for file size estimation.
        public static int recordedFrames;

        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Quaternion> rotations = new List<Quaternion>();
        private Vector3 posLast = Vector3.zero;
        private Quaternion rotLast = Quaternion.identity;
        private float recordingTime;
        private readonly float recordinginterval = 1 / Tracer.framerate;
        private Transform centre;

        public bool hidePartAtEnd;

        // todo: this is should be writing per frame to a binary buffer with a header added after.
        // custom or alembic, fbx, usd, whatever

        public static PartRecorder Create(Part part, Transform centre)
        {
            var recorder = part.gameObject.AddComponent<PartRecorder>();
            recorder.centre = centre;

            return recorder;
        }

        protected void FixedUpdate()
        {
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

            positions.Add(interpolatedPos);
            rotations.Add(interpolatedRot);

            posLast = pos;
            rotLast = rot;
        }

        protected void OnDisable()
        {
            Part part = gameObject.GetComponent<Part>();
            if (part == null)
            {
                Destroy(this);
                return;
            }

            string path = Path.Combine(Tracer.CurrentPath, part.persistentId.ToString() + ".txt");

            // todo: multithread with parallel for
            // Write position data to a text file
            Task.Run(() =>
            {
                using (StreamWriter writer = new StreamWriter(path, false))
                {
                    writer.WriteLine(part.partInfo.name);
                    writer.WriteLine(hidePartAtEnd ? 1 : 0);
                    writer.WriteLine((int)Tracer.framerate);

                    Vector3 pos;
                    Quaternion rot;

                    for (int i = 0; i < positions.Count; i++)
                    {
                        pos = positions[i];
                        rot = rotations[i];
                        writer.WriteLine(string.Join(" ", pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w));
                    }
                }
            });
        }
    }
}
