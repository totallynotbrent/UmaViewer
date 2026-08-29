using UnityEngine;
using System.Collections.Generic;

namespace Gallop.Live
{
    /// <summary>
    /// Hides decorative billboard objects that appear as white rectangles.
    /// Does NOT hide cyalume/light sticks, lights, or audience objects.
    /// </summary>
    public class StageHideBillboards : MonoBehaviour
    {
        [Header("Billboard Hide Settings")]
        [SerializeField] private bool _hideOnStart = true;
        [SerializeField] private bool _verboseLog = true;

        private List<GameObject> _hiddenObjects = new List<GameObject>();

        // Keywords that indicate decorative billboards (safe to hide)
        private string[] _decorativeBillboardKeywords = {
            "decoration_a", "decoration_b"
        };

        // Keywords that should NEVER be hidden (lights, cyalume, audience)
        private string[] _preserveKeywords = {
            "cyalume", "light_stick", "glow", "audience", "crowd", "mob",
            "wash_roof", "box_light", "flarelight", "blink", "laser",
            "spot", "beam", "light_", "_light", "led", "monitor",
            "billboard_ground", "billboard_truss", "decoration_billboard"
        };

        private void Start()
        {
            if (_hideOnStart)
                HideDecorativeBillboards();
        }

        public void HideDecorativeBillboards()
        {
            _hiddenObjects.Clear();

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            int hiddenCount = 0;

            foreach (var obj in allObjects)
            {
                if (obj == null) continue;

                string name = obj.name ?? "";
                bool isDecorativeBillboard = false;
                bool shouldPreserve = false;

                // Check if it's a decorative billboard (safe to hide)
                foreach (var keyword in _decorativeBillboardKeywords)
                {
                    if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isDecorativeBillboard = true;
                        break;
                    }
                }

                // Check if it should be preserved (lights, cyalume, audience)
                foreach (var keyword in _preserveKeywords)
                {
                    if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldPreserve = true;
                        break;
                    }
                }

                // Only hide decorative billboards that aren't preserved
                if (isDecorativeBillboard && !shouldPreserve && obj.activeSelf)
                {
                    obj.SetActive(false);
                    _hiddenObjects.Add(obj);
                    hiddenCount++;

                    if (_verboseLog && hiddenCount <= 10)
                        Debug.Log($"[StageHideBillboards] Hid: {name}");
                }
            }

            if (_verboseLog)
                Debug.Log($"[StageHideBillboards] Hid {hiddenCount} decorative billboard objects.");
        }

        public void ShowAllBillboards()
        {
            int showCount = 0;
            foreach (var obj in _hiddenObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    showCount++;
                }
            }

            if (_verboseLog)
                Debug.Log($"[StageHideBillboards] Showed {showCount} billboard objects.");
            _hiddenObjects.Clear();
        }
    }
}
