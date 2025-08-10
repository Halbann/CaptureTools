using CaptureTools.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace CaptureTools
{
    public static class HDRI
    {
        // HDRI
        public static float widthPower = 11;
        public static int width = 2048;
        public static float sunBrightness = 200;
        public static bool hideKerbalsInEditor = true;
        public static void CaptureHDRI() => CaptureHDRI(width, sunBrightness, hideKerbalsInEditor);

        public static void CaptureHDRI(int width, float sunBrightness = 200, bool hideKerbalsInEditor = false, bool disablePostProcessing = true)
        {
            Color sunColourOriginal = GetSunColour();
            HDRI.SetSunColour(new Color(sunBrightness, sunBrightness, sunBrightness, 1));

            int[] faceMasks = new int[] { 32, 16, 8, 4, 2, 1 };

            RenderTexture cubemap = new RenderTexture(width, width, 24, DefaultFormat.HDR);
            cubemap.dimension = TextureDimension.Cube;

            // Because KSP uses multiple cameras with no background clearing,
            // RenderToCubemap will draw on top of itself from each perspective
            // unless we render each face individually.

            GameScenes scene = HighLogic.LoadedScene;
            if (scene == GameScenes.FLIGHT || scene == GameScenes.SPACECENTER)
            {
                List<Camera> flightCameras = CaptureTools.cameraNames.Select(n => Camera.allCameras.FirstOrDefault(c => c.name == n)).ToList();

                if (flightCameras.Count < 1)
                {
                    CTDebug.LogWarning("Attempting to take an HDRI but there are no cameras.");
                    cubemap.Release();
                    return;
                }

                bool postProcessingEnabled = CaptureTools.GetCameraPostProcessEnabled(flightCameras.Last());
                if (disablePostProcessing)
                    flightCameras.ForEach(c => CaptureTools.ToggleCameraPostProcess(c, false));

                // Render each face.
                foreach (int faceMask in faceMasks)
                {
                    // Render each camera to each face.
                    foreach (Camera flightCam in flightCameras)
                    {
                        bool allowHDR = flightCam.allowHDR;
                        bool allowMSAA = flightCam.allowMSAA;

                        flightCam.allowHDR = true;
                        flightCam.allowMSAA = true;

                        if (disablePostProcessing)
                            CaptureTools.ToggleCameraPostProcess(flightCam, false);

                        flightCam.RenderToCubemap(cubemap, faceMask);

                        flightCam.allowHDR = allowHDR;
                        flightCam.allowMSAA = allowMSAA;
                    }
                }

                if (disablePostProcessing)
                    flightCameras.ForEach(c => CaptureTools.ToggleCameraPostProcess(c, postProcessingEnabled));
            }
            else
            {
                // todo: why not use editor camera?

                Camera main = Camera.main;

                GameObject camObject = new GameObject("CaptureTools HDRI Camera");
                camObject.transform.position = main.transform.position;
                camObject.transform.rotation = main.transform.rotation;

                Camera cam = camObject.AddComponent<Camera>();
                cam.allowMSAA = true;
                cam.allowHDR = true;
                cam.depth = -99;

                if (hideKerbalsInEditor)
                    cam.cullingMask = 65535;

                // Render all faces.
                cam.RenderToCubemap(cubemap, 63);

                UnityEngine.Object.Destroy(camObject);
            }

            // Convert to equirect.
            // todo: get temp RT or discard this step?
            RenderTexture combined = new RenderTexture(width * 2, width, 24, DefaultFormat.HDR);
            cubemap.ConvertToEquirect(combined, Camera.MonoOrStereoscopicEye.Mono);
            cubemap.Release();

            // Transfer from render texture to texture2D.
            // todo: why make a texture with alpha? can alpha not be discarded automatically?

            RenderTexture.active = combined;
            Texture2D tex = new Texture2D(width * 2, width, TextureFormat.RGBAFloat, false);
            tex.ReadPixels(new Rect(0, 0, width * 2, width), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            combined.Release();

            // Discard alpha.
            Color[] pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i].a = 1;

            tex.SetPixels(pixels);
            tex.Apply();

            // Save texture2D to file.
            NativeArray<byte> imageBytes = new NativeArray<byte>(tex.GetRawTextureData(), Allocator.Temp);
            byte[] bytes;

            try
            {
                // todo: make sure that texture is being copied from GPU once and never copied back.

                bytes = ImageConversion.EncodeNativeArrayToEXR(imageBytes, tex.graphicsFormat,
                    (uint)tex.width, (uint)tex.height, 0, Texture2D.EXRFlags.CompressZIP).ToArray();

                string name = DateTime.Now.ToString("yyyyMMddHHmmss") + "_CT_HDRI.exr";

                string path = Path.GetFullPath(Path.Combine(CaptureTools.FilePath, "HDRIs"));
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                File.WriteAllBytes(Path.Combine(path, name), bytes);
            }
            catch
            {
                CTDebug.LogError("Could not save HDRI. Try reducing the size.");
            }

            imageBytes.Dispose();
            bytes = null;

            // Restore sun.
            SetSunColour(sunColourOriginal);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        public static void SetSunColour(Color colour)
        {
            try
            {
                ScaledSun.Instance.gameObject.GetComponent<MeshRenderer>().material.SetColor("_RimColor", colour);
            }
            catch
            {
                CTDebug.LogError("Could not set sun brightness.");
            }
        }

        public static Color GetSunColour()
        {
            try
            {
                return ScaledSun.Instance.gameObject.GetComponent<MeshRenderer>().material.GetColor("_RimColor");
            }
            catch (Exception)
            {
                return Color.white;
            }
        }

        public static void SetSPHLightsBrightness(float brightness, float currentBrightness = 1.18f)
        {
            Material material;
            Texture illum;

            try
            {
                string sph = "SPHmodern/SPH_interior_modern/SPH_Interior_Geometry/model_sph_interior_main_v16";
                material = GameObject.Find(sph).GetComponent<MeshRenderer>().materials[2];
                illum = material.GetTexture("_Illum");
            }
            catch
            {
                CTDebug.LogError("Couldn't find SPH materials while trying to set lighting.");
                return;
            }

            Texture2D illumReadable = GetUnreadableTexture(illum);

            // Amplify the brightness.
            Color[] pixels = illumReadable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white * (pixels[i].r / currentBrightness * brightness);

            illumReadable.SetPixels(pixels);
            illumReadable.Apply();

            // Set the new texture.

            material.SetTexture("_Illum", illumReadable);
        }

        private static Texture2D GetUnreadableTexture(Texture texture)
        {
            // Create a temporary RenderTexture of the same size as the texture
            RenderTexture tmp = RenderTexture.GetTemporary(
                                texture.width,
                                texture.height,
                                0,
                                RenderTextureFormat.DefaultHDR,
                                RenderTextureReadWrite.Linear);

            // Blit the pixels on texture to the RenderTexture
            Graphics.Blit(texture, tmp);

            // Backup the currently set RenderTexture
            RenderTexture previous = RenderTexture.active;

            // Set the current RenderTexture to the temporary one we created
            RenderTexture.active = tmp;

            // Create a new readable Texture2D to copy the pixels to it
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height, DefaultFormat.HDR, TextureCreationFlags.None);

            // Copy the pixels from the RenderTexture to the new Texture
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();

            // Reset the active RenderTexture
            RenderTexture.active = previous;

            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tmp);

            return myTexture2D;
        }
    }
}