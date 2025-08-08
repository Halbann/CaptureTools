using UnityEngine;

namespace CaptureTools.Utils
{
    public struct DampedQuaternion
    {
        public Quaternion current;
        public Quaternion target;
        public Quaternion derivative;
        public float smoothTime;
        public float maxSpeed;

        private static Quaternion zero = new Quaternion(0, 0, 0, 0);

        public DampedQuaternion(Quaternion initialValue, float smoothTime, float maxSpeed)
        {
            current = initialValue;
            target = initialValue;
            derivative = new Quaternion(0, 0, 0, 0);
            this.smoothTime = smoothTime;
            this.maxSpeed = maxSpeed;
        }

        public DampedQuaternion(Quaternion initialValue, float smoothTime)
            : this(initialValue, smoothTime, Mathf.Infinity) { }

        public Quaternion Update(float dt, float smoothTime = -1) =>
            Update(current, target, dt, smoothTime);

        public Quaternion UpdateFrom(Quaternion current, float dt, float smoothTime = -1) =>
            Update(current, target, dt, smoothTime);

        public Quaternion UpdateTo(Quaternion target, float dt, float smoothTime = -1) =>
            Update(current, target, dt, smoothTime);

        public Quaternion Update(Quaternion current, Quaternion target, float dt, float smoothTime = -1)
        {
            if (smoothTime != -1)
                this.smoothTime = smoothTime;

            this.target = target;

            if (this.smoothTime <= 0)
            {
                derivative = zero;
                return this.current = target;
            }

            return this.current = current.SmoothDamp(target, ref derivative, this.smoothTime, maxSpeed, dt);
        }
    }

    public static class QuaternionExtension
    {
        public static Quaternion SmoothDamp(this in Quaternion rot, Quaternion target, ref Quaternion deriv, float time, float maxSpeed, float deltaTime)
        {
            if (deltaTime < Mathf.Epsilon)
                return rot;

            if (time < Mathf.Epsilon)
                return target;

            // account for double-cover
            var Dot = Quaternion.Dot(rot, target);
            var Multi = Dot > 0f ? 1f : -1f;
            target.x *= Multi;
            target.y *= Multi;
            target.z *= Multi;
            target.w *= Multi;
            // smooth damp (nlerp approx)
            var Result = new Vector4(
                Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time, maxSpeed, deltaTime),
                Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time, maxSpeed, deltaTime),
                Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time, maxSpeed, deltaTime),
                Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time, maxSpeed, deltaTime)
            ).normalized;

            // ensure deriv is tangent
            var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
            deriv.x -= derivError.x;
            deriv.y -= derivError.y;
            deriv.z -= derivError.z;
            deriv.w -= derivError.w;

            return new Quaternion(Result.x, Result.y, Result.z, Result.w);
        }
    }
}
