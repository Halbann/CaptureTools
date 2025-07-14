using Scatterer;
using UnityEngine;

namespace CaptureTools
{
    public class SunflareHook : MonoBehaviour
    {
        public Camera nearCamera;
        public Camera scaledSpaceCamera;

        public Scatterer.SunFlare flare;
        public float useDbufferOnCamera;

        public void OnPreRender()
        {
            if (flare == null)
            {
                Destroy(this);
                return;
            }

            var instance = Scatterer.Scatterer.Instance;

            // Swap the cameras temporarily to update the sunglare material properties.

            var originalNear = instance.nearCamera;
            var originalScaled = instance.scaledSpaceCamera;

            instance.nearCamera = nearCamera;
            instance.scaledSpaceCamera = scaledSpaceCamera;

            flare.updateProperties();
            flare.sunglareMaterial.SetFloat(ShaderProperties.renderOnCurrentCamera_PROPERTY, 1f);
            flare.sunglareMaterial.SetFloat(ShaderProperties.useDbufferOnCamera_PROPERTY, this.useDbufferOnCamera);

            instance.nearCamera = originalNear;
            instance.scaledSpaceCamera = originalScaled;
        }

        public void OnPostRender()
        {
            if (flare == null)
            {
                Destroy(this);
                return;
            }

            flare.ClearExtinction();
            flare.sunglareMaterial.SetFloat(ShaderProperties.renderOnCurrentCamera_PROPERTY, 0f);
            flare.sunglareMaterial.SetFloat(ShaderProperties.useDbufferOnCamera_PROPERTY, this.useDbufferOnCamera);
        }
    }
}
