using UnityEngine;
using System.Collections.Generic;

namespace Gallop
{
    /// <summary>
    /// Visual debug - draws lines showing bone movement direction.
    /// </summary>
    public class CySpringVisualDebug : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _enableVisual = true;
        [SerializeField] private float _lineLength = 0.1f;
        [SerializeField] private Color _hairColor = Color.cyan;
        [SerializeField] private Color _skirtColor = Color.magenta;
        
        private Dictionary<Transform, Vector3> _lastPositions = new Dictionary<Transform, Vector3>();
        
        private void LateUpdate()
        {
            if (!_enableVisual)
                return;
            
            Transform[] allTransforms = GetComponentsInChildren<Transform>();
            
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                
                string name = t.name.ToLower();
                bool isHair = name.Contains("hair") || name.Contains("ponytail");
                bool isSkirt = name.Contains("skirt") || name.Contains("cloth");
                
                if (isHair || isSkirt)
                {
                    Vector3 currentPos = t.position;
                    
                    if (_lastPositions.TryGetValue(t, out Vector3 lastPos))
                    {
                        Vector3 movement = currentPos - lastPos;
                        
                        if (movement.sqrMagnitude > 0.00001f)
                        {
                            Color color = isHair ? _hairColor : _skirtColor;
                            Debug.DrawRay(currentPos, movement.normalized * _lineLength, color);
                        }
                    }
                    
                    _lastPositions[t] = currentPos;
                }
            }
        }
    }
}
