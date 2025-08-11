using System;
using System.Reflection;
using UnityEngine;

namespace CaptureTools.Integration
{
    internal static class PostProcessing
    {
        public static bool GetEnabled(Camera cam)
        {
            Behaviour postProcessLayer = cam.gameObject.GetComponent("PostProcessLayer") as Behaviour;
            return postProcessLayer != null && postProcessLayer.enabled;
        }

        public static void Toggle(Camera cam, bool enabled)
        {
            Behaviour postProcessLayer = cam.gameObject.GetComponent("PostProcessLayer") as Behaviour;
            if (postProcessLayer != null)
                postProcessLayer.enabled = enabled;
        }

        public static void Copy(Camera template, Camera camera)
        {
            // todo: should really be in a TUFX integration module.
            // drop support for K3SP, it never worked anyway.

            Component templateLayer = template.gameObject.GetComponent("PostProcessLayer");
            if (templateLayer != null)
            {
                Type layerType = templateLayer.GetType();
                Component layer = camera.gameObject.AddComponent(layerType);

                // set the volume layer.
                FieldInfo volumeLayer = layerType.GetField("volumeLayer", BindingFlags.Public | BindingFlags.Instance);
                volumeLayer.SetValue(layer, volumeLayer.GetValue(templateLayer));

                // call Init(resources) function on the layer.
                FieldInfo resources = layerType.GetField("m_Resources", BindingFlags.NonPublic | BindingFlags.Instance);
                layerType.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance).Invoke(layer, new object[] { resources.GetValue(templateLayer) });
            }
        }
    }
}