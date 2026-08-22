using UnityEngine;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Viewer-side replacement for the missing CustomLensFlare script.
    /// The original script controls lens flare rendering based on light direction.
    /// This simplified version applies a basic lens flare effect.
    /// </summary>
    [DisallowMultipleComponent]
    public class CustomLensFlareViewer : MonoBehaviour
    {
        [Header("Lens Flare Settings")]
        [SerializeField] private float _intensity = 1.0f;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private float _size = 1.0f; // Used for lens flare scaling

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int PID_Color = Shader.PropertyToID("_Color");
        private static readonly int PID_Intensity = Shader.PropertyToID("_Intensity");

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (_renderer == null)
                return;

            // Apply lens flare properties
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(PID_Color, _color);
            _mpb.SetFloat(PID_Intensity, _intensity);
            _renderer.SetPropertyBlock(_mpb);

            // Billboard effect - face the camera
            if (Camera.main != null)
            {
                transform.forward = Camera.main.transform.forward;
            }
        }

        public void SetIntensity(float intensity)
        {
            _intensity = intensity;
        }

        public void SetColor(Color color)
        {
            _color = color;
        }
    }
}
