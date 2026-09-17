using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyPropsData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Props;
            }
        }

        public int settingFlags;
        public int propsID;
        public bool rendererEnable;
        public bool IsVisibleAttachedCharaLinked;
        public bool AutoSwitchLayerOnMirrorRendering;
        public Color color;
        public Color rootColor;
        public Color tipColor;
        public float colorPower;
        public bool IsApplyAnimation;
        public bool IsApplyReserveWarming;
        public AnimationClip AnimationClip;
        public float StartAnimationTime;
        public int AnimationHeadFrame;
        public Color ToonDarkColor;
        public Color ToonBrightColor;
        public bool IsCastShadow;
        public bool IsCastShadowForced;
        public Vector3 _directionalLightAngle;
        public bool IsUpdateOutline;
        public float OutlineWidth;
        public Color OutlineColor;
        public bool IsEmissive;
        public Color EmissiveColor;
        public float EmissiveScrollTimeScale;
        public float EmissiveScrollEnergyScale;

        public LiveTimelineKeyPropsData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyPropsDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsData>
    {
        public LiveTimelineKeyPropsDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelinePropsData : ILiveTimelineGroupDataWithName
    {
        public string name;
        public LiveTimelineKeyPropsDataList keys;
        public int _applyVariation;
        public int _variationId;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelinePropsData()
        {
            keys = new LiveTimelineKeyPropsDataList();
        }
    }
}
