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
        public float animationOffsetTime;
        public float AnimationTime;
        public bool UseAnimationTime;
        public Color AudienceColor;
        public float AudienceColorPower;
        public Color AudienceToonBrightColor;
        public Color AudienceToonDarkColor;
        public float AudienceVertexColorToonPower;
        public float AudienceOutlineWidthPower;
        public Color AudienceOutlineColor;
        public int AudienceOutlineColorBlend;
        public Color AudienceRimColor;
        public float AudienceRimStep;
        public float AudienceRimFeather;
        public float AudienceRimSpecRate;
        public float AudienceRimShadowRate;
        public float AudienceRimHorizonOffset;
        public float AudienceRimVerticalOffset;
        public Color AudienceRimColor2;
        public float AudienceRimStep2;
        public float AudienceRimFeather2;
        public float AudienceRimSpecRate2;
        public float AudienceRimShadowRate2;
        public float AudienceRimHorizonOffset2;
        public float AudienceRimVerticalOffset2;
        public Vector3 AudienceDirectionalLightAngle;
    }

    [Serializable]
    public class LiveTimelineKeyAudienceDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyAudienceData>
    {
    }

    [Serializable]
    public class LiveTimelineAudienceData : ILiveTimelineGroupDataWithName
    {
        // name (the crowd prefab, e.g. pfb_env_live_cmn_audience100_r_toon)
        // is inherited from ILiveTimelineGroupDataWithName; keys + index are local.
        public LiveTimelineKeyAudienceDataList keys;
        public int _objectIndex;
    }
}
