using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyFluctuationData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Fluctuation;
            }
        }

        public bool IsEnable;
        public Vector2 MoveDirection;
        public float MovePower;
        public float Power;
        public float DepthClip;

        public LiveTimelineKeyFluctuationData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyFluctuationDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFluctuationData>
    {
        public LiveTimelineKeyFluctuationDataList()
        {
        }
    }
}
