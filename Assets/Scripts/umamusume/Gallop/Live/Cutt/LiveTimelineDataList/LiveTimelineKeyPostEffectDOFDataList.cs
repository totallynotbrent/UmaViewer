using System;
using Gallop.ImageEffect;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyPostEffectDOFData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.PostEffectDOF;
            }
        }

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

        public LiveTimelineKeyPostEffectDOFData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyPostEffectDOFDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPostEffectDOFData>
    {
        public LiveTimelineKeyPostEffectDOFDataList()
        {
        }
    }
}
