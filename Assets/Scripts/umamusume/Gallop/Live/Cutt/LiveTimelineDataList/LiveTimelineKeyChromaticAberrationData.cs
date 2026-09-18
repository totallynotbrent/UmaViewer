using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // lens fringe track; rendered through the URP chromatic aberration override.
    [Serializable]
    public class LiveTimelineKeyChromaticAberrationData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.ChromaticAberration;
            }
        }

        public bool isEnable;
        public float power;
        public float clip;
        public int effectType;
        public Vector2 redOffset;
        public Vector2 greenOffset;
        public Vector2 blueOffset;
    }

    public class LiveTimelineKeyChromaticAberrationDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyChromaticAberrationData>
    {
    }

    [Serializable]
    public class LiveTimelineChromaticAberrationData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyChromaticAberrationDataList keys;
    }
}
