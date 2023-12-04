using System;
using System.Reflection;
using System.Linq;

using UnityEngine;

using Scatterer;
using ScattererFlare = Scatterer.SunFlare;

namespace CaptureTools.Integration
{
    internal static class CTScatterer
    {
        internal static bool hasChecked = false;
        internal static bool loaded = false;

        private static Type smaaType;
        private static Type taaType;

        internal static Type sunlightModulatorType;

        public static string scattererNamespace = "Scatterer.";
        public static float scattererVersion;

        internal static void Check()
        {
            if (hasChecked)
                return;

            hasChecked = true;

            try
            {
                foreach (var assy in AssemblyLoader.loadedAssemblies)
                {
                    if (assy.dllName.Equals("Scatterer"))
                    {
                        loaded = true;

                        string version = assy.assembly.FullName;
                        version = version.Split(',')[1].Split('=')[1].Split('.')[1].Trim();
                        scattererVersion = float.Parse(version);

                        if (scattererVersion <= 838f)
                            scattererNamespace = "scatterer.";

                        smaaType = assy.assembly.GetType(scattererNamespace + "SubpixelMorphologicalAntialiasing");
                        taaType = assy.assembly.GetType(scattererNamespace + "TemporalAntiAliasing");

                        sunlightModulatorType = assy.assembly.GetType(scattererNamespace + "SunlightModulator");

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

            if (scattererVersion > 838)
                SetupSunflares(localSpace, scaledSpace);
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

        public static void SetupSunflares(Camera localSpace, Camera scaledSpace)
        {
            // Sunflares.

            var instance = Scatterer.Scatterer.Instance;

            var oldNear = instance.nearCamera;
            var oldScaled = instance.scaledSpaceCamera;
            instance.nearCamera = localSpace;
            instance.scaledSpaceCamera = scaledSpace;

            FieldInfo nearCameraHook = typeof(ScattererFlare).GetField("nearCameraHook", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo scaledCameraHook = typeof(ScattererFlare).GetField("scaledCameraHook", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (ConfigNode configNode in instance.planetsConfigsReader.sunflareConfigs)
            {
                ConfigNode[] array2 = configNode.GetNodes();
                for (int j = 0; j < array2.Length; j++)
                {
                    ConfigNode _cn = array2[j];
                    ScattererFlare sunFlare = (ScattererFlare)instance.scaledSpaceCamera.gameObject.AddComponent(typeof(ScattererFlare));
                    try
                    {
                        sunFlare.Configure(FlightGlobals.Bodies.SingleOrDefault((CelestialBody _cb) => _cb.GetName() == _cn.name), _cn.name, Utils.GetScaledTransform(_cn.name), _cn);
                        sunFlare.start();

                        // Near camera hook.
                        var customNearHook = localSpace.gameObject.AddComponent<CaptureToolsSunflareHook>();
                        customNearHook.flare = sunFlare;
                        customNearHook.nearCamera = localSpace;
                        customNearHook.scaledSpaceCamera = scaledSpace;
                        customNearHook.useDbufferOnCamera = 1;

                        // Scaled camera hook.
                        var customScaledHook = scaledSpace.gameObject.AddComponent<CaptureToolsSunflareHook>();
                        customScaledHook.flare = sunFlare;
                        customScaledHook.nearCamera = localSpace;
                        customScaledHook.scaledSpaceCamera = scaledSpace;
                        customScaledHook.useDbufferOnCamera = 0;

                        var helper = scaledSpace.gameObject.AddComponent<CaptureToolsSunFlareHelper>();
                        helper.flare = sunFlare;
                        helper.customNear = customNearHook;
                        helper.customScaled = customScaledHook;
                    }
                    catch (Exception ex)
                    {
                        Utils.LogDebug("Custom sunflare cannot be added to " + _cn.name + " " + ex.ToString());
                        UnityEngine.Object.Destroy(sunFlare);
                        UnityEngine.Object.Destroy(sunFlare);
                    }
                }
                array2 = null;
            }

            instance.nearCamera = oldNear;
            instance.scaledSpaceCamera = oldScaled;
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
                moduleColorField = CTScatterer.sunlightModulatorType.GetField("lastModulateColor", BindingFlags.Instance | BindingFlags.Public);
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

    public class CaptureToolsSunflareHook : MonoBehaviour
    {
        public Camera nearCamera;
        public Camera scaledSpaceCamera;

        public ScattererFlare flare;
        public float useDbufferOnCamera;

        public void OnPreRender()
        {
            if (flare == null)
            {
                Destroy(this);
                return;
            }
            
            var instance = Scatterer.Scatterer.Instance;

            var originalNear = instance.nearCamera;
            var originalScaled = instance.scaledSpaceCamera;

            instance.nearCamera = nearCamera;
            instance.scaledSpaceCamera = scaledSpaceCamera;

            this.flare.updateProperties();
            this.flare.sunglareMaterial.SetFloat(ShaderProperties.renderOnCurrentCamera_PROPERTY, 1f);
            this.flare.sunglareMaterial.SetFloat(ShaderProperties.useDbufferOnCamera_PROPERTY, this.useDbufferOnCamera);

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

            this.flare.ClearExtinction();
            this.flare.sunglareMaterial.SetFloat(ShaderProperties.renderOnCurrentCamera_PROPERTY, 0f);
            this.flare.sunglareMaterial.SetFloat(ShaderProperties.useDbufferOnCamera_PROPERTY, this.useDbufferOnCamera);
        }
    }

    public class CaptureToolsSunFlareHelper : MonoBehaviour
    {
        public ScattererFlare flare;
        public CaptureToolsSunflareHook customNear;
        public CaptureToolsSunflareHook customScaled;
        public SunflareCameraHook near;
        public SunflareCameraHook scaled;

        internal void Start()
        {
            FieldInfo nearCameraHook = typeof(ScattererFlare).GetField("nearCameraHook", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo scaledCameraHook = typeof(ScattererFlare).GetField("scaledCameraHook", BindingFlags.Instance | BindingFlags.NonPublic);

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
    }
}