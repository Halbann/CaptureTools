using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine;

namespace CaptureTools
{
    partial class CaptureTools
    {
        // HDRI
        public static float HDRIWidthPower = 11;
        public static int HDRIWidth = 2048;
        public static float HDRISunBrightness = 200;
        public static bool HDRIHideKerbalsInEditor = true;

        #region Equirectangular

        /*void CaptureTexture(Camera cam = null, bool clear = false)
        {
            // Render camera to texture2D.

            int width = Screen.width;
            int height = Screen.height;

            RenderTexture renderTexture = new RenderTexture(width, height, 24, DefaultFormat.HDR);

            if (cam == null)
            {
                foreach (string cameraName in cameraNames)
                {
                    cam = Camera.allCameras.FirstOrDefault(c => c.name == cameraName);
                    var targetTexture = cam.targetTexture;
                    cam.targetTexture = renderTexture;
                    cam.Render();
                    cam.targetTexture = targetTexture;
                }
            }
            else
            {
                var clearFlags = cam.clearFlags;
                var backgroundColor = cam.backgroundColor;

                if (clear)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0, 0, 0, 1);
                }

                var targetTexture = cam.targetTexture;
                cam.targetTexture = renderTexture;
                cam.Render();
                cam.targetTexture = targetTexture;

                cam.clearFlags = clearFlags;
                cam.backgroundColor = backgroundColor;
            }

            RenderTexture.active = renderTexture;

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // discard alhpa

            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = 1;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            // Save texture2D to file.
            var imageBytes = new NativeArray<byte>(tex.GetRawTextureData(), Allocator.Temp);
            var bytes = ImageConversion.EncodeNativeArrayToEXR(imageBytes, tex.graphicsFormat, (uint)tex.width, (uint)tex.height).ToArray();
            
            string name = DateTime.Now.ToString("yyyy MMdd HHmmss") + cam.name;
            name = name.Replace(" ", "_");
            File.WriteAllBytes(Path.Combine(pluginDataPath, name + "_HDRI.exr"), bytes);

            // Cleanup.
            RenderTexture.active = null;
            renderTexture.Release();
            Destroy(tex);

            Debug.Log("HDRI saved to " + Path.Combine(pluginDataPath, name + "_HDRI.exr"));
        }*/

        /////////////////////////////////////////////////////

        public void CaptureHDRI() =>
            Instance.CaptureHDRI(HDRIWidth, HDRISunBrightness, HDRIHideKerbalsInEditor);

        public void CaptureHDRI(int width, float sunBrightness = 200, bool hideKerbalsInEditor = false, bool disablePostProcessing = true)
        {
            Color sunColourOriginal = GetSunColour();
            SetSunColour(new Color(sunBrightness, sunBrightness, sunBrightness, 1));

            int[] faceMasks = new int[] { 32, 16, 8, 4, 2, 1 };

            //List<int> faceMasksFiltered = new List<int>();
            //for (int i = 0; i < faceMasks.Length; i++)
            //{
            //    if ((faceMaskSelection & faceMasks[i]) == faceMasks[i])
            //        faceMasksFiltered.Add(faceMasks[i]);
            //}

            RenderTexture cubemap = new RenderTexture(width, width, 24, DefaultFormat.HDR);
            cubemap.dimension = TextureDimension.Cube;

            // Because KSP uses multiple cameras with no background clearing,
            // RenderToCubemap will draw on top of itself from each perspective
            // unless we render each face individually.

            var scene = HighLogic.LoadedScene;
            if (scene == GameScenes.FLIGHT || scene == GameScenes.SPACECENTER)
            {
                List<Camera> flightCameras = cameraNames.Select(n => Camera.allCameras.FirstOrDefault(c => c.name == n)).ToList();

                if (flightCameras.Count < 1)
                {
                    Debug.Log("[CaptureTools]: Attmepting to take an HDRI but there are no cameras.");
                    cubemap.Release();
                    return;
                }

                bool postProcessingEnabled = GetCameraPostProcessEnabled(flightCameras.Last());
                if (disablePostProcessing)
                    flightCameras.ForEach(c => ToggleCameraPostProcess(c, false));

                // Render each face.
                foreach (int faceMask in faceMasks)
                {
                    // Render each camera to each face.
                    foreach (Camera flightCam in flightCameras)
                    {
                        var allowHDR = flightCam.allowHDR;
                        var allowMSAA = flightCam.allowMSAA;

                        flightCam.allowHDR = true;
                        flightCam.allowMSAA = true;

                        if (disablePostProcessing)
                            ToggleCameraPostProcess(flightCam, false);

                        flightCam.RenderToCubemap(cubemap, faceMask);

                        flightCam.allowHDR = allowHDR;
                        flightCam.allowMSAA = allowMSAA;
                    }
                }

                if (disablePostProcessing)
                    flightCameras.ForEach(c => ToggleCameraPostProcess(c, postProcessingEnabled));
            }
            else
            {
                Camera main = Camera.main;

                Camera cam;
                GameObject camObject = new GameObject("CaptureTools HDRI Camera");

                camObject.transform.position = main.transform.position;
                camObject.transform.rotation = main.transform.rotation;

                cam = camObject.AddComponent<Camera>();
                cam.allowMSAA = true;
                cam.allowHDR = true;
                cam.depth = -99;

                if (hideKerbalsInEditor)
                    cam.cullingMask = 65535;

                // Render all faces.
                cam.RenderToCubemap(cubemap, 63);

                Destroy(camObject);
            }

            // Convert to equirect.
            RenderTexture combined = new RenderTexture(width * 2, width, 24, DefaultFormat.HDR);
            cubemap.ConvertToEquirect(combined, Camera.MonoOrStereoscopicEye.Mono);
            cubemap.Release();

            // Transfer from render texture to texture2D.
            RenderTexture.active = combined;
            Texture2D tex = new Texture2D(width * 2, width, TextureFormat.RGBAFloat, false);
            tex.ReadPixels(new Rect(0, 0, width * 2, width), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            combined.Release();

            // Discard alpha.
            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = 1;
            }
            tex.SetPixels(pixels);
            tex.Apply();



            // Save texture2D to file.
            var imageBytes = new NativeArray<byte>(tex.GetRawTextureData(), Allocator.Temp);
            byte[] bytes;

            try
            {
                bytes = ImageConversion.EncodeNativeArrayToEXR(
                imageBytes,
                tex.graphicsFormat,
                (uint)tex.width,
                (uint)tex.height,
                0,
                Texture2D.EXRFlags.CompressZIP).ToArray();

                string name = DateTime.Now.ToString("yyyyMMddHHmmss") + "_CT_HDRI.exr";

                string path = Path.GetFullPath(Path.Combine(FilePath, "HDRIs"));
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                File.WriteAllBytes(Path.Combine(path, name), bytes);
            }
            catch
            {
                Debug.LogError("[CaptureTools]: Could not save HDRI. Try reducing the size.");
            }

            imageBytes.Dispose();
            bytes = null;

            //string pizname = DateTime.Now.ToString("yyyyMMddHHmmss") + "_CT_HDRI_PIZ.exr";
            //File.WriteAllBytes(Path.Combine(pluginDataPath, pizname), PIZ);

            // Restore sun.
            SetSunColour(sunColourOriginal);

            DestroyImmediate(tex);
        }

        public static void SetSunColour(Color colour)
        {
            try
            {
                ScaledSun.Instance.gameObject.GetComponent<MeshRenderer>().material.SetColor("_RimColor", colour);
            }
            catch (Exception)
            {
                Debug.Log("[CaptureTools]: Could not set sun brightness.");
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
                illum = material.GetTexture("_Illum") as Texture;
                //illum = material.GetTexture("_MainTex") as Texture;
            }
            catch
            {
                Debug.Log("[CaptureTools]: Couldn't find SPH materials while trying to set lighting.");
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

        #endregion
    }
}
