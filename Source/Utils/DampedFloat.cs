using UnityEngine;

namespace CaptureTools.Utils
{
    public struct DampedFloat
    {
        public float current;
        public float target;
        public float derivative;
        public float smoothTime;
        public float maxSpeed;

        public DampedFloat(float initialValue, float smoothTime, float maxSpeed)
        {
            current = initialValue;
            target = initialValue;
            derivative = 0f;
            this.smoothTime = smoothTime;
            this.maxSpeed = maxSpeed;
        }

        public DampedFloat(float initialValue, float smoothTime)
            : this(initialValue, smoothTime, Mathf.Infinity) { }

        public float Update(float dt, float smoothTime = -1) =>
            Update(current, target, dt);

        public float UpdateFrom(float current, float dt, float smoothTime = -1) =>
            Update(current, target, smoothTime, dt);

        public float UpdateTo(float target, float dt, float smoothTime = -1) =>
            Update(current, target, smoothTime, dt);

        public float Update(float current, float target, float dt, float smoothTime = -1)
        {
            if (smoothTime != -1)
                this.smoothTime = smoothTime;

            this.target = target;

            if (this.smoothTime <= 0)
            {
                derivative = 0;
                return this.current = target;
            }

            return this.current = Mathf.SmoothDamp(this.current, target, ref derivative, this.smoothTime, maxSpeed, dt);
        }
    }
}
