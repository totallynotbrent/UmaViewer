using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Viewer-side replacement for the missing BillboardController script.
    /// The original script makes objects face the camera (billboard effect).
    /// This simplified version applies a basic billboard effect.
    /// </summary>
    [DisallowMultipleComponent]
    public class BillboardControllerViewer : MonoBehaviour
    {
        [Header("Billboard Settings")]
        [SerializeField] private bool _lockXRotation = false;
        [SerializeField] private bool _lockYRotation = true;
        [SerializeField] private bool _lockZRotation = false;
        [SerializeField] private float _distanceFromCamera = 10f; // Used for billboard positioning

        private Transform _cameraTransform;

        private void Start()
        {
            _cameraTransform = Camera.main?.transform;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
            {
                _cameraTransform = Camera.main?.transform;
                if (_cameraTransform == null)
                    return;
            }

            // Calculate direction to camera
            Vector3 direction = _cameraTransform.position - transform.position;

            // Create rotation that faces the camera
            Quaternion targetRotation = Quaternion.LookRotation(-direction);

            // Apply locked axes
            Vector3 euler = targetRotation.eulerAngles;
            if (_lockXRotation) euler.x = transform.eulerAngles.x;
            if (_lockYRotation) euler.y = transform.eulerAngles.y;
            if (_lockZRotation) euler.z = transform.eulerAngles.z;

            transform.rotation = Quaternion.Euler(euler);
        }

        public void SetCamera(Transform camera)
        {
            _cameraTransform = camera;
        }
    }
}
