using UnityEngine;

namespace CaptureTools
{
    internal class EnsureEffects : MonoBehaviour
    {
        protected void OnPreCull()
        {
            EnableAtmosphere();
        }

        private void EnableAtmosphere()
        {
            // Effects can be disabled after rendering the TextureReplacer cubemap.

            var bodies = Scatterer.Scatterer.Instance.planetsConfigsReader.scattererCelestialBodies;
            var active = bodies.Find(m => !ReferenceEquals(null, m.prolandManager));

            active.prolandManager.GetSkyNode().localScatteringContainer.UpdateContainer();
        }
    }
}
    