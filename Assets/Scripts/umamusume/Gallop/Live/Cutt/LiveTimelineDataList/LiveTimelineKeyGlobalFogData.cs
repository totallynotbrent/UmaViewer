using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // global fog keys: distance or height fog with color, mode, density and an optional
    // blink-light link that tints the fog with the named fixture's color.
    [Serializable]
    public class LiveTimelineKeyGlobalFogData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.ColorCorrection;
            }
        }

        public bool isDistance;
        public float startDistance;
        public bool isHeight;
        public float height;
        public float heightDensity;
        public Color color;
        public int fogMode;
        public float expDensity;
        public float start;
        public float end;
        public bool useRadialDistance;
        public string BlinkLightName = "";
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower = 1f;
        public bool IsAdjustedBlinkLightColor = true;
    }

    [Serializable]
    public class LiveTimelineKeyGlobalFogDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyGlobalFogData>
    {
    }

    // per-track wrapper matching the bundle layout: named groups of fog keys.
    [Serializable]
    public class LiveTimelineGlobalFogData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "GlobalFog";

        public LiveTimelineKeyGlobalFogDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineGlobalFogData()
            : base(default_name)
        {
            keys = new LiveTimelineKeyGlobalFogDataList();
        }
    }
}
