using UnityEngine;

namespace Gallop.Live.Cutt
{
    // mirrors the game's ToneCurveUpdateInfo field-for-field.
    public struct ToneCurveUpdateInfo
    {
        public bool isValid;
        public bool IsEnable;
        public AnimationCurve ToneAnimationCurve;
        public AnimationCurve MaskToneCurve;
        public Color MinCorrectionLevel;
        public Color MaxCorrectionLevel;
        public Color MaskMinCorrectionLevel;
        public Color MaskMaxCorrectionLevel;
        public float DepthMask;
    }
}
