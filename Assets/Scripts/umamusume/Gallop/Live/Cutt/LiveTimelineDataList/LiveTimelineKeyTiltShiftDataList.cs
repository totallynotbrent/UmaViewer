using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyTiltShiftData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.TiltShift;
            }
        }

        public int mode;
        public int quality;
        public float blurArea;
        public float maxBlurSize;
        public int downsample;
        public Vector2 offset;
        public float roll;

        public LiveTimelineKeyTiltShiftData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyTiltShiftDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTiltShiftData>
    {
        public LiveTimelineKeyTiltShiftDataList()
        {
        }
    }
}
