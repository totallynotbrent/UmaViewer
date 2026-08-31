using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace Gallop
{
    /// <summary>
    /// Debug hair vs skirt physics to find the difference.
    /// </summary>
    public class CySpringHairDebug : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _enableDebug = true;
        [SerializeField] private float _logInterval = 3.0f;
        
        private CySpringController _cySpringController;
        private float _logTimer;
        
        private void Start()
        {
            _cySpringController = GetComponent<CySpringController>();
            if (_cySpringController == null)
            {
                Debug.LogWarning("[CySpringHairDebug] No CySpringController found");
                enabled = false;
                return;
            }
            
            Debug.Log("[CySpringHairDebug] Analyzing hair vs skirt physics...");
        }
        
        private void Update()
        {
            if (!_enableDebug || _cySpringController == null)
                return;
            
            _logTimer += Time.deltaTime;
            if (_logTimer >= _logInterval)
            {
                AnalyzePhysics();
                _logTimer = 0f;
            }
        }
        
        private void AnalyzePhysics()
        {
            Debug.Log("=== HAIR VS SKIRT PHYSICS ANALYSIS ===");
            
            // Check spring rates
            Debug.Log($"SpringRate: {_cySpringController.SpringRate}");
            Debug.Log($"WindPowerRate: {_cySpringController.WindPowerRate}");
            Debug.Log($"AdditionalWindTimeScale: {_cySpringController.AdditionalWindTimeScale}");
            Debug.Log($"GravityRate: {CySpringController.GravityRate}");
            
            // Check collision scales
            Debug.Log("Collision Scales:");
            Debug.Log($"  Head: {_cySpringController.transform.localScale}");
            
            // Check if skirt controller exists
            var skirtController = GetComponent<SkirtController>();
            if (skirtController != null)
            {
                Debug.Log("SkirtController: EXISTS");
                Debug.Log($"  SkirtController.IsEnabled: {skirtController.IsEnableSkirt}");
            }
            else
            {
                Debug.Log("SkirtController: NOT FOUND");
            }
            
            // Get spring array info via reflection
            var springArrayField = typeof(CySpringController).GetField("_springArray",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (springArrayField != null)
            {
                var springArray = springArrayField.GetValue(_cySpringController) as CySpring[];
                if (springArray != null)
                {
                    Debug.Log($"SpringArray length: {springArray.Length}");
                    
                    for (int i = 0; i < springArray.Length; i++)
                    {
                        var spring = springArray[i];
                        if (spring == null) continue;
                        
                        string partName = i == 0 ? "Head(Hair)" : i == 1 ? "Body(Skirt)" : "Tail";
                        Debug.Log($"  [{partName}] StiffnessForceRate: {GetStiffnessRate(spring)}");
                    }
                }
            }
            
            Debug.Log("=== END ANALYSIS ===");
        }
        
        private float GetStiffnessRate(CySpring spring)
        {
            var field = typeof(CySpring).GetField("_stiffnessForceRate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
                return (float)field.GetValue(spring);
            
            return -1f;
        }
    }
}
