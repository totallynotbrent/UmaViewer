using Gallop.ImageEffect;

namespace Gallop.Live.Cutt
{
    public struct PostEffectUpdateInfo_DOF
    {
        public bool isValid;

        public float forcalSize;
        public float blurSpread;
        public int charactor;
        public DofDiffusionBloomOverlayParam.DofDiffusionBloomType dofBlurType;
        public int dofQuality;
        public float dofForegroundSize;
        public float dofFocalPoint;
        public float dofSmoothness;
        public float BallBlurPowerFactor;
        public float BallBlurBrightnessThreshhold;
        public float BallBlurBrightnessIntensity;
        public float BallBlurSpread;
    }
}
