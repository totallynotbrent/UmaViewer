using UnityEngine;

namespace Gallop.Live.Cutt
{
    public struct PropsAttachUpdateInfo
    {
        public bool isValid;

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
    }
}
