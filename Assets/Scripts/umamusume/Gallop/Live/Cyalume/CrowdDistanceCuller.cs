using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.Live.Cyalume
{
    /// <summary>
    /// Buckets crowd/audience renderers by bounds center distance to main camera
    /// and disables renderer.enabled beyond threshold. Keeps stadium look close,
    /// culls distant crowd to reduce 30k -> ~3k batches. Re-evaluates every frame
    /// via frustum planes. Shadow casting is disabled for all crowd renderers after instantiate.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class CrowdDistanceCuller : MonoBehaviour
    {
        [SerializeField] private float cullDistance = 80f;
        [SerializeField] private int frameInterval = 1;

        private List<Renderer> crowdRenderers = new List<Renderer>(512);
        private Transform cameraTransform;
        private Camera mainCamera;
        private Plane[] frustumPlanes = new Plane[6];
        private int frameCounter;
        private float sqrCullDistance;

        public void Initialize(IEnumerable<Renderer> renderers, Camera camera = null)
        {
            crowdRenderers.Clear();
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    crowdRenderers.Add(r);
                    // Keep close look: disable shadow casting for crowd to save shadow map passes
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            sqrCullDistance = cullDistance * cullDistance;
            mainCamera = camera != null ? camera : Camera.main;
            if (mainCamera != null) cameraTransform = mainCamera.transform;
            frameCounter = 0;
        }

        public void SetRenderers(IEnumerable<Renderer> renderers, Camera camera = null)
        {
            Initialize(renderers, camera);
        }

        void LateUpdate()
        {
            if (crowdRenderers.Count == 0) return;

            // Try to refresh camera reference if missing (e.g., after scene load)
            if (mainCamera == null || cameraTransform == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null && Gallop.Live.Director.instance != null && Gallop.Live.Director.instance.MainCameraTransform != null)
                {
                    cameraTransform = Gallop.Live.Director.instance.MainCameraTransform;
                    // Find camera from transform
                    mainCamera = cameraTransform.GetComponent<Camera>();
                    if (mainCamera == null) mainCamera = cameraTransform.GetComponentInChildren<Camera>();
                }
                else if (mainCamera != null)
                {
                    cameraTransform = mainCamera.transform;
                }
                if (mainCamera == null || cameraTransform == null) return;
            }

            frameCounter++;
            if ((frameCounter % frameInterval) != 0) return;

            Vector3 camPos = cameraTransform.position;
            // Update frustum planes for culling
            GeometryUtility.CalculateFrustumPlanes(mainCamera, frustumPlanes);

            for (int i = 0; i < crowdRenderers.Count; i++)
            {
                var r = crowdRenderers[i];
                if (r == null) continue;

                Bounds bounds = r.bounds;
                Vector3 center = bounds.center;

                // Frustum check first (cheaper than distance for off-screen)
                // Use bounds for accurate frustum test
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                {
                    // Outside frustum: disable
                    if (r.enabled) r.enabled = false;
                    continue;
                }

                // Distance check
                float sqrDist = (center - camPos).sqrMagnitude;
                bool shouldBeEnabled = sqrDist <= sqrCullDistance;
                if (r.enabled != shouldBeEnabled)
                    r.enabled = shouldBeEnabled;
            }
        }

        public void SetCullDistance(float distance)
        {
            cullDistance = distance;
            sqrCullDistance = cullDistance * cullDistance;
        }

        public bool ShouldBeEnabled(Renderer r)
        {
            if (r == null) return false;
            if (mainCamera == null || cameraTransform == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null && Gallop.Live.Director.instance != null && Gallop.Live.Director.instance.MainCameraTransform != null)
                {
                    cameraTransform = Gallop.Live.Director.instance.MainCameraTransform;
                    mainCamera = cameraTransform.GetComponent<Camera>();
                    if (mainCamera == null) mainCamera = cameraTransform.GetComponentInChildren<Camera>();
                }
                else if (mainCamera != null) cameraTransform = mainCamera.transform;
                if (mainCamera == null || cameraTransform == null) return r.enabled;
                GeometryUtility.CalculateFrustumPlanes(mainCamera, frustumPlanes);
            }
            Bounds bounds = r.bounds;
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return false;
            float sqrDist = (bounds.center - cameraTransform.position).sqrMagnitude;
            return sqrDist <= sqrCullDistance;
        }

        public void Refresh()
        {
            frameCounter = frameInterval - 1;
        }
    }
}
