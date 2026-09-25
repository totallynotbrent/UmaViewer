using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // exposure keys ship gain/lift per key; the volume exposure follows the authored track.
    [Serializable]
    public class LiveTimelineKeyExposureData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.ColorCorrection;
            }
        }

        public bool IsEnable;
        public float DepthMask;
        public float Gain;
        public float Lift;
        public float MaskGain;
        public float MaskLift;
    }

    [Serializable]
    public class LiveTimelineKeyExposureDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyExposureData>
    {
    }

    // tone curve keys carry full channel curves with min/max clamp colors.
    [Serializable]
    public class LiveTimelineKeyToneCurveData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.ColorCorrection;
            }
        }

        public bool IsEnable;
        public float DepthMask;
        public AnimationCurve ToneAnimationCurve;
        public Color MinCorrectionLevel;
        public Color MaxCorrectionLevel;
        public AnimationCurve MaskToneCurve;
        public Color MaskMinCorrectionLevel;
        public Color MaskMaxCorrectionLevel;
    }

    [Serializable]
    public class LiveTimelineKeyToneCurveDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyToneCurveData>
    {
    }
}
