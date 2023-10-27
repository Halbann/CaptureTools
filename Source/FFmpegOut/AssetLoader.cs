using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FFmpegOut
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class FFmpegOutAssets : MonoBehaviour
    {
        private static bool _loaded;
        private static string _bundlePath;

        public static Shader blitterShader;
        public static Shader preprocessShader;

        public string ShadersPath
        {
            get
            {
                switch (Application.platform)
                {
                    //case RuntimePlatform.OSXPlayer:
                    //    return _bundlePath + Path.DirectorySeparatorChar +
                    //           "KCSshaders_macosx.bundle";

                    //case RuntimePlatform.WindowsPlayer:
                    //    return _bundlePath + Path.DirectorySeparatorChar +
                    //           "KCSshaders_windows.bundle";

                    //case RuntimePlatform.LinuxPlayer:
                    //    return _bundlePath + Path.DirectorySeparatorChar +
                    //    "KCSshaders_macosx.bundle";

                    default:
                        return _bundlePath + Path.DirectorySeparatorChar +
                               "shaders";
                }
            }
        }

        //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private void Awake()
        {
            _bundlePath = KSPUtil.ApplicationRootPath + "GameData" +
                Path.DirectorySeparatorChar + "CaptureTools" +
                Path.DirectorySeparatorChar + "AssetBundles";

            //_bundlePath = Application.dataPath;

            //List<string> path = Application.dataPath.Split(Path.PathSeparator).ToList();
            //path.Remove(path.Last());
            //string rootPath = Path.Combine(path.ToArray());
            //_bundlePath = Path.Combine(rootPath, "GameData", "CaptureTools", "AssetBundles");

            if (!_loaded)
            {
                LoadShaderAssets();
                _loaded = true;
            }
        }

        //private void Start()
        //{
        //}

        private void LoadShaderAssets()
        {
            AssetBundle shaderBundle = AssetBundle.LoadFromFile(ShadersPath);

            if (shaderBundle != null)
            {
                Shader[] shaders = shaderBundle.LoadAllAssets<Shader>();
                IEnumerator<Shader> shader = shaders.AsEnumerable().GetEnumerator();
                while (shader.MoveNext())
                {
                    if (shader.Current == null) continue;
                    //Debug.Log($"[KCS] Shader \"{shader.Current.name}\" loaded. Shader supported? {shader.Current.isSupported}");

                    switch (shader.Current.name)
                    {
                        case "Hidden/FFmpegOut/Blitter":
                            blitterShader = shader.Current;
                            break;

                        case "Hidden/FFmpegOut/Preprocess":
                            preprocessShader = shader.Current;
                            break;

                        default:
                            Debug.Log($"[CaptureTools] Unexpected shader : {shader.Current.name}");
                            break;
                    }
                }

                shader.Dispose();
                shaderBundle.Unload(false); // unload the raw asset bundle
            }
            else
            {
                Debug.Log("[CaptureTools] Error: Found no asset bundle to load");
            }
        }
    }
}