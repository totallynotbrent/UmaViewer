using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyVortexData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Vortex;
            }
        }

        public bool IsEnable;
        public Vector4 Area;
        public float RotVolume;
        public float DepthClip;

        public LiveTimelineKeyVortexData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyVortexDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyVortexData>
    {
        public LiveTimelineKeyVortexDataList()
        {
        }
    }
}
