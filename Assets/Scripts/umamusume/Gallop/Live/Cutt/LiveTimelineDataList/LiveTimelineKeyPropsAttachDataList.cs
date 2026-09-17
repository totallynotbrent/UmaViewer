using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyPropsAttachData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.PropsAttach;
            }
        }

        public string _attachJointName;
        public int _attachJointHash;
        public string _copyPositionJointName;
        public int _copyPositionJointHash;
        public int _settingFlags;
        public int _propsId;
        public Vector3 _offsetPosition;
        public Vector3 OffsetRotate;
        public Vector3 OffsetScale;
        public bool IsLinkAttachBone;
        public int _attachType;
        public int _attachPropId;
        public string _attachTargetPropNodeName;

        public LiveTimelineKeyPropsAttachData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyPropsAttachDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsAttachData>
    {
        public LiveTimelineKeyPropsAttachDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelinePropsAttachData : ILiveTimelineGroupDataWithName
    {
        public string name;
        public LiveTimelineKeyPropsAttachDataList keys;
        public int _applyVariation;
        public int _variationId;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelinePropsAttachData()
        {
            keys = new LiveTimelineKeyPropsAttachDataList();
        }
    }
}
