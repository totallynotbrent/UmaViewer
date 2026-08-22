using UnityEngine;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Viewer-side replacement for the missing CustomProjector script.
    /// The original script controls projector rendering for stage effects.
    /// This simplified version applies basic projector properties.
    /// </summary>
    [DisallowMultipleComponent]
    public class CustomProjectorViewer : MonoBehaviour
    {
        [Header("Projector Settings")]
        [SerializeField] private float _intensity = 1.0f;
        [SerializeField] private Color _projectorColor = Color.white;
        [SerializeField] private float _fieldOfView = 30f;
        [SerializeField] private float _nearClip = 0.1f;
        [SerializeField] private float _farClip = 10f;
        [SerializeField] private Texture2D _projectorTexture;

        private Projector _projector;
        private MaterialPropertyBlock _mpb;
        private Renderer _renderer;
        private static readonly int PID_Color = Shader.PropertyToID("_Color");
        private static readonly int PID_Intensity = Shader.PropertyToID("_Intensity");

        private void Awake()
        {
            _projector = GetComponent<Projector>();
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            if (_projector != null)
            {
                _projector.fieldOfView = _fieldOfView;
                _projector.nearClipPlane = _nearClip;
                _projector.farClipPlane = _farClip;
                _projector.orthographic = false;

                if (_projectorTexture != null)
                    _projector.material.mainTexture = _projectorTexture;
            }
        }

        private void LateUpdate()
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(PID_Color, _projectorColor);
                _mpb.SetFloat(PID_Intensity, _intensity);
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        public void SetIntensity(float intensity)
        {
            _intensity = intensity;
        }

        public void SetColor(Color color)
        {
            _projectorColor = color;
        }

        public void SetTexture(Texture2D texture)
        {
            _projectorTexture = texture;
            if (_projector != null && _projector.material != null)
                _projector.material.mainTexture = texture;
        }
    }
}
