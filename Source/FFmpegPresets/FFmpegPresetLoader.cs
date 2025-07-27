using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class FFmpegPresetLoader : MonoBehaviour
    {
        public static Dictionary<string, FFmpegPreset> presets = new Dictionary<string, FFmpegPreset>();
        public static event System.Action OnPresetChanged;
        public static string pluginDataPath;

        private static string currentPresetName;
        public static FFmpegPreset CurrentPreset
        {
            get
            {
                if (presets.TryGetValue(currentPresetName, out FFmpegPreset preset))
                    return preset;

                return null;
            }
        }

        protected void Awake()
        {
            pluginDataPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "CaptureTools", "PluginData");
            ReloadPresets();
        }

        private void ReloadPresets()
        {
            presets.Clear();
            LoadPresetsFromDatabase();
            LoadPresetsFromPluginData();
            FindDefaultPreset();

            OnPresetChanged?.Invoke();
        }

        public static bool AddPreset(FFmpegPreset preset, bool silent = false)
        {
            if (presets.ContainsKey(preset.Name))
            {
                Debug.LogError("[CaptureTools]: Already loaded a preset with the name: " + preset.Name + ". Please check your configurations and remove any duplicates. Only the first configuration parsed for any one name will be loaded.");
                return false;
            }

            presets.Add(preset.Name, preset);
            if (!silent)
                OnPresetChanged?.Invoke();

            return true;
        }

        public static void RemovePreset(FFmpegPreset preset, bool silent = false)
        {
            if (!presets.ContainsKey(preset.Name))
                return;

            presets.Remove(preset.Name);
            if (!silent)
                OnPresetChanged?.Invoke();
        }

        private static void LoadPresetsFromDatabase()
        {
            foreach (UrlDir.UrlConfig profileConfig in GameDatabase.Instance.root.GetConfigs(FFmpegPreset.nodeName))
            {
                FFmpegPreset preset = new FFmpegPreset();

                if (FFmpegPreset.TryLoadPresetFromURLConfig(preset, profileConfig))
                    AddPreset(preset, true);
            }
        }

        private static void LoadPresetsFromPluginData()
        {
            // Find all .cfg files in the PluginData directory.
            string[] presetFiles = Directory.GetFiles(pluginDataPath, "*.cfg", SearchOption.AllDirectories);
            List<ConfigNode> fileNodes = new List<ConfigNode>();

            foreach (string file in presetFiles)
            {
                FFmpegPreset preset = new FFmpegPreset();

                if (FFmpegPreset.TryLoadPresetFromFile(preset, file))
                    if (AddPreset(preset, true))
                    {
                        preset.path = file;
                        preset.Editable = true;
                    }
            }
        }

        private void FindDefaultPreset()
        {
            if (presets.ContainsKey("x264"))
                SetCurrentPreset("x264");
            else if (presets.Count > 0)
                SetCurrentPreset(presets.Keys.First());
        }

        public static void SetCurrentPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName))
            {
                Debug.LogWarning("[CaptureTools]: Attempted to set current preset with an empty name.");
                return;
            }

            if (presets.ContainsKey(presetName))
                currentPresetName = presetName;
            else
                Debug.LogError("[CaptureTools]: Failed to set current preset. Preset with name '" + presetName + "' does not exist.");
        }

        /*public static bool DuplicatePreset(string presetName, out string newName)
        {
            newName = presetName;

            if (string.IsNullOrEmpty(presetName))
            {
                Debug.LogWarning("[CaptureTools]: Attempted to duplicate a preset with an empty name.");
                return false;
            }

            if (presets.TryGetValue(presetName, out FFmpegPreset presetToCopy))
            {
                FFmpegPreset newPreset = new FFmpegPreset(presetToCopy)
                {
                    Editable = true,
                    Name = GetUnusedName(presetToCopy.Name),
                    Author = "Player"
                };

                newPreset.SaveToFile();
                AddPreset(newPreset);
                return true;
            }
            else
            {
                Debug.LogError("[CaptureTools]: Failed to duplicate preset. Preset with name '" + presetName + "' does not exist.");
                return false;
            }
        }*/

        private static string GetUnusedName(string name)
        {
            if (!presets.ContainsKey(name))
                return name;

            return GetUnusedName(name + " (Copy)");
        }
    }
}
