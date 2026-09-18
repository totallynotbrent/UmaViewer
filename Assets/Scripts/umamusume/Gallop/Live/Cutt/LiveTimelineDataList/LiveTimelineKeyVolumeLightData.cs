using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // sun-shaft / volumetric glow track; rendered as a bloom-driven glow until a
    // dedicated light-shaft pass exists.
    [Serializable]
    public class LiveTimelineKeyVolumeLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.VolumeLight;
            }
        }

        public bool enable;
        public float power;
        public float blurRadius;
        public float komorebi;
        public float ColorRate;
        public float EffectColorPower;
        public float ScreenColorPower;
        public float BlinkLightBrightnessPower;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public bool IsAdjustedBlinkLightColor;
        public bool isEnabledBorderClear;
        public Vector3 sunPosition;
        public Color color1;
    }

    public class LiveTimelineKeyVolumeLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyVolumeLightData>
    {
    }

    [Serializable]
    public class LiveTimelineVolumeLightData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyVolumeLightDataList keys;
    }
}
