using Scatterer;
using UnityEngine;

namespace CaptureTools
{
    class ModulateSunColour : MonoBehaviour
    {      
        public SunlightModulator sunlightModulator;
        private Color originalColour = Color.white;

        internal void OnPreCull()
        {
            var sunlight = Sun.Instance.sunLight;
            originalColour = sunlight.color;
            sunlight.color = sunlightModulator.lastModulateColor;
        }

        internal void OnPostRender()
        {
            var sunlight = Sun.Instance.sunLight;
            sunlight.color = originalColour;
        }
    }
}
