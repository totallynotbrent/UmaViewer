using UnityEngine;
using System.Collections.Generic;

namespace Gallop.Live
{
    /// <summary>
    /// Hides billboard crowd objects that appear as white rectangles.
    /// These are typically audience billboards that need proper textures/shaders.
    /// This script can be toggled to hide or show them.
    /// </summary>
    public class StageBillboardCleanup : MonoBehaviour
    {
        [Header("Billboard Cleanup Settings")]
        [SerializeField] private bool _hideWhiteBillboards = true;
        [SerializeField] private bool _verboseLog = true;

        private List<GameObject> _billboardObjects = new List<GameObject>();
        private bool _cleanedUp;

        private void Start()
        {
            if (_hideWhiteBillboards)
                CleanUpWhiteBillboards();
        }

        private void Update()
        {
        Gallop.Live.SectionProfiler.Begin("script.StageBillboardCleanup");
            // Re-check if new objects appear
            if (!_cleanedUp && _hideWhiteBillboards)
                CleanUpWhiteBillboards();

        Gallop.Live.SectionProfiler.End();        }

        public void CleanUpWhiteBillboards()
        {
            _billboardObjects.Clear();

            // Find all billboard objects
            var allObjects = FindObjectsOfType<GameObject>(true);
            foreach (var obj in allObjects)
            {
                if (obj == null) continue;

                string name = obj.name ?? "";
                if (name.IndexOf("billboard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("audience", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("crowd", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Check if this object has a renderer with white/missing texture
                    var renderers = obj.GetComponentsInChildren<Renderer>(true);
                    bool isWhite = false;

                    foreach (var renderer in renderers)
                    {
                        if (renderer == null) continue;

                        var materials = renderer.sharedMaterials;
                        foreach (var mat in materials)
                        {
                            if (mat == null) continue;

                            // Check if material has a white or missing main texture
                            if (mat.HasProperty("_MainTex"))
                            {
                                var tex = mat.GetTexture("_MainTex");
                                if (tex == null)
                                {
                                    isWhite = true;
                                    break;
                                }
                            }

                            // Check if material color is white
                            if (mat.HasProperty("_Color"))
                            {
                                var color = mat.GetColor("_Color");
                                if (color.r > 0.9f && color.g > 0.9f && color.b > 0.9f && color.a > 0.9f)
                                {
                                    isWhite = true;
                                    break;
                                }
                            }
                        }

                        if (isWhite) break;
                    }

                    if (isWhite)
                    {
                        _billboardObjects.Add(obj);
                        if (_verboseLog)
                            Debug.Log($"[StageBillboardCleanup] Found white billboard: {name}");

                        // Hide the object
                        obj.SetActive(false);
                    }
                }
            }

            _cleanedUp = true;

            if (_verboseLog)
                Debug.Log($"[StageBillboardCleanup] Cleaned up {_billboardObjects.Count} white billboard objects.");
        }

        public void ShowBillboards()
        {
            foreach (var obj in _billboardObjects)
            {
                if (obj != null)
                    obj.SetActive(true);
            }

            if (_verboseLog)
                Debug.Log($"[StageBillboardCleanup] Showed {_billboardObjects.Count} billboard objects.");
        }

        public void HideBillboards()
        {
            foreach (var obj in _billboardObjects)
            {
                if (obj != null)
                    obj.SetActive(false);
            }

            if (_verboseLog)
                Debug.Log($"[StageBillboardCleanup] Hid {_billboardObjects.Count} billboard objects.");
        }
    }
}
