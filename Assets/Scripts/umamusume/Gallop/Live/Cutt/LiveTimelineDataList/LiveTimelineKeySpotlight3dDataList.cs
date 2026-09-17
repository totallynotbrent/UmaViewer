using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeySpotlight3dData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Spotlight3d;
            }
        }

        public bool isActive;
        public Color color;
        public float colorPower;
        public float localHeight;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public Vector3 characterPosition;
        public int targetCameraType;
        public int targetCameraIndex;
        public string assetName;
        public int characterIndex;

        public LiveTimelineKeySpotlight3dData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeySpotlight3dDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeySpotlight3dData>
    {
        public LiveTimelineKeySpotlight3dDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineSpotlight3dData : ILiveTimelineGroupDataWithName
    {
        public string name;
        public LiveTimelineKeySpotlight3dDataList keys;
        public int _characterIndex;
        public int _assetId;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineSpotlight3dData()
        {
            keys = new LiveTimelineKeySpotlight3dDataList();
        }
    }
}
