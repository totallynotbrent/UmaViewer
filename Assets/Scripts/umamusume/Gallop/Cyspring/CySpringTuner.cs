using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Tunes CySpring physics parameters for skirt and hair.
    /// Attach to the Live root or a global manager object.
    /// </summary>
    public class CySpringTuner : MonoBehaviour
    {
        [Header("Spring Physics")]
        [SerializeField] private float _springRate = 1.0f;           // Overall spring rate
        [SerializeField] private float _stiffnessRate = 1.0f;        // Stiffness (higher = less floaty)
        [SerializeField] private float _windPowerRate = 1.0f;        // Wind influence
        [SerializeField] private float _timeScale = 1.0f;            // Time scale for physics
        
        [Header("Collision Settings")]
        [SerializeField] private float _collisionScaleMultiplier = 1.0f;  // Scale all collision radii
        [SerializeField] private bool _enableCollisionAdjustments = true;
        
        [Header("Performance")]
        [SerializeField] private bool _autoTuneOnStart = true;       // Auto-tune when live loads
        [SerializeField] private float _tuneDelay = 0.5f;            // Delay before tuning (seconds)
        
        private CySpringController[] _cySpringControllers;
        private float _tuneTimer;
        private bool _hasTuned;
        
        private void Start()
        {
            if (_autoTuneOnStart)
            {
                _tuneTimer = _tuneDelay;
                _hasTuned = false;
            }
        }
        
        private void Update()
        {
            if (_hasTuned || !_autoTuneOnStart)
                return;
            
            _tuneTimer -= Time.deltaTime;
            if (_tuneTimer <= 0)
            {
                TuneAllPhysics();
                _hasTuned = true;
            }
        }
        
        /// <summary>
        /// Find and tune all CySpring controllers in the scene
        /// </summary>
        public void TuneAllPhysics()
        {
            _cySpringControllers = FindObjectsOfType<CySpringController>();
            Debug.Log($"[CySpringTuner] Found {_cySpringControllers.Length} CySpring controllers");
            
            foreach (var controller in _cySpringControllers)
            {
                TuneController(controller);
            }
        }
        
        /// <summary>
        /// Tune a single CySpring controller
        /// </summary>
        private void TuneController(CySpringController controller)
        {
            if (controller == null) return;
            
            // Apply spring rate (affects how strong the springs are)
            controller.SpringRate = _springRate;
            
            // Apply stiffness rate (higher = less floaty, more rigid)
            controller.StiffnessRate = _stiffnessRate;
            
            // Apply wind power rate
            controller.WindPowerRate = _windPowerRate;
            
            // Apply time scale
            controller.TimeScale = _timeScale;
            
            // Apply collision adjustments if enabled
            if (_enableCollisionAdjustments && _collisionScaleMultiplier != 1.0f)
            {
                AdjustCollisionScale(controller, _collisionScaleMultiplier);
            }
            
            Debug.Log($"[CySpringTuner] Tuned: {controller.gameObject.name} " +
                     $"springRate={_springRate} stiffness={_stiffnessRate} " +
                     $"wind={_windPowerRate} timeScale={_timeScale} " +
                     $"collisionScale={_collisionScaleMultiplier}");
        }
        
        /// <summary>
        /// Adjust collision scale for a controller
        /// </summary>
        private void AdjustCollisionScale(CySpringController controller, float scale)
        {
            // Set collision scale for all parts
            try
            {
                controller.SetScale(CySpringController.Parts.Head, scale);
                controller.SetScale(CySpringController.Parts.Body, scale);
                controller.SetScale(CySpringController.Parts.Tail, scale);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CySpringTuner] Failed to adjust collision scale: {e.Message}");
            }
        }
        
        /// <summary>
        /// Reset to default physics settings
        /// </summary>
        public void ResetToDefaults()
        {
            _springRate = 1.0f;
            _stiffnessRate = 1.0f;
            _windPowerRate = 1.0f;
            _timeScale = 1.0f;
            _collisionScaleMultiplier = 1.0f;
            
            TuneAllPhysics();
        }
        
        /// <summary>
        /// Apply preset physics settings for different live types
        /// </summary>
        public void ApplyPreset(string presetName)
        {
            switch (presetName.ToLower())
            {
                case "energetic":
                    // Fast, responsive physics for upbeat songs
                    _springRate = 1.2f;
                    _stiffnessRate = 1.4f;
                    _windPowerRate = 1.3f;
                    _timeScale = 1.1f;
                    _collisionScaleMultiplier = 1.1f;
                    break;
                    
                case "gentle":
                    // Softer, more flowing physics for ballads
                    _springRate = 0.9f;
                    _stiffnessRate = 0.8f;
                    _windPowerRate = 0.7f;
                    _timeScale = 0.95f;
                    _collisionScaleMultiplier = 0.9f;
                    break;
                    
                case "default":
                default:
                    ResetToDefaults();
                    break;
            }
            
            TuneAllPhysics();
            Debug.Log($"[CySpringTuner] Applied preset: {presetName}");
        }
        
        /// <summary>
        /// Reduce floatiness by increasing stiffness
        /// </summary>
        public void ReduceFloatiness(float amount = 0.3f)
        {
            _stiffnessRate = Mathf.Clamp(_stiffnessRate + amount, 0.5f, 3.0f);
            TuneAllPhysics();
        }
        
        /// <summary>
        /// Reduce collision issues by adjusting spring rate and collision scale
        /// </summary>
        public void FixCollision(float amount = 0.2f)
        {
            _springRate = Mathf.Clamp(_springRate - amount, 0.3f, 2.0f);
            _collisionScaleMultiplier = Mathf.Clamp(_collisionScaleMultiplier - amount * 0.5f, 0.5f, 2.0f);
            TuneAllPhysics();
        }
        
        /// <summary>
        /// Increase collision radius to prevent penetration
        /// </summary>
        public void IncreaseCollisionRadius(float amount = 0.1f)
        {
            _collisionScaleMultiplier = Mathf.Clamp(_collisionScaleMultiplier + amount, 0.5f, 3.0f);
            TuneAllPhysics();
        }
        
        /// <summary>
        /// Decrease collision radius for tighter fitting
        /// </summary>
        public void DecreaseCollisionRadius(float amount = 0.1f)
        {
            _collisionScaleMultiplier = Mathf.Clamp(_collisionScaleMultiplier - amount, 0.3f, 2.0f);
            TuneAllPhysics();
        }
        
        // Preset methods for easy access
        public void ApplyEnergeticPreset() => ApplyPreset("energetic");
        public void ApplyGentlePreset() => ApplyPreset("gentle");
        public void ApplyDefaultPreset() => ApplyPreset("default");
    }
}
