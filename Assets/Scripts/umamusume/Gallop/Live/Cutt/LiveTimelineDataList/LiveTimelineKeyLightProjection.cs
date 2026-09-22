using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // authored light-projection track: keys drive the mirror-ball / texture projection
    // rig (game key type LightProjection; the game consumes these through
    // LightProjectionController + CustomProjector).
    [Serializable]
    public class LiveTimelineKeyLightProjectionData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.LightProjection;
            }
        }

        public bool IsEnable;
        public bool OverrideIgnoreLayer;
        public LayerMask OverrideLayerMask;
        public bool BacksideOff;
        public float MirrorBallBacksideFeather;
        public bool ProjectionToCharacterModelOnly;
        public int TextureId;
        public Color Color;
        public Vector3 Position;
        public Vector3 Angle;
        public Vector3 Scale;
        public bool Orthographic;
        public float OrthographicSize;
        public float NearClipPlane;
        public float FarClipPlane;
        public float FieldOfView;
        public float ColorPower;
        public int LightBlendMode;
        public bool CharacterAttach;
        public int CharacterAttachPosition;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
        public bool IsAdjustedBlinkLightColor;
        public Vector3 MirrorBallRotateAxis;
        public float MirrorBallRotateValue;
        public float MirrorBallProjectionRadius;
        public float MirrorBallFallOffPower;
        public bool MirrorBallIsLoopRotation;
        public float MirrorBallLoopRotationSpeed;
        public Vector2 MirrorBallUVOffset;
        public Vector2 MirrorBallUVScale;
        public bool MirrorBallUseCubeMap;

        public LiveTimelineKeyLightProjectionData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyLightProjectionDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLightProjectionData>
    {
        public LiveTimelineKeyLightProjectionDataList()
        {
        }
    }

    [Serializable]
    public class LiveTimelineLightProjectionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLightProjectionDataList keys;
        public int ContentType;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineLightProjectionData()
        {
            keys = new LiveTimelineKeyLightProjectionDataList();
        }
    }
}
