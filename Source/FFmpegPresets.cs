using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class FFmpegPresets : MonoBehaviour
    {
        public static Dictionary<string, FFmpegPreset> presets = new Dictionary<string, FFmpegPreset>();
        public static FFmpegPreset currentPreset = fallbackPreset;
        public static FFmpegPreset fallbackPreset = new FFmpegPreset("Default", false,
            "-y -f rawvideo -vcodec rawvideo -pixel_format rgba -colorspace bt709 -video_size {width}x{height} -framerate {framerate} -loglevel error -i - -pix_fmt yuv420p -crf {crf} -timecode {timecode} \"{path}.mp4\"");

        public static string defaultPresetPath;

        protected void Awake()
        {
            defaultPresetPath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "CaptureTools", "PluginData");
            LoadPresets();
        }

        private static void LoadPresets()
        {
            presets.Clear();

            // https://github.com/KSPModStewards/TUFX/blob/765d244b7e0765b25187ec518b283d94d72f0fc0/Source/TexturesUnlimitedFXLoader.cs#L370
            // How to save afterwards? I guess we have the URL. What if you have more than one preset per file?

            var presetsUnsorted = new List<FFmpegPreset>();
            FFmpegPreset preset;

            // todo: doesn't load presets in PluginData (by design).

            foreach (var profileConfig in GameDatabase.Instance.root.GetConfigs("CAPTURETOOLS_FFMPEG_PRESET"))
            {
                try
                {
                    preset = new FFmpegPreset(profileConfig);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CaptureTools]: Error while loading FFmpeg preset from config {profileConfig.name}: {e.Message}");
                    continue;
                }

                if (presetsUnsorted.FindIndex(p => p.Name == preset.Name) > -1)
                {
                    Debug.LogError("[CaptureTools]: Already loaded a preset with the name: " + preset.Name + ". Please check your configurations and remove any duplicates. Only the first configuration parsed for any one name will be loaded.");
                    continue;
                }

                presetsUnsorted.Add(preset);
            }

            SortPresets(presetsUnsorted);

            if (presets.ContainsKey("x264"))
                Set("x264");
            else if (presets.Count > 0)
                Set(presets.Keys.First());
        }

        public static void SortPresets(List<FFmpegPreset> unsortedPresets = null)
        {
            if (!presets.Any() && !(unsortedPresets?.Any() ?? false))
                return;

            if (unsortedPresets == null)
                 unsortedPresets = new List<FFmpegPreset>(presets.Values);

            presets.Clear();

            foreach (var p in unsortedPresets.OrderBy(p => p.Name))
            {
                if (!presets.ContainsKey(p.Name))
                    presets.Add(p.Name, p);
            }
        }

        public static void Set(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[CaptureTools]: Attempted to set a preset with an empty name.");
                return;
            }

            if (presets.TryGetValue(name, out FFmpegPreset preset))
            {
                currentPreset = preset;
            }
            else
            {
                Debug.LogError("[CaptureTools]: Failed to load FFmpeg preset with name: " + name + ".");
            }
        }
    }

    public class FFmpegPreset
    {
        private string name;
        private string command;
        private string author;

        public string Name
        {
            get => name;
            set => Set(ref name, value);
        }

        public string Command
        {
            get => command;
            set => Set(ref command, value);
        }

        public string Author
        {
            get => author;
            set => Set(ref author, value);
        }

        public bool Modified { get; private set; }
        public bool editingAllowed = true;

        // Is it worth the time to consolidate urlConfig and path?
        public string CfgPath => urlConfig != null ? Path.GetFullPath(Path.Combine("GameData", urlConfig?.parent.url + ".cfg")) : path;
        private UrlDir.UrlConfig urlConfig;
        private string path;

        const string PROFILE_NODE_NAME = "CAPTURETOOLS_FFMPEG_PRESET";

        public FFmpegPreset(string name, bool editingAllowed, string command)
        {
            this.name = name;
            this.command = command;
            this.editingAllowed = editingAllowed;
        }

        public FFmpegPreset(UrlDir.UrlConfig config)
        {
            urlConfig = config;
            Reload();
        }

        private void Set<T>(ref T field, T value)
        {
            if (!editingAllowed)
                return;

            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            if (!Modified)
                Modified = true;

            field = value;
        }

        private ConfigNode SaveToNode()
        {
            ConfigNode node = new ConfigNode(PROFILE_NODE_NAME);
            node.SetValue("name", name, true);
            node.SetValue("author", author, true);
            node.SetValue("command", Uri.EscapeDataString(command), true);
            node.SetValue("editingAllowed", editingAllowed, true);

            return node;
        }

        public void Save(bool force = false)
        {
            if (!force && !Modified)
                return;

            author = Environment.UserName;

            try
            {
                // If the config is null, it means this is a Save As and we should create a new file.
                if (urlConfig != null)
                    SaveExisting();
                else
                    SaveNew();
            }
            catch (Exception e)
            {
                Debug.LogError("[CaptureTools]: Error while saving FFmpeg preset: " + e.Message);
            }

            Modified = false;

            if (!FFmpegPresets.presets.ContainsValue(this))
            {
                FFmpegPresets.presets.Add(Name, this);
                FFmpegPresets.SortPresets();
            }
        }

        private void SaveNew()
        {
            foreach (var preset in FFmpegPresets.presets.Values)
                if (preset != this && preset.Name == name)
                    throw new Exception($"A preset with the name '{name}' already exists at {preset.CfgPath}.");
            
            var node = new ConfigNode(PROFILE_NODE_NAME);
            node.AddNode(SaveToNode());

            if (!Directory.Exists(FFmpegPresets.defaultPresetPath))
                Directory.CreateDirectory(FFmpegPresets.defaultPresetPath);

            var invalidChars = Path.GetInvalidFileNameChars();
            string fileName = new string(Name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray()) + ".cfg";
            string filePath = Path.Combine(FFmpegPresets.defaultPresetPath, fileName);

            // The file doesn't exist or it does exist but it was created by this FFmpegPreset.
            if (!File.Exists(filePath) || path == filePath)
            {
                node.Save(filePath, "This config file was generated automatically.");
                path = filePath;
            }
            else
            {
                throw new Exception($"A file with the name '{fileName}' already exists at {filePath}." +
                    $" Please choose a different name or delete the existing file.");
            }
        }

        private void SaveExisting()
        {
            UrlDir.UrlFile newFile = new UrlDir.UrlFile(urlConfig.parent.parent, new FileInfo(urlConfig.parent.fullPath));

            for (int i = 0; i < newFile.configs.Count; ++i)
            {
                if (newFile.configs[i].type == PROFILE_NODE_NAME && newFile.configs[i].name == name)
                {
                    newFile.configs[i].config = SaveToNode();
                    newFile.SaveConfigs();
                    urlConfig = newFile.configs[i];
                    return;
                }
            }
        }

        public void Reload()
        {
            if (string.IsNullOrEmpty(CfgPath))
                return;

            LoadFromNode(urlConfig?.config ?? ConfigNode.Load(path).GetNode(PROFILE_NODE_NAME));
        }

        private void LoadFromNode(ConfigNode node)
        {
            try
            {
                name = RequireValue(node, "name");
                command = Uri.UnescapeDataString(RequireValue(node, "command"));
                node.TryGetValue("editingAllowed", ref editingAllowed);
                node.TryGetValue("author", ref author);

                Modified = false;
            }
            catch 
            {
                Debug.LogError("[CaptureTools]: Error while loading FFmpeg preset from config node: " + node.name + ". Check one of the default presets for reference.");
            }
        }

        private string RequireValue(ConfigNode node, string key)
        {
            string value = string.Empty;
            if (node.TryGetValue(key, ref value))
                return value;

            throw new Exception($"Required value '{key}' not found in config node '{node.name}'.");
        }

        public void Delete()
        {
            if (FFmpegPresets.presets.Count < 2 && FFmpegPresets.presets.ContainsValue(this))
            {
                Debug.LogWarning("[CaptureTools]: Attempted to delete a preset when there is only one preset available. Cannot delete the last preset.");
                return;
            }

            if (string.IsNullOrEmpty(CfgPath))
            {
                Debug.LogWarning("[CaptureTools]: Attempted to delete a preset without a valid path.");
                return;
            }

            if (!File.Exists(CfgPath))
            {
                Debug.LogWarning("[CaptureTools]: Attempted to delete a preset that does not exist at path: " + path);
                return;
            }

            try
            {
                if (urlConfig == null)
                {
                    File.Delete(path);
                    Debug.Log("[CaptureTools]: Successfully deleted FFmpeg preset: " + name);
                }
                else
                {
                    UrlDir.UrlFile file = new UrlDir.UrlFile(urlConfig.parent.parent, new FileInfo(urlConfig.parent.fullPath));
                    for (int i = 0; i < file.configs.Count; ++i)
                    {
                        if (file.configs[i].type == PROFILE_NODE_NAME && file.configs[i].name == name)
                        {
                            file.configs.RemoveAt(i);
                            file.SaveConfigs();
                            Debug.Log("[CaptureTools]: Successfully deleted FFmpeg preset: " + name);
                            return;
                        }
                    }
                }

                if (FFmpegPresets.currentPreset == this && FFmpegPresets.presets.ContainsValue(this))
                {
                    var list = FFmpegPresets.presets.Values.ToList();
                    FFmpegPresets.currentPreset = list[(list.IndexOf(this) - 1 + list.Count) % list.Count];
                }

                FFmpegPresets.presets.Remove(name);
            }
            catch (Exception e)
            {
                Debug.LogError("[CaptureTools]: Error while deleting FFmpeg preset: " + e.Message);
            }
        }
    }
}
