using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // authored renderer on/off track: keys toggle a named stage object's renderers
    // across the song (the game's RendererControl system).
    [Serializable]
    public class LiveTimelineKeyRendererData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Renderer;
            }
        }

        public bool renderEnable;

        public LiveTimelineKeyRendererData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyRendererDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyRendererData>
    {
        public LiveTimelineKeyRendererDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineRendererData : ILiveTimelineGroupDataWithName
    {
        public string name;
        public LiveTimelineKeyRendererDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineRendererData()
        {
            keys = new LiveTimelineKeyRendererDataList();
        }
    }
}
