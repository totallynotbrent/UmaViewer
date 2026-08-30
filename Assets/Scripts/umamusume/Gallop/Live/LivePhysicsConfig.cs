using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Global physics configuration for live concerts.
    /// Automatically applies to all characters when added to StageController.
    /// </summary>
    public class LivePhysicsConfig : MonoBehaviour
    {
        [Header("Hair Physics (Higher = Less Clipping)")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _hairStiffness = 1.2f;      // Higher = less floaty/clipping
        
        [Header("Skirt Physics")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _skirtStiffness = 1.4f;     // Higher = less floaty/clipping
        
        [Header("Tail/Accessories")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _tailStiffness = 1.3f;      // Higher = less floaty/clipping
        
        [Header("Wind")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float _windIntensity = 0.3f;      // Lower = less clipping
        
        [Header("Collision")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _collisionScale = 1.2f;     // Bigger = less penetration
        
        [Header("Performance")]
        [SerializeField] private bool _autoApply = true;            // Apply on start
        [SerializeField] private float _applyDelay = 0.5f;          // Delay before applying
        [SerializeField] private bool _applyToNewCharacters = true; // Auto-apply to new characters
        
        private CySpringController[] _cySpringControllers;
        private float _applyTimer;
        private bool _hasApplied;
        private int _lastControllerCount;
        
        // Singleton for global access
        public static LivePhysicsConfig Instance { get; private set; }
        
        // Public getters
        public float HairStiffness => _hairStiffness;
        public float SkirtStiffness => _skirtStiffness;
        public float TailStiffness => _tailStiffness;
        public float WindIntensity => _windIntensity;
        public float CollisionScale => _collisionScale;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            if (_autoApply)
            {
                _applyTimer = _applyDelay;
                _hasApplied = false;
            }
        }
        
        private void Update()
        {
            // Initial apply after delay
            if (!_hasApplied && _autoApply)
            {
                _applyTimer -= Time.deltaTime;
                if (_applyTimer <= 0)
                {
                    ApplyPhysicsSettings();
                    _hasApplied = true;
                    _lastControllerCount = _cySpringControllers?.Length ?? 0;
                }
                return;
            }
            
            // Auto-apply to new characters
            if (_applyToNewCharacters && _hasApplied)
            {
                var currentControllers = FindObjectsOfType<CySpringController>();
                if (currentControllers.Length != _lastControllerCount)
                {
                    Debug.Log($"[LivePhysicsConfig] Detected {currentControllers.Length - _lastControllerCount} new controllers, applying settings");
                    _cySpringControllers = currentControllers;
                    ApplyPhysicsSettings();
                    _lastControllerCount = currentControllers.Length;
                }
            }
        }
        
        /// <summary>
        /// Apply all physics settings to all CySpring controllers
        /// </summary>
        public void ApplyPhysicsSettings()
        {
            _cySpringControllers = FindObjectsOfType<CySpringController>();
            Debug.Log($"[LivePhysicsConfig] Applying settings to {_cySpringControllers.Length} controllers");
            
            foreach (var controller in _cySpringControllers)
            {
                ApplyToController(controller);
            }
        }
        
        /// <summary>
        /// Apply settings to a specific controller
        /// </summary>
        public void ApplyToController(CySpringController controller)
        {
            if (controller == null) return;
            
            // Apply hair stiffness (Head part)
            controller.SetPartsSpringRate(CySpringController.Parts.Head, _hairStiffness);
            
            // Apply skirt stiffness (Body part)
            controller.SetPartsSpringRate(CySpringController.Parts.Body, _skirtStiffness);
            
            // Apply tail/accessory stiffness (Tail part)
            controller.SetPartsSpringRate(CySpringController.Parts.Tail, _tailStiffness);
            
            // Apply wind intensity
            controller.AdditionalWindTimeScale = _windIntensity;
            
            // Apply collision scale
            try
            {
                if (_collisionScale != 1.0f)
                {
                    controller.SetScale(CySpringController.Parts.Head, _collisionScale);
                    controller.SetScale(CySpringController.Parts.Body, _collisionScale);
                    controller.SetScale(CySpringController.Parts.Tail, _collisionScale);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LivePhysicsConfig] Failed to set collision scale: {e.Message}");
            }
            
            Debug.Log($"[LivePhysicsConfig] Applied to: {controller.gameObject.name} " +
                     $"hair={_hairStiffness} skirt={_skirtStiffness} tail={_tailStiffness} " +
                     $"wind={_windIntensity} collision={_collisionScale}");
        }
        
        /// <summary>
        /// Set hair stiffness globally
        /// </summary>
        public void SetHairStiffness(float value)
        {
            _hairStiffness = Mathf.Clamp(value, 0.1f, 2.0f);
            ApplyPhysicsSettings();
        }
        
        /// <summary>
        /// Set skirt stiffness globally
        /// </summary>
        public void SetSkirtStiffness(float value)
        {
            _skirtStiffness = Mathf.Clamp(value, 0.1f, 2.0f);
            ApplyPhysicsSettings();
        }
        
        /// <summary>
        /// Set tail stiffness globally
        /// </summary>
        public void SetTailStiffness(float value)
        {
            _tailStiffness = Mathf.Clamp(value, 0.1f, 2.0f);
            ApplyPhysicsSettings();
        }
        
        /// <summary>
        /// Set wind intensity globally
        /// </summary>
        public void SetWindIntensity(float value)
        {
            _windIntensity = Mathf.Clamp(value, 0.0f, 1.0f);
            ApplyPhysicsSettings();
        }
        
        /// <summary>
        /// Set collision scale globally
        /// </summary>
        public void SetCollisionScale(float value)
        {
            _collisionScale = Mathf.Clamp(value, 0.5f, 2.0f);
            ApplyPhysicsSettings();
        }
        
        // Preset methods
        public void ApplyEnergeticPreset()
        {
            _hairStiffness = 1.2f;
            _skirtStiffness = 1.4f;
            _tailStiffness = 1.3f;
            _windIntensity = 0.7f;
            _collisionScale = 1.1f;
            ApplyPhysicsSettings();
        }
        
        public void ApplyGentlePreset()
        {
            _hairStiffness = 0.6f;
            _skirtStiffness = 0.7f;
            _tailStiffness = 0.65f;
            _windIntensity = 0.3f;
            _collisionScale = 0.9f;
            ApplyPhysicsSettings();
        }
        
        public void ApplyDefaultPreset()
        {
            _hairStiffness = 1.2f;
            _skirtStiffness = 1.4f;
            _tailStiffness = 1.3f;
            _windIntensity = 0.3f;
            _collisionScale = 1.2f;
            ApplyPhysicsSettings();
        }
        
        /// <summary>
        /// Reset to default values
        /// </summary>
        public void ResetToDefaults()
        {
            ApplyDefaultPreset();
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
