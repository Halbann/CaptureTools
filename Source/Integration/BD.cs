using System;
using System.Reflection;
using UnityEngine;

namespace CaptureTools
{
    internal static class BD
    {
        internal static bool checkedForBD = false;
        internal static bool BDLoaded = false;
        internal static FieldInfo bdTargetField;

        internal static void BDArmouryCheck()
        {
            if (checkedForBD)
                return;

            checkedForBD = true;

            try
            {
                foreach (var assy in AssemblyLoader.loadedAssemblies)
                {
                    if (assy.assembly.FullName.Contains("BDArmory"))
                    {
                        BDLoaded = true;

                        var aiModuleType = assy.assembly.GetType("BDArmory.Control.BDGenericAIBase");
                        bdTargetField = GetAITargetField(aiModuleType);

                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CaptureTools]: Failed to setup BD integration: {e.Message}");
            }
        }

        public static bool TryGetBDAI(Vessel v, out PartModule AIModule)
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
            foreach (var f in fields)
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
