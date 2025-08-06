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

        public DampedQuaternion(Quaternion initial, float smoothTime, float maxSpeed)
        {
            current = initial;
            target = initial;
            derivative = Quaternion.identity;
            this.smoothTime = smoothTime;
            this.maxSpeed = maxSpeed;
        }

        public Quaternion Update(Quaternion target, float dt)
        {
            return current = current.SmoothDamp(target, ref derivative, smoothTime, maxSpeed <= 0 ? float.PositiveInfinity : maxSpeed, dt);
        }
    }

    public static class QuaternionExtension
    {
        public static Quaternion SmoothDamp(this in Quaternion rot, Quaternion target, ref Quaternion deriv, float time, float maxSpeed, float deltaTime)
        {
            if (deltaTime < Mathf.Epsilon) return rot;

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
