using CaptureTools.Utils;
using System;
using System.Reflection;

namespace CaptureTools.Integration
{
    internal static class BD
    {
        internal static bool checkedFor = false;
        internal static bool loaded = false;
        internal static FieldInfo targetField;

        internal static void Check()
        {
            if (checkedFor)
                return;

            checkedFor = true;

            try
            {
                foreach (AssemblyLoader.LoadedAssembly assy in AssemblyLoader.loadedAssemblies)
                {
                    if (assy.assembly.FullName.Contains("BDArmory"))
                    {
                        loaded = true;

                        Type aiModuleType = assy.assembly.GetType("BDArmory.Control.BDGenericAIBase");
                        targetField = GetAITargetField(aiModuleType);

                        return;
                    }
                }
            }
            catch (Exception e)
            {
                CTDebug.LogError($"Failed to setup BD integration: {e.Message}");
            }
        }

        public static bool TryGetAI(Vessel v, out PartModule AIModule)
        {
            if (v)
            {
                foreach (Part p in v.parts)
                {
                    if (p.GetComponent("BDModulePilotAI"))
                    {
                        AIModule = (PartModule)p.GetComponent("BDModulePilotAI");
                        return true;
                    }
                    if (p.GetComponent("BDModuleVTOLAI"))
                    {
                        AIModule = (PartModule)p.GetComponent("BDModuleVTOLAI");
                        return true;
                    }
                    if (p.GetComponent("BDModuleSurfaceAI"))
                    {
                        AIModule = (PartModule)p.GetComponent("BDModuleSurfaceAI");
                        return true;
                    }
                }
            }

            AIModule = null;

            return false;
        }

        public static FieldInfo GetAITargetField(Type aiModType)
        {
            if (aiModType == null)
                return null;

            FieldInfo[] fields = aiModType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo f in fields)
            {
                if (f.Name == "targetVessel")
                {
                    //bdAITargetFieldGetter = ReflectionUtils.CreateGetter<object, Vessel>(f);
                    //if (CamTools.DEBUG) Debug.Log("[CameraTools]: Created bdAITargetFieldGetter.");

                    return f;
                }
            }

            return null;
        }

        /*public static void AddTestCanvas(Camera camera)
        {
            //https://github.com/BrettRyland/BDArmory/blob/93bda2f0d902dcc11e79c66ce5456a6ef770967a/BDArmory/Competition/BDACompetitionMode.cs#L2520


            // Add a test canvas to the individual canvas with the words "TEST" on it.
            var canvasObject = new GameObject("Capture Tools TestCanvas" + camera.name, typeof(Canvas), typeof(CanvasScaler));
            
            // Set the canvas to render to the camera.
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.transform.SetParent(camera.transform, false);

            // Set the canvas scaler to scale with the screen size.
            //var scaler = canvasObject.GetComponent<CanvasScaler>();
            //scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // Add a text object to the canvas.
            //var textObject = new GameObject("Capture Tools TestText" + camera.name, typeof(Text));
            //textObject.transform.SetParent(canvasObject.transform, false);
            //var text = textObject.GetComponent<Text>();
            //text.text = "TEST" + camera.name;
            //text.fontSize = 100;
            //text.alignment = TextAnchor.MiddleCenter;
            //text.color = Color.red;

            var textObject = new GameObject("Capture Tools TestText" + camera.name, typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = "TEST" + camera.name;
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.red;
            text.name = "Capture Tools Text " + camera.name;

            // Set the rect size to fit the text.
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(text.preferredWidth, text.preferredHeight);
        }*/
    }

    /*Type GetAIModuleType()
    {
        foreach (var assy in AssemblyLoader.loadedAssemblies)
        {
            if (assy.assembly.FullName.Contains("BDArmory"))
            {
                foreach (var t in assy.assembly.GetTypes())
                {
                    if (t.Name == "BDGenericAIBase")
                    {
                        if (CamTools.DEBUG) Debug.Log("[CameraTools]: Found BDGenericAIBase type.");
                        return t;
                    }
                }
            }
        }

        return null;
    }*/
}
