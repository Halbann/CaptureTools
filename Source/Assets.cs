using System.IO;
using UnityEngine;
using UnityEngine.Audio;

namespace CaptureTools
{
    /*[KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class CaptureToolsAssets : MonoBehaviour
    {
        public static AssetBundle bundle;
        private bool loaded = false;

        public static Shader equirectangularShader;

        void Awake()
        {
            if (loaded) return;

            string path = KSPUtil.ApplicationRootPath + "GameData" + 
                Path.DirectorySeparatorChar + "CaptureTools" + 
                Path.DirectorySeparatorChar + "AssetBundles" +
                Path.DirectorySeparatorChar + "assets";

            bundle = AssetBundle.LoadFromFile(path);
            loaded = true;

            GetShaders();
        }

        void GetShaders()
        {
            equirectangularShader = bundle.LoadAsset<Shader>("Assets/EquirectangularConverter.shader");
        }
    }*/

}
