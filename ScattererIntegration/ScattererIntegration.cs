using System;
using System.Linq;
using System.Reflection;

using UnityEngine;

using Scatterer;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class ScattererIntegration : MonoBehaviour
    {
        protected void Awake()
        {
            CaptureTools.OnSetupCameras += SetupCameras;
        }

        public static void SetupCameras(Camera localSpace, Camera scaledSpace, bool doubleAA = false)
        {
            Debug.Log($"[CaptureTools]: Setting up Scatterer integration on {localSpace.name} and {scaledSpace.name}.");

            if (Scatterer.Scatterer.Instance.mainSettings.useSubpixelMorphologicalAntialiasing)
            {
                AddSMAA(localSpace);
                // SMAA is never added to the scaled space camera.
            }

            if (Scatterer.Scatterer.Instance.mainSettings.useTemporalAntiAliasing)
            {
                AddTAA(localSpace, FlightCamera.fetch.mainCamera);
                AddTAA(scaledSpace, ScaledCamera.Instance.cam);
            }

            var sunlightModulator = ScaledCamera.Instance.gameObject.GetComponent<SunlightModulator>();
            var modulator = localSpace.gameObject.AddComponent<ModulateSunColour>();
            modulator.sunlightModulator = sunlightModulator;

            SetupSunflares(localSpace, scaledSpace);

            scaledSpace.gameObject.AddComponent<EnsureEffects>();

            // Can't use this because DeferredRaymarchedVolumetricCloudsRenderer is marked internal.

            //Atmosphere.DeferredRaymarchedVolumetricCloudsRenderer.EnableForThisFrame(localSpace, null);
            //localSpace.GetComponent<Atmosphere.DeferredRaymarchedVolumetricCloudsRenderer>()?.Initialize();

            // Do the same using reflection
            var originalCloudRenderer = FlightCamera.fetch.mainCamera.GetComponent("Atmosphere.DeferredRaymarchedVolumetricCloudsRenderer");
            if (originalCloudRenderer != null)
            {
                try
                {
                    var cloudRendererType = originalCloudRenderer.GetType();
                    cloudRendererType.GetMethod("EnableForThisFrame", BindingFlags.Public | BindingFlags.Static)
                        .Invoke(originalCloudRenderer, new object[] { localSpace, null });

                    var initializeMethod = cloudRendererType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
                    var newCloudRenderer = localSpace.GetComponent("Atmosphere.DeferredRaymarchedVolumetricCloudsRenderer");

                    initializeMethod?.Invoke(newCloudRenderer, null);

                    Debug.Log($"[CaptureTools]: Successful early initialisation of DeferredRaymarchedVolumetricCloudsRenderer.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CaptureTools]: Failed early initialisation of DeferredRaymarchedVolumetricCloudsRenderer: {ex.Message}");
                }
            }
        }

        private static void AddTAA(Camera camera, Camera template)
        {
            var templateAA = template.GetComponent<TemporalAntiAliasing>();

            if (templateAA == null)
            {
                Debug.LogError("[CaptureTools]: Failed to copy temporal anti-aliasing from the main camera. Couldn't find existing TAA.");
                return;
            }

            // Add Scatterer's TAA component to a camera.

            var taa = camera.gameObject.AddComponent<TemporalAntiAliasing>();

            CopyField("hdrEnabled", taa, templateAA);
            taa.jitterTransparencies = templateAA.jitterTransparencies;
            taa.checkOceanDepth = templateAA.checkOceanDepth;
            taa.resetMotionVectors = templateAA.resetMotionVectors;
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

        private static void AddSMAA(Camera camera)
        {
            // Add Scatterer's SMAA component to a camera.

            camera.gameObject.AddComponent<SubpixelMorphologicalAntialiasing>();
        }

        private static void SetupSunflares(Camera localSpace, Camera scaledSpace)
        {
            // Sunflares.

            var instance = Scatterer.Scatterer.Instance;

            var oldNear = instance.nearCamera;
            var oldScaled = instance.scaledSpaceCamera;
            instance.nearCamera = localSpace;
            instance.scaledSpaceCamera = scaledSpace;

            foreach (ConfigNode configNode in instance.planetsConfigsReader.sunflareConfigs)
            {
                ConfigNode[] array2 = configNode.GetNodes();
                for (int j = 0; j < array2.Length; j++)
                {
                    ConfigNode _cn = array2[j];
                    Scatterer.SunFlare sunFlare = instance.scaledSpaceCamera.gameObject.AddComponent<Scatterer.SunFlare>();
                    try
                    {
                        sunFlare.Configure(FlightGlobals.Bodies.SingleOrDefault((CelestialBody _cb) => _cb.GetName() == _cn.name), _cn.name, Utils.GetScaledTransform(_cn.name), _cn);
                        sunFlare.start();

                        // Near camera hook.
                        var customNearHook = localSpace.gameObject.AddComponent<SunflareHook>();
                        customNearHook.flare = sunFlare;
                        customNearHook.nearCamera = localSpace;
                        customNearHook.scaledSpaceCamera = scaledSpace;
                        customNearHook.useDbufferOnCamera = 1;

                        // Scaled camera hook.
                        var customScaledHook = scaledSpace.gameObject.AddComponent<SunflareHook>();
                        customScaledHook.flare = sunFlare;
                        customScaledHook.nearCamera = localSpace;
                        customScaledHook.scaledSpaceCamera = scaledSpace;
                        customScaledHook.useDbufferOnCamera = 0;

                        string sunflareKey = _cn.name + "CaptureTools";
                        Scatterer.Scatterer.Instance.sunflareManager.scattererSunFlares.Add(sunflareKey, sunFlare);

                        var helper = scaledSpace.gameObject.AddComponent<SunFlareHelper>();
                        helper.sunflareKey = sunflareKey;
                        helper.flare = sunFlare;
                        helper.customNear = customNearHook;
                        helper.customScaled = customScaledHook;
                    }
                    catch (Exception ex)
                    {
                        Destroy(sunFlare);
                        Destroy(sunFlare);
                    }
                }
                array2 = null;
            }

            instance.nearCamera = oldNear;
            instance.scaledSpaceCamera = oldScaled;
        }

        protected void OnDestroy()
        {
            CaptureTools.OnSetupCameras -= SetupCameras;
        }
    }
}
