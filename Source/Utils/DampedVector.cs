using UnityEngine;

namespace CaptureTools.Utils
{
    public struct DampedVector
    {
        public Vector3 current;
        public Vector3 target;
        public Vector3 derivative;
        public float smoothTime;
        public float maxSpeed;

        public DampedVector(Vector3 initialValue, float smoothTime, float maxSpeed)
        {
            current = initialValue;
            target = initialValue;
            derivative = Vector3.zero;
            this.smoothTime = smoothTime;
            this.maxSpeed = maxSpeed;
        }

        public DampedVector(Vector3 initialValue, float smoothTime)
            : this(initialValue, smoothTime, Mathf.Infinity) { }

        public Vector3 Update(float dt, float smoothTime = -1) =>
            Update(current, target, dt, smoothTime);

        public Vector3 UpdateFrom(Vector3 current, float dt, float smoothTime = -1) =>
            Update(current, target, dt, smoothTime);

        public Vector3 UpdateTo(Vector3 target, float dt, float smoothTime = -1) =>
            Update(current, target, dt, smoothTime);

        public Vector3 Update(Vector3 current, Vector3 target, float dt, float smoothTime = -1)
        {
            if (smoothTime != -1)
                this.smoothTime = smoothTime;

            this.target = target;

            if (this.smoothTime <= 0)
            {
                derivative = Vector3.zero;
                return this.current = target;
            }

            if (current != this.current)
                derivative += (current - this.current) / dt;

            return this.current = Vector3.SmoothDamp(this.current, target, ref derivative, this.smoothTime, maxSpeed, dt);
        }
    }
}
