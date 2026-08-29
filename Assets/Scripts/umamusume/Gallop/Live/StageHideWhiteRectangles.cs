using UnityEngine;
using System.Collections.Generic;

namespace Gallop.Live
{
    /// <summary>
    /// Hides cyalume d003 front and back objects that appear as white rectangles.
    /// </summary>
    public class StageHideWhiteRectangles : MonoBehaviour
    {
        [Header("Hide Settings")]
        [SerializeField] private bool _hideOnStart = true;
        [SerializeField] private bool _verboseLog = true;

        private List<GameObject> _hiddenObjects = new List<GameObject>();

        private void Start()
        {
            if (_hideOnStart)
                HideWhiteRectangles();
        }

        public void HideWhiteRectangles()
        {
            _hiddenObjects.Clear();

            var allObjects = FindObjectsOfType<GameObject>(true);
            int hiddenCount = 0;

            foreach (var obj in allObjects)
            {
                if (obj == null) continue;

                string name = obj.name ?? "";

                // Hide cyalume d003 front and back objects
                if (name.IndexOf("d003", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("front", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("back", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    if (obj.activeSelf)
                    {
                        obj.SetActive(false);
                        _hiddenObjects.Add(obj);
                        hiddenCount++;

                        if (_verboseLog)
                            Debug.Log($"[StageHideWhiteRectangles] Hid: {name}");
                    }
                }
            }

            if (_verboseLog)
                Debug.Log($"[StageHideWhiteRectangles] Hid {hiddenCount} objects.");
        }

        public void ShowWhiteRectangles()
        {
            foreach (var obj in _hiddenObjects)
            {
                if (obj != null)
                    obj.SetActive(true);
            }
            _hiddenObjects.Clear();
        }
    }
}
