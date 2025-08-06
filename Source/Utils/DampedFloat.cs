using UnityEngine;

namespace CaptureTools.Utils
{
    public struct DampedFloat
    {
        public float value;
        public float target;
        public float speed;
        public float smoothTime;

        public DampedFloat(float initialValue, float targetValue, float smoothTime)
        {
            value = initialValue;
            target = targetValue;
            speed = 0f;
            this.smoothTime = smoothTime;
        }

        public float Update(float newSmoothTime = -1)
        {
            if (newSmoothTime != -1)
                smoothTime = newSmoothTime;

            return value = Mathf.SmoothDamp(value, target, ref speed, smoothTime);
        }
    }
}
