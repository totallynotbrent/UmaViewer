using UnityEngine;

// ponytail: stubs for AssetBundle prefabs that reference Gallop.Live.* scripts stripped in viewer build
// Keep empty to suppress "missing script" warnings; upgrade path: implement official logic from decompiled Gallop.Live
namespace Gallop.Live
{
    public class UnityLensFlareController : MonoBehaviour {}
    public class RendererController : MonoBehaviour {}
    // Bundle references "CameraTimeline3" on CameraTimeline3 GO - original likely Gallop.Live.Cutt.CameraTimeline
    public class CameraTimeline3 : MonoBehaviour {}
    // Fallback for "<null>" on LiveControllerPrefab root - provides Director companion if stripped
    public class LiveControllerStub : MonoBehaviour {}
}
