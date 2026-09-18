using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // audience/crowd block track: transform + cyalume tint + which crowd
    // animation clip plays, per keyframed block instance.
    [Serializable]
    public class LiveTimelineKeyAudienceData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Audience;
            }
        }

        public Vector3 position;
        public Vector3 rotate;
        public Vector3 scale;
        public Color cyalumeColor;
        public Color cyalumeGlowColor;
        public float cyalumeGlowColorPower;
        public float cyalumeMaskRadius;
        public int animationSetting;
        public int animationRootIndex;
        public int animationBodyRegion;
        public int animationCategory;
        public int animationIndex;
        public int animationWrapMode;
        public float animationSpeed;
    }

    public class LiveTimelineKeyAudienceDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyAudienceData>
    {
    }

    [Serializable]
    public class LiveTimelineAudienceData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyAudienceDataList keys;
        public int _objectIndex;
    }
}
