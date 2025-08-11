using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CaptureTools.Utils;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class Addons : MonoBehaviour
    {
        private static readonly HashSet<string> loadedAddons = new HashSet<string>();

        protected void Awake()
        {
            // Load integrations automatically at startup.
            LoadIntegrations();
        }

        public static Dictionary<string, string> BuildIntegrationList()
        {
            // todo: specify version brackets

            Dictionary<string, string> addons = new Dictionary<string, string>
            {
                { "ScattererIntegration.addon", "scatterer" },
                { "DeferredIntegration.addon", "deferred" }
            };

            return addons;
        }

        public void LoadIntegrations()
        {
            Dictionary<string, string> integrations = BuildIntegrationList();

            List<string> addonsToLoad = new List<string>();

            // Find out which addons to load based on loaded assemblies.
            foreach (AssemblyLoader.LoadedAssembly assembly in AssemblyLoader.loadedAssemblies)
            {
                if (integrations.Count < 1)
                    break;

                foreach (KeyValuePair<string, string> dependency in integrations.Reverse())
                {
                    if (!assembly.dllName.ToLower().Equals(dependency.Value))
                        continue;

                    addonsToLoad.Add(dependency.Key);
                    integrations.Remove(dependency.Key);
                }
            }

            // Load those addons.
            foreach (string key in addonsToLoad)
                LoadAddon(key);
        }

        public static void LoadAll()
        {
            string addonPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "CaptureTools", "Addons");

            // Get everything ending with .addon

            if (!Directory.Exists(addonPath))
            {
                CTDebug.LogError("Failed to load addons: Addons directory not found.");
                return;
            }

            foreach (string file in Directory.GetFiles(addonPath, "*.addon"))
            {
                if (loadedAddons.Contains(Path.GetFileNameWithoutExtension(file)))
                    continue;

                LoadAddonFromPath(file);
            }
        }

        public static void LoadAddon(string name)
        {
            string addonPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "CaptureTools", "Addons", name);
            LoadAddonFromPath(addonPath);
        }

        public static void LoadAddonFromPath(string addonPath)
        {
            try
            {
                string filename = Path.GetFileName(addonPath);
                string addonName = Path.GetFileNameWithoutExtension(addonPath);

                if (AssemblyLoader.loadedAssemblies.Any(a => a.dllName.Equals(addonName, System.StringComparison.OrdinalIgnoreCase)))
                    throw new System.Exception($"{filename} already loaded.");

                if (!File.Exists(addonPath))
                    throw new System.Exception($"{filename} not found.");

                CTDebug.Log($"Loading addon {filename}.");

                AssemblyLoader.LoadPlugin(new FileInfo(addonPath), addonPath, null);
                AssemblyLoader.LoadedAssembly addon = AssemblyLoader.loadedAssemblies.FirstOrDefault(p => p.name == addonName);

                if (addon == null)
                    throw new System.Exception($"{filename} failed to load.");
                else
                    CTDebug.Log($"Successfully loaded addon {filename}.");

                loadedAddons.Add(addonName);
                addon.Load();
            }
            catch (System.Exception e)
            {
                CTDebug.LogError($"Failed to load addon: {e.Message}");
            }
        }
    }
}
