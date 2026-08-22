using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Viewer-side replacement for the missing LightProjection script.
    /// The original script projects light patterns onto surfaces.
    /// This simplified version applies basic light projection properties.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightProjectionViewer : MonoBehaviour
    {
        [Header("Light Projection Settings")]
        [SerializeField] private float _intensity = 1.0f;
        [SerializeField] private Color _lightColor = Color.white;
        [SerializeField] private float _range = 10f;
        [SerializeField] private float _spotAngle = 30f;

        private Light _light;
        private MaterialPropertyBlock _mpb;
        private Renderer _renderer;
        private static readonly int PID_LightColor = Shader.PropertyToID("_LightColor");
        private static readonly int PID_Intensity = Shader.PropertyToID("_Intensity");

        private void Awake()
        {
            _light = GetComponent<Light>();
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            if (_light != null)
            {
                _light.type = LightType.Spot;
                _light.range = _range;
                _light.spotAngle = _spotAngle;
                _light.color = _lightColor;
                _light.intensity = _intensity;
            }
        }

        private void LateUpdate()
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(PID_LightColor, _lightColor);
                _mpb.SetFloat(PID_Intensity, _intensity);
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        public void SetIntensity(float intensity)
        {
            _intensity = intensity;
            if (_light != null)
                _light.intensity = intensity;
        }

        public void SetColor(Color color)
        {
            _lightColor = color;
            if (_light != null)
                _light.color = color;
        }
    }
}
