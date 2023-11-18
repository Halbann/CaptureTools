using System;
using System.Reflection;

using UnityEngine;

namespace CaptureTools.Integration
{
    internal static class Scatterer
    {
        internal static bool hasChecked = false;
        internal static bool loaded = false;

        private static Type smaaType;
        private static Type taaType;

        internal static Type sunlightModulatorType;

        internal static void Check()
        {
            if (hasChecked)
                return;

            hasChecked = true;

            try
            {
                foreach (var assy in AssemblyLoader.loadedAssemblies)
                {
                    if (assy.dllName.Contains("Scatterer"))
                    {
                        loaded = true;

                        smaaType = assy.assembly.GetType("Scatterer.SubpixelMorphologicalAntialiasing");
                        taaType = assy.assembly.GetType("Scatterer.TemporalAntiAliasing");

                        sunlightModulatorType = assy.assembly.GetType("Scatterer.SunlightModulator");

                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CaptureTools]: Failed to setup Scatterer integration: {e.Message}");
            }
        }

        public static void SetupCameras(Camera localSpace, Camera scaledSpace, bool doubleAA = false)
        {
            if (UsingTAA())
            {
                AddTAA(localSpace, FlightCamera.fetch.mainCamera);
                AddTAA(scaledSpace, ScaledCamera.Instance.cam);

                if (doubleAA)
                    AddSMAA(localSpace);
            }
            else
            {
                AddSMAA(localSpace);
            }

            var sunlightModulator = ScaledCamera.Instance.gameObject.GetComponent(sunlightModulatorType);
            var modulator = localSpace.gameObject.AddComponent<ModulateSunColour>();
            modulator.sunlightModulator = sunlightModulator;
        }

        public static bool UsingTAA()
        {
            return null != FlightCamera.fetch.mainCamera.gameObject.GetComponent(taaType);
        }

        public static void AddTAA(Camera camera, Camera template)
        {
            var templateAA = template.gameObject.GetComponent(taaType);

            if (templateAA == null)
            {
                Debug.LogError("[CaptureTools]: Failed to copy anti-aliasing from Scatterer near camera. Couldn't find existing TAA.");
                return;
            }

            // Add Scatterer's TAA component to a camera.

            var taa = camera.gameObject.AddComponent(taaType);
            CopyField("hdrEnabled", taa, templateAA);
            CopyField("jitterTransparencies", taa, templateAA);
            CopyField("checkOceanDepth", taa, templateAA);
        }

        private static void CopyField(string name, object instance, object template)
        {
            if (instance == null || template == null)
            {
                Debug.LogError($"[CaptureTools]: Failed to copy field {name}. Objects were null.");
                return;
            }
            
            var field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                Debug.LogError($"[CaptureTools]: Failed to copy field {name}. Couldn't find field.");
                return;
            }

            field.SetValue(instance, field.GetValue(template));
        }

        public static void AddSMAA(Camera camera)
        {
            // Add Scatterer's SMAA component to a camera.

            camera.gameObject.AddComponent(smaaType);
        }
    }

    public class ModulateSunColour : MonoBehaviour
    {
        public Component sunlightModulator;

        private FieldInfo moduleColorField;
        private bool init = false;

        private Color originalColour = Color.white;

        internal void OnPreCull()
        {
            if (!init)
            {
                moduleColorField = Scatterer.sunlightModulatorType.GetField("lastModulateColor", BindingFlags.Instance | BindingFlags.Public);
                init = true;
            }

            var sunlight = Sun.Instance.sunLight;
            originalColour = sunlight.color;
            sunlight.color = (Color)moduleColorField.GetValue(sunlightModulator);
        }

        internal void OnPostRender()
        {
            var sunlight = Sun.Instance.sunLight;
            sunlight.color = originalColour;
        }
    }
}
