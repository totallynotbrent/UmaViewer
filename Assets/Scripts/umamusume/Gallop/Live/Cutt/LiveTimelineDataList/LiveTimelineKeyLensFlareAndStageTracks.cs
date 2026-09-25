using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // authored lens-flare track: keys drive the stage flare fixtures' enable,
    // brightness and color (game key type LensFlare).
    [Serializable]
    public class LiveTimelineKeyLensFlareData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.LensFlare;
            }
        }

        public Vector3 offset;
        public Color color;
        public float brightness;
        public float fadeSpeed;
        public int enableParameter;
        public bool enableFlare;
        public bool IsAutoBrightness;
        public bool IsOverridePosition;

        public LiveTimelineKeyLensFlareData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyLensFlareDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLensFlareData>
    {
        public LiveTimelineKeyLensFlareDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineLensFlareData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLensFlareDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineLensFlareData()
        {
            keys = new LiveTimelineKeyLensFlareDataList();
        }
    }

    // authored projector drive track: per-projector motion/material/speed/color/power/
    // size keys (game key type Projector).
    [Serializable]
    public class LiveTimelineKeyProjectorData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Projector;
            }
        }

        public int motionID;
        public int materialID;
        public float speed;
        public Color color1;
        public float power;
        public Vector3 position;
        public float rotate;
        public Vector2 size;
        public int LightBlendMode;
        public int loopType;
        public int loopCount;
        public int loopExecutedCount;
        public int loopIntervalFrame;
        public bool isPasteLoopUnit;
        public bool isChangeLoopInterpolate;

        public LiveTimelineKeyProjectorData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyProjectorDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyProjectorData>
    {
        public LiveTimelineKeyProjectorDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineProjectorData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyProjectorDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineProjectorData()
        {
            keys = new LiveTimelineKeyProjectorDataList();
        }
    }

    // authored emitter track: keys set the named emitter's rate over the song
    // (game key type Particle).
    [Serializable]
    public class LiveTimelineKeyParticleData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Particle;
            }
        }

        public float emissionRate;

        public LiveTimelineKeyParticleData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyParticleDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyParticleData>
    {
        public LiveTimelineKeyParticleDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineParticleData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyParticleDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineParticleData()
        {
            keys = new LiveTimelineKeyParticleDataList();
        }
    }

    // authored particle-group flicker track: keys drive the named group's
    // flicker rates (game key type ParticleGroup).
    [Serializable]
    public class LiveTimelineKeyParticleGroupData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.ParticleGroup;
            }
        }

        public float FlickerLightRate;
        public float FlickerDarkRate;

        public LiveTimelineKeyParticleGroupData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyParticleGroupDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyParticleGroupData>
    {
        public LiveTimelineKeyParticleGroupDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineParticleGroupData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyParticleGroupDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineParticleGroupData()
        {
            keys = new LiveTimelineKeyParticleGroupDataList();
        }
    }

    // authored light-shaft track: keys toggle and steer the stage shaft pass
    // (game key type LightShafts).
    [Serializable]
    public class LiveTimelineKeyLightShaftsData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.LightShafts;
            }
        }

        public bool enabled;
        public Vector4 speed;
        public Vector4 angle;
        public Vector4 offset;
        public Vector4 alpha;
        public Vector4 alpha2;
        public Vector4 maskAlpha;
        public float maskAnimeTime;
        public Vector2 maskAlphaRange;
        public float scale;
        public bool _isAdjustScale;

        public LiveTimelineKeyLightShaftsData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyLightShaftsDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLightShaftsData>
    {
        public LiveTimelineKeyLightShaftsDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineLightShaftsData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLightShaftsDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineLightShaftsData()
        {
            keys = new LiveTimelineKeyLightShaftsDataList();
        }
    }

    // authored node-scale track: keys pick a scale-type per character flag set
    // (game key type NodeScale).
    [Serializable]
    public class LiveTimelineKeyNodeScaleData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.NodeScale;
            }
        }

        public int characterFlag;
        public int targetFlag;
        public int sizeType;
        public float scaleRatePer;

        public LiveTimelineKeyNodeScaleData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyNodeScaleDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyNodeScaleData>
    {
        public LiveTimelineKeyNodeScaleDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineNodeScaleData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyNodeScaleDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineNodeScaleData()
        {
            keys = new LiveTimelineKeyNodeScaleDataList();
        }
    }

    // authored title overlay track: keys trigger the title's in/out actions
    // (game key type Title).
    [Serializable]
    public class LiveTimelineKeyTitleData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.Title;
            }
        }

        public int _actionType;
        public int _actionFrame;

        public LiveTimelineKeyTitleData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyTitleDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTitleData>
    {
        public LiveTimelineKeyTitleDataList()
        {
        }
    }

}
