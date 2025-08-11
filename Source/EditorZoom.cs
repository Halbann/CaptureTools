using UnityEngine;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorZoom : MonoBehaviour
    {
        private float lastMiddleMouseClickTime;
        public static float editorZoomSpeed = 0.05f;
        private bool editorZoomScrollLock = false;
        private Camera[] editorCameras;

        private Camera[] EditorCameras =>
            editorCameras ?? (editorCameras = EditorLogic.fetch.editorCamera.GetComponentsInChildren<Camera>());

        protected void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt))
            {
                InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS, "CaptureToolsEditorZoom");
                editorZoomScrollLock = true;

                // Scroll zoom.
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    // todo: test new method.

                    Camera camera = EditorLogic.fetch.editorCamera;
                    float fov = Mathf.Clamp(camera.fieldOfView * (1 + editorZoomSpeed * Mathf.Sign(scroll)), 5f, 90f);

                    foreach (var c in EditorCameras)
                        c.fieldOfView = fov;
                }
            }
            else if (editorZoomScrollLock)
            {
                InputLockManager.RemoveControlLock("CaptureToolsEditorZoom");
                editorZoomScrollLock = false;
            }

            // Double click middle mouse button to reset zoom.
            if (Input.GetKeyDown(KeyCode.Mouse2))
            {
                if (Time.realtimeSinceStartup - lastMiddleMouseClickTime < 0.5f)
                    foreach (var c in EditorCameras)
                        c.fieldOfView = 60;

                lastMiddleMouseClickTime = Time.realtimeSinceStartup;
            }
        }
    }
}
