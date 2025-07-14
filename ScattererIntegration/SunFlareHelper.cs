using Scatterer;
using System.Reflection;
using UnityEngine;

namespace CaptureTools
{
    public class SunFlareHelper : MonoBehaviour
    {
        public Scatterer.SunFlare flare;
        public SunflareHook customNear;
        public SunflareHook customScaled;
        public SunflareCameraHook near;
        public SunflareCameraHook scaled;
        public string sunflareKey;

        internal void Start()
        {
            FieldInfo nearCameraHook = typeof(Scatterer.SunFlare).GetField("nearCameraHook", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo scaledCameraHook = typeof(Scatterer.SunFlare).GetField("scaledCameraHook", BindingFlags.Instance | BindingFlags.NonPublic);

            near = (SunflareCameraHook)nearCameraHook.GetValue(flare);
            scaled = (SunflareCameraHook)scaledCameraHook.GetValue(flare);
        }

        internal void LateUpdate()
        {
            if (flare == null)
            {
                Destroy(this);
                return;
            }

            customNear.enabled = near.enabled;
            customScaled.enabled = scaled.enabled;

            near.enabled = false;
            scaled.enabled = false;
        }

        protected void OnDestroy()
        {
            // Remove sunflare from scattererSunflares.

            Scatterer.Scatterer.Instance.sunflareManager.scattererSunFlares.Remove(sunflareKey);
        }
    }

}