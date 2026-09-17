using UnityEngine;

namespace Gallop.Live.Cutt
{
    public struct TiltShiftUpdateInfo
    {
        public bool isValid;

        public int mode;
        public int quality;
        public float blurArea;
        public float maxBlurSize;
        public int downsample;
        public Vector2 offset;
        public float roll;
    }
}
