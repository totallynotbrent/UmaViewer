using UnityEngine;

namespace Gallop.Live.Cutt
{
    // per-frame global fog state handed to the camera fog handler.
    public struct GlobalFogUpdateInfo
    {
        public bool isDistance;
        public float startDistance;
        public bool isHeight;
        public float height;
        public float heightDensity;
        public Color color;
        public int fogMode;
        public float expDensity;
        public float start;
        public float end;
        public bool useRadialDistance;
    }
}
