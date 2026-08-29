using UnityEngine;
using System.Collections.Generic;

namespace Gallop.Live
{
    /// <summary>
    /// Finds the specific large white rectangle blocking the view.
    /// Only logs objects that are large and white.
    /// </summary>
    public class StageFindWhiteRectangles : MonoBehaviour
    {
        [Header("Find Settings")]
        [SerializeField] private bool _findOnStart = true;
        [SerializeField] private float _minSize = 5f; // Minimum size to be considered

        private void Start()
        {
            if (_findOnStart)
                FindWhiteRectangles();
        }

        public void FindWhiteRectangles()
        {
            var allObjects = FindObjectsOfType<GameObject>(true);
            int foundCount = 0;

            Debug.Log("=== Finding Large White Rectangle Objects ===");

            foreach (var obj in allObjects)
            {
                if (obj == null) continue;

                string name = obj.name ?? "";

                // Skip very small objects
                var renderers = obj.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;

                    // Check if this renderer is large
                    var bounds = renderer.bounds;
                    float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

                    if (size < _minSize) continue;

                    // Check if material is white
                    var materials = renderer.sharedMaterials;
                    foreach (var mat in materials)
                    {
                        if (mat == null) continue;

                        if (mat.HasProperty("_Color"))
                        {
                            var color = mat.GetColor("_Color");
                            if (color.r > 0.8f && color.g > 0.8f && color.b > 0.8f && color.a > 0.8f)
                            {
                                Debug.Log($"[WhiteRect] Large white object: {name} size={size:F1} pos={obj.transform.position} bounds={bounds.size}");
                                foundCount++;
                                break;
                            }
                        }

                        // Check for no texture
                        if (mat.HasProperty("_MainTex"))
                        {
                            var tex = mat.GetTexture("_MainTex");
                            if (tex == null && size > _minSize)
                            {
                                Debug.Log($"[WhiteRect] Large no-texture object: {name} size={size:F1} pos={obj.transform.position}");
                                foundCount++;
                                break;
                            }
                        }
                    }
                }
            }

            Debug.Log($"=== Found {foundCount} large white rectangle objects ===");
        }
    }
}
