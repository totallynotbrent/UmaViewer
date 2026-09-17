using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyRadialBlurData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.RadialBlur;
            }
        }

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

        public LiveTimelineKeyRadialBlurData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyRadialBlurDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyRadialBlurData>
    {
        public LiveTimelineKeyRadialBlurDataList()
        {
        }
    }
}
