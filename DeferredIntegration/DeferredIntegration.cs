using Deferred;
using UnityEngine;

namespace CaptureTools
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class DeferredIntegration : MonoBehaviour
    {
        public static bool enableForwardRenderingCompatibility = true;

        protected void Awake()
        {
            CaptureTools.OnSetupCameras += SetupCameras;
        }

        public static void SetupCameras(Camera localSpace, Camera scaledSpace, bool doubleAA = false)
        {
            Debug.Log($"[CaptureTools]: Setting up Deferred integration on {localSpace.name} and {scaledSpace.name}.");

            if (localSpace != null)
            {
                // Deferred rendering prevents alpha blending from working, so special components are used by deferred to cut holes through the
                // local space image for the terrain from the scaled space image to show through.
                if (!localSpace.GetComponent<DeferredPQSFade>())
                    localSpace.gameObject.AddComponent<DeferredPQSFade>();

                // A problem arises when using Deferred and TUFX together.
                // The quad that is created by ForwardRenderingCompatibility seems to be causing issues in the motion vectors.
                // The best way to reproduce the issue is to enable motion blur, turn up the smoothing a bit, and suddenly zoom in the camera.

                // This fix (enableForwardRenderingCompatibility) has a side effect of messing up the motion vectors/depth buffer on the stock camera.
                if (enableForwardRenderingCompatibility && !localSpace.GetComponent<ForwardRenderingCompatibility>())
                {
                    ForwardRenderingCompatibility forwardRenderingCompatibility = localSpace.gameObject.AddComponent<ForwardRenderingCompatibility>();
                    forwardRenderingCompatibility.Init(15);
                }

                if (!localSpace.GetComponent<RefreshLegacyAmbient>())
                    localSpace.gameObject.AddComponent<RefreshLegacyAmbient>();
            }

            if (scaledSpace != null)
            {
                if (!scaledSpace.GetComponent<DisableCameraReflectionProbe>())
                    scaledSpace.gameObject.AddComponent<DisableCameraReflectionProbe>();

                if (enableForwardRenderingCompatibility && !scaledSpace.GetComponent<ForwardRenderingCompatibility>())
                {
                    ForwardRenderingCompatibility forwardRenderingCompatibility = scaledSpace.gameObject.AddComponent<ForwardRenderingCompatibility>();
                    forwardRenderingCompatibility.Init(10);

                    Transform parent = forwardRenderingCompatibility.transform;
                    int count = parent.childCount;
                    Transform child;
                    MeshCollider collider;

                    // Thoroughly disable the collider, because Scatterer is not fast enough. Otherwise can get exceptions on the next FixedUpdate when various stock raycasts hit it.
                    for (int i = 0; i < count; i++)
                        if ((child = parent.GetChild(i)).name == "Quad")
                        {
                            if ((collider = child.GetComponent<MeshCollider>()) != null)
                            {
                                collider.enabled = false;
                                collider.gameObject.SetActive(false);
                                Destroy(collider);
                            }
                            break;
                        }
                }

                if (!scaledSpace.GetComponent<RefreshLegacyAmbient>())
                    scaledSpace.gameObject.AddComponent<RefreshLegacyAmbient>();
            }

            // Disable stock camera forward rendering compatibility quads regardless of the setting,
            // otherwise they will mess up the motion vectors and depth buffer on the new camera.
            FlightCamera.fetch.mainCamera.gameObject.GetChild("Quad")?.SetActive(false);
            ScaledCamera.Instance.cam.gameObject.GetChild("Quad")?.SetActive(false);
        }

        protected void OnDestroy()
        {
            CaptureTools.OnSetupCameras -= SetupCameras;

            // Re-enable ForwardRenderingCompatibility to the main camera if it was disabled.
            FlightCamera.fetch.mainCamera.gameObject.GetChild("Quad")?.SetActive(true);
            ScaledCamera.Instance.cam.gameObject.GetChild("Quad")?.SetActive(true);
        }
    }
}
