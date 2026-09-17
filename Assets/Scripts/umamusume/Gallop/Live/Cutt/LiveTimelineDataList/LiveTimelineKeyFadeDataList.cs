using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyFadeData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Fade;
            }
        }

        public Color fadeColor;

        public LiveTimelineKeyFadeData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyFadeDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFadeData>
    {
        public LiveTimelineKeyFadeDataList()
        {
        }
    }
}
