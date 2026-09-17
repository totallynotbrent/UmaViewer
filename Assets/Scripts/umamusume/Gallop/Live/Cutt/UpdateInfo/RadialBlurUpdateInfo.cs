using UnityEngine;

namespace Gallop.Live.Cutt
{
    public struct RadialBlurUpdateInfo
    {
        public bool isValid;

        public int moveBlurType;
        public Vector2 radialBlurOffset;
        public int radialBlurDownsample;
        public float radialBlurStartArea;
        public float radialBlurEndArea;
        public float radialBlurPower;
        public int radialBlurIteration;
        public Vector2 radialBlurEllipseDir;
        public float radialBlurRollEulerAngles;
        public float depthPowerFront;
        public float depthPowerBack;
        public Vector4 depthCancelRect;
        public float depthCancelBlendLength;
    }
}
