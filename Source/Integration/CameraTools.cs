using CaptureTools.Utils;
using System;
using System.Reflection;

namespace CaptureTools.Integration
{
    public static class CameraTools
    {
        // Camera Tools integration.
        // todo: create addon.

        public static bool loaded = false;
        public static UnityEngine.Object instance;
        public static string cameraKey;
        public static string revertKey;
        public static Type type;

        public static void Check()
        {
            try
            {
                if (type == null)
                    foreach (AssemblyLoader.LoadedAssembly assy in AssemblyLoader.loadedAssemblies)
                        if (assy.assembly.FullName.Contains("CameraTools"))
                            type = assy.assembly.GetType("CameraTools.CamTools");

                instance = UnityEngine.Object.FindObjectOfType(type);

                if (instance != null)
                {
                    loaded = true;

                    FieldInfo cameraKey = type.GetField("cameraKey", BindingFlags.Public | BindingFlags.Instance);
                    CameraTools.cameraKey = (string)cameraKey.GetValue(instance);

                    FieldInfo revertKey = type.GetField("revertKey", BindingFlags.Public | BindingFlags.Instance);
                    CameraTools.revertKey = (string)revertKey.GetValue(instance);
                }
            }
            catch (Exception e)
            {
                CTDebug.LogError($"Failed to get Camera Tools keybinds: {e.Message}");
            }
        }
    }
}