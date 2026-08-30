using UnityEngine;
using UnityEngine.UI;

namespace Gallop.Live
{
    /// <summary>
    /// Runtime UI for controlling CySpring physics settings.
    /// Attach to a Canvas with the UI elements.
    /// </summary>
    public class LivePhysicsUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Slider _springRateSlider;
        [SerializeField] private Slider _stiffnessSlider;
        [SerializeField] private Slider _windPowerSlider;
        [SerializeField] private Slider _timeScaleSlider;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _energeticButton;
        [SerializeField] private Button _gentleButton;
        [SerializeField] private Text _statusText;
        
        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.P;
        
        private CySpringTuner _tuner;
        private bool _isVisible;
        
        private void Start()
        {
            _tuner = FindObjectOfType<CySpringTuner>();
            if (_tuner == null)
            {
                Debug.LogWarning("[LivePhysicsUI] No CySpringTuner found in scene");
                return;
            }
            
            SetupUI();
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
            if (_springRateSlider != null)
            {
                _springRateSlider.minValue = 0.1f;
                _springRateSlider.maxValue = 3.0f;
                _springRateSlider.value = 1.0f;
                _springRateSlider.onValueChanged.AddListener(OnSpringRateChanged);
            }
            
            if (_stiffnessSlider != null)
            {
                _stiffnessSlider.minValue = 0.1f;
                _stiffnessSlider.maxValue = 3.0f;
                _stiffnessSlider.value = 1.0f;
                _stiffnessSlider.onValueChanged.AddListener(OnStiffnessChanged);
            }
            
            if (_windPowerSlider != null)
            {
                _windPowerSlider.minValue = 0.0f;
                _windPowerSlider.maxValue = 3.0f;
                _windPowerSlider.value = 1.0f;
                _windPowerSlider.onValueChanged.AddListener(OnWindPowerChanged);
            }
            
            if (_timeScaleSlider != null)
            {
                _timeScaleSlider.minValue = 0.1f;
                _timeScaleSlider.maxValue = 2.0f;
                _timeScaleSlider.value = 1.0f;
                _timeScaleSlider.onValueChanged.AddListener(OnTimeScaleChanged);
            }
            
            // Setup buttons
            if (_resetButton != null)
                _resetButton.onClick.AddListener(OnResetClicked);
            
            if (_energeticButton != null)
                _energeticButton.onClick.AddListener(OnEnergeticClicked);
            
            if (_gentleButton != null)
                _gentleButton.onClick.AddListener(OnGentleClicked);
            
            UpdateStatusText();
        }
        
        public void TogglePanel()
        {
            _isVisible = !_isVisible;
            _panel.SetActive(_isVisible);
        }
        
        private void OnSpringRateChanged(float value)
        {
            if (_tuner != null)
            {
                // Access private field via reflection or make it public
                // For now, just log the value
                Debug.Log($"[LivePhysicsUI] Spring Rate: {value:F2}");
            }
        }
        
        private void OnStiffnessChanged(float value)
        {
            if (_tuner != null)
            {
                Debug.Log($"[LivePhysicsUI] Stiffness: {value:F2}");
            }
        }
        
        private void OnWindPowerChanged(float value)
        {
            if (_tuner != null)
            {
                Debug.Log($"[LivePhysicsUI] Wind Power: {value:F2}");
            }
        }
        
        private void OnTimeScaleChanged(float value)
        {
            if (_tuner != null)
            {
                Debug.Log($"[LivePhysicsUI] Time Scale: {value:F2}");
            }
        }
        
        private void OnResetClicked()
        {
            if (_tuner != null)
            {
                _tuner.ResetToDefaults();
                UpdateSlidersFromTuner();
                UpdateStatusText("Reset to defaults");
            }
        }
        
        private void OnEnergeticClicked()
        {
            if (_tuner != null)
            {
                _tuner.ApplyEnergeticPreset();
                UpdateSlidersFromTuner();
                UpdateStatusText("Applied energetic preset");
            }
        }
        
        private void OnGentleClicked()
        {
            if (_tuner != null)
            {
                _tuner.ApplyGentlePreset();
                UpdateSlidersFromTuner();
                UpdateStatusText("Applied gentle preset");
            }
        }
        
        private void UpdateSlidersFromTuner()
        {
            // This would update sliders from tuner values
            // For now, just update status
            UpdateStatusText();
        }
        
        private void UpdateStatusText(string message = null)
        {
            if (_statusText != null)
            {
                _statusText.text = message ?? "Ready - Press P to toggle";
            }
        }
    }
}
