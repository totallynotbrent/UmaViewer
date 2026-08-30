using UnityEngine;
using UnityEngine.UI;

namespace Gallop.Live
{
    /// <summary>
    /// Simple physics settings panel for the character viewer.
    /// Add this to your UI Canvas to get physics sliders.
    /// </summary>
    public class PhysicsSettingsPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Slider _hairStiffnessSlider;
        [SerializeField] private Slider _skirtStiffnessSlider;
        [SerializeField] private Slider _tailStiffnessSlider;
        [SerializeField] private Slider _windIntensitySlider;
        [SerializeField] private Slider _collisionScaleSlider;
        [SerializeField] private Button _energeticButton;
        [SerializeField] private Button _gentleButton;
        [SerializeField] private Button _defaultButton;
        [SerializeField] private Text _statusText;
        
        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.P;
        
        private LivePhysicsConfig _config;
        private bool _isVisible;
        
        private void Start()
        {
            // Create config if it doesn't exist
            if (LivePhysicsConfig.Instance == null)
            {
                GameObject configObj = new GameObject("LivePhysicsConfig");
                _config = configObj.AddComponent<LivePhysicsConfig>();
            }
            else
            {
                _config = LivePhysicsConfig.Instance;
            }
            
            SetupUI();
            if (_panel != null)
                _panel.SetActive(false);
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                TogglePanel();
            }
        }
        
        private void SetupUI()
        {
            // Setup sliders
            if (_hairStiffnessSlider != null)
            {
                _hairStiffnessSlider.minValue = 0.1f;
                _hairStiffnessSlider.maxValue = 2.0f;
                _hairStiffnessSlider.value = 0.75f;
                _hairStiffnessSlider.onValueChanged.AddListener(OnHairStiffnessChanged);
            }
            
            if (_skirtStiffnessSlider != null)
            {
                _skirtStiffnessSlider.minValue = 0.1f;
                _skirtStiffnessSlider.maxValue = 2.0f;
                _skirtStiffnessSlider.value = 0.9f;
                _skirtStiffnessSlider.onValueChanged.AddListener(OnSkirtStiffnessChanged);
            }
            
            if (_tailStiffnessSlider != null)
            {
                _tailStiffnessSlider.minValue = 0.1f;
                _tailStiffnessSlider.maxValue = 2.0f;
                _tailStiffnessSlider.value = 0.85f;
                _tailStiffnessSlider.onValueChanged.AddListener(OnTailStiffnessChanged);
            }
            
            if (_windIntensitySlider != null)
            {
                _windIntensitySlider.minValue = 0.0f;
                _windIntensitySlider.maxValue = 1.0f;
                _windIntensitySlider.value = 0.5f;
                _windIntensitySlider.onValueChanged.AddListener(OnWindIntensityChanged);
            }
            
            if (_collisionScaleSlider != null)
            {
                _collisionScaleSlider.minValue = 0.5f;
                _collisionScaleSlider.maxValue = 2.0f;
                _collisionScaleSlider.value = 1.0f;
                _collisionScaleSlider.onValueChanged.AddListener(OnCollisionScaleChanged);
            }
            
            // Setup buttons
            if (_energeticButton != null)
                _energeticButton.onClick.AddListener(OnEnergeticClicked);
            
            if (_gentleButton != null)
                _gentleButton.onClick.AddListener(OnGentleClicked);
            
            if (_defaultButton != null)
                _defaultButton.onClick.AddListener(OnDefaultClicked);
            
            UpdateStatusText("Press P to toggle physics settings");
        }
        
        public void TogglePanel()
        {
            _isVisible = !_isVisible;
            if (_panel != null)
                _panel.SetActive(_isVisible);
        }
        
        private void OnHairStiffnessChanged(float value)
        {
            if (_config != null)
                _config.SetHairStiffness(value);
            UpdateStatusText($"Hair Stiffness: {value:F2}");
        }
        
        private void OnSkirtStiffnessChanged(float value)
        {
            if (_config != null)
                _config.SetSkirtStiffness(value);
            UpdateStatusText($"Skirt Stiffness: {value:F2}");
        }
        
        private void OnTailStiffnessChanged(float value)
        {
            if (_config != null)
                _config.SetTailStiffness(value);
            UpdateStatusText($"Tail Stiffness: {value:F2}");
        }
        
        private void OnWindIntensityChanged(float value)
        {
            if (_config != null)
                _config.SetWindIntensity(value);
            UpdateStatusText($"Wind Intensity: {value:F2}");
        }
        
        private void OnCollisionScaleChanged(float value)
        {
            if (_config != null)
                _config.SetCollisionScale(value);
            UpdateStatusText($"Collision Scale: {value:F2}");
        }
        
        private void OnEnergeticClicked()
        {
            if (_config != null)
            {
                _config.ApplyEnergeticPreset();
                UpdateSlidersFromConfig();
                UpdateStatusText("Applied Energetic preset");
            }
        }
        
        private void OnGentleClicked()
        {
            if (_config != null)
            {
                _config.ApplyGentlePreset();
                UpdateSlidersFromConfig();
                UpdateStatusText("Applied Gentle preset");
            }
        }
        
        private void OnDefaultClicked()
        {
            if (_config != null)
            {
                _config.ApplyDefaultPreset();
                UpdateSlidersFromConfig();
                UpdateStatusText("Applied Default preset");
            }
        }
        
        private void UpdateSlidersFromConfig()
        {
            if (_config == null) return;
            
            if (_hairStiffnessSlider != null)
                _hairStiffnessSlider.value = _config.HairStiffness;
            
            if (_skirtStiffnessSlider != null)
                _skirtStiffnessSlider.value = _config.SkirtStiffness;
            
            if (_tailStiffnessSlider != null)
                _tailStiffnessSlider.value = _config.TailStiffness;
            
            if (_windIntensitySlider != null)
                _windIntensitySlider.value = _config.WindIntensity;
            
            if (_collisionScaleSlider != null)
                _collisionScaleSlider.value = _config.CollisionScale;
        }
        
        private void UpdateStatusText(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }
    }
}
