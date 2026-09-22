using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // authored camera layer track: keys give a min/max framing offset band the
    // chara-relative cameras apply (game key type CameraLayer).
    [Serializable]
    public class LiveTimelineKeyCameraLayerData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.CameraLayer;
            }
        }

        public Vector3 offsetMaxPosition;
        public Vector3 offsetMinPosition;

        public LiveTimelineKeyCameraLayerData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyCameraLayerDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCameraLayerData>
    {
        public LiveTimelineKeyCameraLayerDataList()
        {
        }
    }

    // authored facial noise track: a single key class carrying which characters get
    // the micro-expression jitter (game key type FacialNoise).
    [Serializable]
    public class LiveTimelineKeyFacialNoiseData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.FacialNoise;
            }
        }

        public int EnableCharacterBitFlag;

        public LiveTimelineKeyFacialNoiseData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyFacialNoiseDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFacialNoiseData>
    {
        public LiveTimelineKeyFacialNoiseDataList()
        {
        }
    }

    // authored motion noise track: per-side spring/bone noise plus cy-spring gravity
    // overrides (game key type CharaMotionNoise).
    [Serializable]
    public class LiveTimelineKeyCharaMotionNoiseData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.CharaMotionNoise;
            }
        }

        public float sideChrMotNoiseBaseBias;
        public float sideChrMotNoiseRange;
        public float sideChrMotNoiseFrequency;
        public float backChrMotNoiseBaseBias;
        public float backChrMotNoiseRange;
        public float backChrMotNoiseFrequency;
        public bool isNegativeCheck;
        public int cySpringGravityScaleType;
        public float cySpringGravityScale;
        public string cySpringGravityScaleBoneName;
        public float cySpringExpressionKneeCollisionRadius;
        public float cySpringExpressionAnkleCollisionRadius;
        public float cySpringExpressionInfluenceAngle;
        public float cySpringExpressionInfluenceMaxAngle;

        public LiveTimelineKeyCharaMotionNoiseData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyCharaMotionNoiseDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaMotionNoiseData>
    {
        public LiveTimelineKeyCharaMotionNoiseDataList()
        {
        }
    }

    // authored sweat locator track: per-owner droplet locator visibility and offsets
    // (game key type SweatLocator).
    [Serializable]
    public class LiveTimelineKeySweatLocatorData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.SweatLocator;
            }
        }

        public int owner;
        public float alpha;
        public int randomVisibleCount;

        public bool locator0_isVisible;
        public Vector3 locator0_offset;
        public Vector3 locator0_offsetAngle;
        public int locator0_offsetType;

        public bool locator1_isVisible;
        public Vector3 locator1_offset;
        public Vector3 locator1_offsetAngle;
        public int locator1_offsetType;

        public bool locator2_isVisible;
        public Vector3 locator2_offset;
        public Vector3 locator2_offsetAngle;
        public int locator2_offsetType;

        public bool locator3_isVisible;
        public Vector3 locator3_offset;
        public Vector3 locator3_offsetAngle;
        public int locator3_offsetType;

        public bool locator4_isVisible;
        public Vector4 locator4_offset;
        public Vector3 locator4_offsetAngle;
        public int locator4_offsetType;

        public LiveTimelineKeySweatLocatorData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeySweatLocatorDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeySweatLocatorData>
    {
        public LiveTimelineKeySweatLocatorDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineSweatLocatorData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeySweatLocatorDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineSweatLocatorData()
        {
            keys = new LiveTimelineKeySweatLocatorDataList();
        }
    }

    [Serializable]
    public class LiveTimelineCameraLayerData
    {
        public LiveTimelineKeyCameraLayerDataList keys;

        public ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineCameraLayerData()
        {
            keys = new LiveTimelineKeyCameraLayerDataList();
        }
    }

    [Serializable]
    public class LiveTimelineFacialNoiseData
    {
        public LiveTimelineKeyFacialNoiseDataList keys;

        public ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineFacialNoiseData()
        {
            keys = new LiveTimelineKeyFacialNoiseDataList();
        }
    }

    [Serializable]
    public class LiveTimelineCharaMotionNoiseData
    {
        public LiveTimelineKeyCharaMotionNoiseDataList keys;

        public ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineCharaMotionNoiseData()
        {
            keys = new LiveTimelineKeyCharaMotionNoiseDataList();
        }
    }
}
