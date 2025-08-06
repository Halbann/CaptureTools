using UnityEngine;

namespace CaptureTools.Utils
{
    public struct DampedVector
    {
        public Vector3 value;
        public Vector3 target;
        public Vector3 derivative;
        public float smoothTime;

        public DampedVector(Vector3 initialValue, Vector3 targetValue, float smoothTime)
        {
            value = initialValue;
            target = targetValue;
            derivative = Vector3.zero;
            this.smoothTime = smoothTime;
        }

        public Vector3 Update(float newSmoothTime = -1)
        {
            if (newSmoothTime != -1)
                smoothTime = newSmoothTime;

            return value = Vector3.SmoothDamp(value, target, ref derivative, smoothTime);
        }
    }
}
