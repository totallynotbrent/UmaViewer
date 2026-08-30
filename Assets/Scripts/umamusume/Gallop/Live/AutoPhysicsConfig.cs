using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Auto-creates LivePhysicsConfig and applies better physics settings.
    /// Add this to ViewerMain or any persistent object.
    /// </summary>
    public class AutoPhysicsConfig : MonoBehaviour
    {
        [Header("Hair Physics (Higher = Less Clipping)")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _hairStiffness = 1.2f;      // Much higher to prevent clipping
        
        [Header("Skirt Physics")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _skirtStiffness = 1.4f;     // Higher to prevent clipping
        
        [Header("Tail/Accessories")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _tailStiffness = 1.3f;      // Higher to prevent clipping
        
        [Header("Wind")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float _windIntensity = 0.3f;      // Lower wind to reduce clipping
        
        [Header("Collision")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _collisionScale = 1.2f;     // Bigger collisions to prevent penetration
        
        private LivePhysicsConfig _config;
        
        private void Awake()
        {
            // Create LivePhysicsConfig if it doesn't exist
            if (LivePhysicsConfig.Instance == null)
            {
                GameObject configObj = new GameObject("LivePhysicsConfig");
                _config = configObj.AddComponent<LivePhysicsConfig>();
                DontDestroyOnLoad(configObj);
                
                // Apply our settings
                _config.SetHairStiffness(_hairStiffness);
                _config.SetSkirtStiffness(_skirtStiffness);
                _config.SetTailStiffness(_tailStiffness);
                _config.SetWindIntensity(_windIntensity);
                _config.SetCollisionScale(_collisionScale);
                
                Debug.Log("[AutoPhysicsConfig] Created LivePhysicsConfig with anti-clipping settings");
            }
            else
            {
                _config = LivePhysicsConfig.Instance;
            }
        }
        
        private void Update()
        {
            // Press P to toggle physics settings info
            if (Input.GetKeyDown(KeyCode.P))
            {
                Debug.Log("[AutoPhysicsConfig] Current Settings:");
                Debug.Log($"  Hair Stiffness: {_config.HairStiffness} (higher = less clipping)");
                Debug.Log($"  Skirt Stiffness: {_config.SkirtStiffness}");
                Debug.Log($"  Tail Stiffness: {_config.TailStiffness}");
                Debug.Log($"  Wind Intensity: {_config.WindIntensity}");
                Debug.Log($"  Collision Scale: {_config.CollisionScale} (bigger = less penetration)");
                Debug.Log("Press 1=Energetic, 2=Gentle, 3=Default");
            }
            
            // Quick preset keys
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _config.ApplyEnergeticPreset();
                Debug.Log("[AutoPhysicsConfig] Applied Energetic preset");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _config.ApplyGentlePreset();
                Debug.Log("[AutoPhysicsConfig] Applied Gentle preset");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                _config.ApplyDefaultPreset();
                Debug.Log("[AutoPhysicsConfig] Applied Default preset");
            }
        }
    }
}
