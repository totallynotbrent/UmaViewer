using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyHandShakeCameraData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.HandShakeCamera;
            }
        }

        public float power;
        public float frequency;
        public float Rate;

        public LiveTimelineKeyHandShakeCameraData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyHandShakeCameraDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyHandShakeCameraData>
    {
        public LiveTimelineKeyHandShakeCameraDataList()
        {
        }
    }
}
