using System;
using System.Collections.Generic;
using UnityEngine;

// declares the worksheet tracks the game authors that the fork's worksheet class
// never declared, so unity's name-matching deserializer stopped dropping them at
// load. field names and types come from the extracted game schema
// (umadump out/game_dump.json + the worksheet typetree).
namespace Gallop.Live.Cutt
{
    // cinematic animation-clip camera moves.
    [Serializable]
    public class LiveTimelineKeyCameraMotionData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CameraMotion;
        public bool IsEnable;
        public int MotionType;
        public AnimationClip Clip;
        public float MotionHeadTime;
        public float PlaySpeed;
        public LiveCharaPositionFlag CharaRelativeBase;
        public LiveCameraCharaParts CharaRelativeParts;
        public Vector3 Offset;
        public Vector3 CharaPos;
    }

    [Serializable]
    public class LiveTimelineKeyCameraMotionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCameraMotionData> { }

    // generic cutscene event hooks; the nested event payload stays opaque.
    [Serializable]
    public class LiveTimelineKeyEventData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public int eventType;
        public int eventValue;
    }

    [Serializable]
    public class LiveTimelineKeyEventDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyEventData> { }

    // stage texture scroll animations.
    [Serializable]
    public class LiveTimelineKeyTextureAnimationData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.TextureAnimation;
        public string textureName;
        public bool textureNameEmpty;
        public Vector2 offset;
        public Vector2 tiling;
        public Vector2 scrollSpeed;
        public float scrollInterval;
        public int textureID;
    }

    [Serializable]
    public class LiveTimelineKeyTextureAnimationDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTextureAnimationData> { }

    [Serializable]
    public class LiveTimelineTextureAnimationData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyTextureAnimationDataList keys;
    }

    // crowd wave objects.
    [Serializable]
    public class LiveTimelineKeyWaveObjectData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.WaveObject;
        public bool IsWorldDir;
        public Vector3 WaveDir;
        public float WaveFreq;
        public float WaveSpeed;
        public float WaveSize;
    }

    [Serializable]
    public class LiveTimelineKeyWaveObjectDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyWaveObjectData> { }

    [Serializable]
    public class LiveTimelineWaveObjectData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyWaveObjectDataList keys;
    }

    // parent constraints binding stage objects to character nodes.
    [Serializable]
    public class LiveTimelineKeyParentConstraintData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public bool _enable;
        public float _weight;
        public bool _influenceOfSourceScale;
    }

    [Serializable]
    public class LiveTimelineKeyParentConstraintDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyParentConstraintData> { }

    [Serializable]
    public class LiveTimelineParentConstraintData : ILiveTimelineGroupData
    {
        public LiveTimelineKeyParentConstraintDataList keys;
        public int _objectType;
        public int _charaIndex;
        public int _dressIndex;
        public int _propsIndex;
        public string _stageObjectName;
        public int _stageObjectNameHash;
    }

    // lip-sync pattern selection.
    [Serializable]
    public class LiveTimelineKeyLipSyncPatternData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.LipSyncPattern;
        public int PatternRangeIndex;
    }

    [Serializable]
    public class LiveTimelineKeyLipSyncPatternDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLipSyncPatternData> { }

    [Serializable]
    public class LiveTimelineKeyLipSyncPatternRangeData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.LipSyncPatternRange;
    }

    [Serializable]
    public class LiveTimelineKeyLipSyncPatternRangeDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLipSyncPatternRangeData> { }

    [Serializable]
    public class LiveTimelineLipSyncPatternData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLipSyncPatternDataList keys;
        public int _patternId;
        public bool _applyVariation;
        public int _variationId;
    }

    // transmitted light (god-ray style light bleed) and its masks.
    [Serializable]
    public class LiveTimelineKeyTransmittedLightData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.TransmittedLight;
        public int Iterations;
        public float Intensity;
        public float Threshold;
        public float BlurSpread;
        public int BlendMode;
        public int ATTR_ENABLE;
    }

    [Serializable]
    public class LiveTimelineKeyTransmittedLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTransmittedLightData> { }

    [Serializable]
    public class LiveTimelineKeyTransmittedLightMaskData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.TransmittedLightMask;
        public int _lightContainerCount;
        public float[] MaskScaleArray;
        public bool _isBlinkLightColorPowerLinked;
    }

    [Serializable]
    public class LiveTimelineKeyTransmittedLightMaskDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTransmittedLightMaskData> { }

    [Serializable]
    public class LiveTimelineTransmittedLightMaskData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyTransmittedLightMaskDataList keys;
    }

    // lens distortion.
    [Serializable]
    public class LiveTimelineKeyLensDistortionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public float Intensity;
        public float IntensityX;
        public float IntensityY;
        public float CenterX;
        public float CenterY;
        public float Scale;
    }

    [Serializable]
    public class LiveTimelineKeyLensDistortionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLensDistortionData> { }

    // screen captures requested by the timeline.
    [Serializable]
    public class LiveTimelineKeyScreenCaptureData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public int CaptureId;
        public float CaptureScale;
    }

    [Serializable]
    public class LiveTimelineKeyScreenCaptureDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyScreenCaptureData> { }

    // tail motion selection.
    [Serializable]
    public class LiveTimelineKeyTailMotionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public bool UseTailRandomMotion;
        public int MotionIndex;
        public float PlaySpeed;
        public bool IsTimescaleDisabled;
    }

    [Serializable]
    public class LiveTimelineKeyTailMotionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTailMotionData> { }

    // facial toon light rig, per character slot.
    [Serializable]
    public class LiveTimelineKeyFacialToonData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public float CheekPretenseThreshold;
        public float NosePretenseThreshold;
        public float CylinderBlend;
        public float HairNormalBlend;
        public int UseOriginalDirectionalLight;
        public Vector3 OriginalDirectionalLightDir;
        public float EyeToonStep;
        public float EyeToonFeather;
        public float EyeSaturation;
    }

    [Serializable]
    public class LiveTimelineKeyFacialToonDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFacialToonData> { }

    [Serializable]
    public class LiveTimelineFacialToonData : ILiveTimelineGroupData
    {
        public const int DATA_LIST_SIZE = 20;
        public LiveTimelineKeyFacialToonDataList centerKeys;
        public LiveTimelineKeyFacialToonDataList left1Keys;
        public LiveTimelineKeyFacialToonDataList right1Keys;
        public LiveTimelineKeyFacialToonDataList left2Keys;
        public LiveTimelineKeyFacialToonDataList right2Keys;
        public LiveTimelineKeyFacialToonDataList motion5Keys;
        public LiveTimelineKeyFacialToonDataList motion6Keys;
        public LiveTimelineKeyFacialToonDataList motion7Keys;
        public LiveTimelineKeyFacialToonDataList motion8Keys;
        public LiveTimelineKeyFacialToonDataList motion9Keys;
        public LiveTimelineKeyFacialToonDataList motion10Keys;
        public LiveTimelineKeyFacialToonDataList motion11Keys;
        public LiveTimelineKeyFacialToonDataList motion12Keys;
        public LiveTimelineKeyFacialToonDataList motion13Keys;
        public LiveTimelineKeyFacialToonDataList motion14Keys;
        public LiveTimelineKeyFacialToonDataList motion15Keys;
        public LiveTimelineKeyFacialToonDataList motion16Keys;
        public LiveTimelineKeyFacialToonDataList motion17Keys;
        public LiveTimelineKeyFacialToonDataList motion18Keys;
        public LiveTimelineKeyFacialToonDataList motion19Keys;
    }

    // pre color correction (character/shadow exclusion masks).
    [Serializable]
    public class LiveTimelineKeyPreColorCorrectionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.PreColorCorrection;
        public uint excludeColorCorrectionCharacterFlags;
        public uint excludeColorCorrectionShadowFlags;
    }

    [Serializable]
    public class LiveTimelineKeyPreColorCorrectionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPreColorCorrectionData> { }

    [Serializable]
    public class LiveTimelinePreColorCorrectionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyPreColorCorrectionDataList keys;
    }

    // monitor camera layer + position + look-at.
    [Serializable]
    public class LiveTimelineKeyMonitorCameraLayerData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public int layer;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraLayerDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorCameraLayerData> { }

    [Serializable]
    public class LiveTimelineMonitorCameraLayerData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMonitorCameraLayerDataList keys;
    }

    [Serializable]
    public class LiveTimelineKeyEyeCameraPositionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.EyeCameraPos;
        public bool IsEnabled;
        public float Power;
        public float Roll;
        public float Fov;
        public LiveCameraCullingLayer CullingMask;
        public Texture2D MaskTexture;
    }

    [Serializable]
    public class LiveTimelineKeyEyeCameraPositionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyEyeCameraPositionData> { }

    [Serializable]
    public class LiveTimelineEyeCameraPositionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyEyeCameraPositionDataList keys;
        public int _characterIndex;
    }

    [Serializable]
    public class LiveTimelineKeyEyeCameraLookAtData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.EyeCameraLookAt;
    }

    [Serializable]
    public class LiveTimelineKeyEyeCameraLookAtDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyEyeCameraLookAtData> { }

    [Serializable]
    public class LiveTimelineEyeCameraLookAtData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyEyeCameraLookAtDataList keys;
    }

    // the real stage-environment track: water, shadow and mirror flags.
    [Serializable]
    public class LiveTimelineKeyStageEnvironmentData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Environment;
        public bool isValidMirror;
        public bool isMirror;
        public bool isBgMirror;
        public bool IsMirrorBg3d;
        public bool EnableCharacterMirrorExpandFaceBounds;
        public LiveCharaPositionFlag characterMirror;
        public LiveCharaPositionFlag CharacterMirrorHead;
        public LiveCharaPositionFlag CharacterMirrorExpandFaceBounds;
        public float mirrorReflectionRate;
        public bool isValidShadow;
        public LiveCharaPositionFlag characterShadow;
        public bool isSoftShadow;
        public bool IsToonMirror;
        public bool isValidWaterReflection;
        public float waterReflection;
        public float waveScale;
        public float waterDistortion;
        public float waterUCross;
        public float waterVCross;
        public float waterUSpeed;
        public float waterVSpeed;
        public float waterNormalPower;
        public float waveDistortionPower;
        public float waveClearly;
        public float waveDiffusion;
        public float waveDecline;
        public Color waterColor;
        public float waterVOffset;
        public bool IsValidStageFovShift;
        public float BaseFov;
        public float ShiftPower;
        public bool IsShiftY;
    }

    [Serializable]
    public class LiveTimelineKeyStageEnvironmentDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyStageEnvironmentData> { }

    [Serializable]
    public class LiveTimelineEnvironmentData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyStageEnvironmentDataList keys;
    }

    // mirror reflection as its own named track with per-track base camera.
    [Serializable]
    public class LiveTimelineMirrorReflectionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMirrorReflectionDataList keys;
        public LiveTimelineDefine.MirrorReflectionBaseCameraType _baseCameraType;
        public int _baseCameraIndex;
    }

    // live stage effects (the concert vfx layer).
    [Serializable]
    public class LiveTimelineKeyEffectData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Effect;
        public int attributeFlags;
        public Color color;
        public float colorPower;
    }

    [Serializable]
    public class LiveTimelineKeyEffectDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyEffectData> { }

    [Serializable]
    public class LiveTimelineEffectData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyEffectDataList keys;
        public bool _applyVariation;
        public int _variationId;
        public string _folder;
    }

    // foot-contact effects.
    [Serializable]
    public class LiveTimelineKeyContactEffectData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public bool ResetEffectAll;
        public bool IsEnabled;
        public LiveCharaPositionFlag CharacterFlag;
        public int EffectMaxCount;
        public Color EffectColor;
        public float EffectColorPower;
        public bool ResetEffectColor;
        public bool IsEffectColorPowerRGBOnly;
        public Vector3 EffectOffset;
        public Vector3 EffectRotate;
        public float ContactPlaneHeight;
    }

    [Serializable]
    public class LiveTimelineKeyContactEffectDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyContactEffectData> { }

    [Serializable]
    public class LiveTimelineContactEffectData : ILiveTimelineGroupData
    {
        public LiveTimelineKeyContactEffectDataList keys;
        public string _folder;
    }

    // ray-hit spark effects.
    [Serializable]
    public class LiveTimelineKeyRayHitEffectData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public bool ResetEffectAll;
        public bool IsEnabled;
        public bool IsRaycastMaskLayerCharaModel;
        public bool IsRaycastMaskLayerBG;
        public bool IsRaycastMaskLayer3D;
        public LiveCharaPositionFlag Character;
        public float RayOriginAngleX;
        public float RayOriginAngleZ;
        public float RayOriginRadius;
        public int RayTargetNode;
        public Vector3 RayTargetPositionOffset;
        public float RayMaxDistance;
        public float RaycastInterval;
        public int RayHitEffectMaxCount;
    }

    [Serializable]
    public class LiveTimelineKeyRayHitEffectDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyRayHitEffectData> { }

    [Serializable]
    public class LiveTimelineRayHitEffectData : ILiveTimelineGroupData
    {
        public LiveTimelineKeyRayHitEffectDataList keys;
        public string _folder;
        public float _effectDuration;
    }

    // hatching (sketch shading).
    [Serializable]
    public class LiveTimelineKeyHatchingData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public int BlendMode;
        public float BlendAlpha;
        public Texture2D NoiseTexture;
        public float StrokeTiling;
        public float StrokeHeight;
        public float StrokeAngle;
        public float StrokeLength;
        public Vector2 CircleMaskOffset;
        public float CircleMaskSize;
        public Vector2 CircleMaskStretchRatio;
        public float CircleMaskAngle;
        public float CircleMaskInnerRatio;
        public float CircleMaskInnerEffect;
    }

    [Serializable]
    public class LiveTimelineKeyHatchingDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyHatchingData> { }

    // edge style (mist outline).
    [Serializable]
    public class LiveTimelineKeyEdgeStyleData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public bool IsEnable;
        public Vector2 OffsetDirectionBase;
        public float OffsetScaleBase;
        public Vector2 OffsetDirectionExpand;
        public float OffsetScaleExpand;
        public bool IsCharacterListMode;
        public List<int> VisibleIndexList;
        public int DownscaleFactor;
        public float MinDepthValue;
        public float MaxDepthValue;
        public Texture2D MistNoiseTexture;
        public Color MistColor;
        public float MistDensity;
    }

    [Serializable]
    public class LiveTimelineKeyEdgeStyleDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyEdgeStyleData>
    {
        public int EdgeStyle;
    }

    // camera-flash bursts.
    [Serializable]
    public class LiveTimelineKeyFlashPlayerData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.FlashPlayer;
        public int _actionType;
        public bool UseActionLabel;
        public string ActionLabel;
        public string CueSheetName;
        public string CueName;
    }

    [Serializable]
    public class LiveTimelineKeyFlashPlayerDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFlashPlayerData> { }

    // chara node cyspring toggles.
    [Serializable]
    public class LiveTimelineKeyCharaNodeData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CharaNode;
        public LiveCharaPositionFlag PositionFlag;
        public bool EnableHeadCySpring;
        public bool EnableEarCySpring;
        public bool EnableBodyCySpring;
        public bool EnableSkirtCySpring;
        public bool EnableTailCySpring;
        public List<string> TargetCySpringBornNameList;
        public List<bool> TargetCySpringBornEnableList;
    }

    [Serializable]
    public class LiveTimelineKeyCharaNodeDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaNodeData> { }

    [Serializable]
    public class LiveTimelineCharaNodeData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyCharaNodeDataList keys;
    }

    // per-position character foot lights.
    [Serializable]
    public class LiveTimelineKeyCharaFootLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CharaFootLight;
        public LiveCharaPositionFlag positionFlag;
        public float[] hightMax;
        public Color[] lightColor;
        public int[] LightBlendModeArray;
        public int[] EasingArray;
    }

    [Serializable]
    public class LiveTimelineKeyCharaFootLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaFootLightData> { }

    // stage billboards.
    [Serializable]
    public class LiveTimelineKeyBillboardData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Billboard;
        public bool manualAngle;
        public Vector3 angle;
        public Quaternion manualRotation;
    }

    [Serializable]
    public class LiveTimelineKeyBillboardDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyBillboardData> { }

    [Serializable]
    public class LiveTimelineBillboardData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyBillboardDataList keys;
    }

    // multi-camera post track containers.
    [Serializable]
    public class LiveTimelineKeyMultiCameraPostFilmData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraPostFilm;

        public int filmMode;
        public int colorType;
        public float filmPower;
        public Vector2 filmOffsetParam;
        public Vector4 filmOptionParam;
        public Color color0;
        public Color color1;
        public Color color2;
        public Color color3;
        public float depthPower;
        public float DepthClip;
        public float RollAngle;
        public Vector2 FilmScale;
        public int layerMode;
        public int movieResId;
        public int movieFrameOffset;
        public float movieSpeed;
        public int colorBlend;
        public float colorBlendFactor;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
        public bool IsAdjustedBlinkLightColor;
        public int loopType;
        public int loopCount;
        public int loopExecutedCount;
        public int loopIntervalFrame;
        public bool isPasteLoopUnit;
        public bool isChangeLoopInterpolate;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostFilmDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraPostFilmData> { }

    [Serializable]
    public class LiveTimelineMultiCameraPostFilmData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraPostFilmDataList keys;
        public int MultiCameraNo;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostEffectBloomDiffusionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraPostEffectBloomDiffusion;

        public float bloomDofWeight;
        public float threshold;
        public float intensity;
        public float BloomBlurSize;
        public int BloomBlendMode;
        public float diffusionBlurSize;
        public float diffusionBright;
        public float diffusionThreshold;
        public float diffusionSaturation;
        public float diffusionContrast;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostEffectBloomDiffusionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraPostEffectBloomDiffusionData> { }

    [Serializable]
    public class LiveTimelineMultiCameraPostEffectBloomDiffusionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraPostEffectBloomDiffusionDataList keys;
        public int MultiCameraNo;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraColorCorrectionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraColorCorrection;

        public bool enable;
        public float saturation;
        public int mode;
        public bool selective;
        public Color keyColor;
        public Color targetColor;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraColorCorrectionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraColorCorrectionData> { }

    [Serializable]
    public class LiveTimelineMultiCameraColorCorrectionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraColorCorrectionDataList keys;
        public int MultiCameraNo;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraTiltShiftData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraTiltShift;

        public int mode;
        public int quality;
        public float blurArea;
        public float maxBlurSize;
        public int downsample;
        public float roll;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraTiltShiftDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraTiltShiftData> { }

    [Serializable]
    public class LiveTimelineMultiCameraTiltShiftData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraTiltShiftDataList keys;
        public int MultiCameraNo;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraRadialBlurData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraRadialBlur;

        public int moveBlurType;
        public int radialBlurDownsample;
        public float radialBlurStartArea;
        public float radialBlurEndArea;
        public float radialBlurPower;
        public int radialBlurIteration;
        public float radialBlurRollEulerAngles;
        public float depthPowerFront;
        public float depthPowerBack;
        public float depthCancelBlendLength;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraRadialBlurDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraRadialBlurData> { }

    [Serializable]
    public class LiveTimelineMultiCameraRadialBlurData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraRadialBlurDataList keys;
        public int MultiCameraNo;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostEffectDOFData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraPostEffectDOF;

        public float forcalSize;
        public float blurSpread;
        public int charactor;
        public int dofBlurType;
        public int dofQuality;
        public float dofForegroundSize;
        public float dofFocalPoint;
        public float dofSmoothness;
        public float BallBlurPowerFactor;
        public float BallBlurBrightnessThreshhold;
        public float BallBlurBrightnessIntensity;
        public float BallBlurSpread;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostEffectDOFDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraPostEffectDOFData> { }

    [Serializable]
    public class LiveTimelineMultiCameraPostEffectDOFData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraPostEffectDOFDataList keys;
        public int MultiCameraNo;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraTransmittedLightData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraTransmittedLight;

        public int Iterations;
        public float Intensity;
        public float Threshold;
        public float BlurSpread;
        public int BlendMode;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraTransmittedLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraTransmittedLightData> { }

    [Serializable]
    public class LiveTimelineMultiCameraTransmittedLightData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraTransmittedLightDataList keys;
        public int MultiCameraNo;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraLayerData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraLayerDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraLayerData> { }

    [Serializable]
    public class LiveTimelineMultiCameraLayerData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraLayerDataList keys;
        public int MultiCameraNo;
    }

    // extra per-stage lights.
    [Serializable]
    public class LiveTimelineKeyAdditionalLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.AdditionalLight;
        public Vector3 Position;
        public Vector3 Rotate;
        public bool IsEnable;
        public int Type;
        public float Range;
        public int SpotAngle;
        public int IndirectMultiplier;
        public int ShadowType;
        public float Strength;
        public float Bias;
        public float NormalBias;
        public float NearPlane;
    }

    [Serializable]
    public class LiveTimelineKeyAdditionalLightList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyAdditionalLightData> { }

    [Serializable]
    public class LiveTimelineAdditionalLight : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyAdditionalLightList keys;
    }

    // multi-light fake shadow track.
    [Serializable]
    public class LiveTimelineKeyMultiLightShadowData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiLightShadow;
        public bool IsUseParam;
        public Vector3 ShadowCenterPosition;
        public Vector3 ShadowForward;
        public float ShadowFadeStart;
        public float ShadowFadeLength;
        public bool IsUseShadowFront;
        public bool IsShadowNearStart;
        public Color ShadowStartColor;
        public Color ShadowEndColor;
        public float ShadowDistance;
        public bool IsDarkestShadowOnly;
    }

    [Serializable]
    public class LiveTimelineKeyMultiLightShadowDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiLightShadowData> { }

    // per-group crowd + penlight choreography (renamed tracks with group index).
    [Serializable]
    public class LiveTimelineKeyMobControlData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MobControl;
        public Vector3 Position;
        public Vector3 Angle;
        public Vector3 Scale;
        public Quaternion Rotation;
    }

    [Serializable]
    public class LiveTimelineKeyMobControlDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMobControlData> { }

    [Serializable]
    public class LiveTimelineMobControlData : ILiveTimelineGroupDataWithName
    {
        public int GroupIndex;
        public LiveTimelineKeyMobControlDataList Keys;
    }

    [Serializable]
    public class LiveTimelineKeyCyalumeControlData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CyalumeControl;
        public Vector3 Position;
        public Vector3 Angle;
        public Vector3 Scale;
        public Quaternion Rotation;
    }

    [Serializable]
    public class LiveTimelineKeyCyalumeControlDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCyalumeControlData> { }

    [Serializable]
    public class LiveTimelineCyalumeControlData : ILiveTimelineGroupDataWithName
    {
        public int GroupIndex;
        public LiveTimelineKeyCyalumeControlDataList Keys;
    }

    // authored wind on hair/cloth per character slot.
    [Serializable]
    public class LiveTimelineKeyCharaWindData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CharaWind;
        public bool IsEnableWind;
        public CySpringWindParam WindParam;
    }

    [Serializable]
    public class LiveTimelineKeyCharaWindDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaWindData> { }

    [Serializable]
    public class LiveTimelineCharaWindData : ILiveTimelineGroupData
    {
        public const int DATA_LIST_SIZE = 20;
        public LiveTimelineKeyCharaWindDataList centerKeys;
        public LiveTimelineKeyCharaWindDataList left1Keys;
        public LiveTimelineKeyCharaWindDataList right1Keys;
        public LiveTimelineKeyCharaWindDataList left2Keys;
        public LiveTimelineKeyCharaWindDataList right2Keys;
        public LiveTimelineKeyCharaWindDataList place06Keys;
        public LiveTimelineKeyCharaWindDataList place07Keys;
        public LiveTimelineKeyCharaWindDataList place08Keys;
        public LiveTimelineKeyCharaWindDataList place09Keys;
        public LiveTimelineKeyCharaWindDataList place10Keys;
        public LiveTimelineKeyCharaWindDataList place11Keys;
        public LiveTimelineKeyCharaWindDataList place12Keys;
        public LiveTimelineKeyCharaWindDataList place13Keys;
        public LiveTimelineKeyCharaWindDataList place14Keys;
        public LiveTimelineKeyCharaWindDataList place15Keys;
        public LiveTimelineKeyCharaWindDataList place16Keys;
        public LiveTimelineKeyCharaWindDataList place17Keys;
        public LiveTimelineKeyCharaWindDataList place18Keys;
        public LiveTimelineKeyCharaWindDataList place19Keys;
        public LiveTimelineKeyCharaWindDataList place20Keys;
    }

    // character part visibility per position slot.
    [Serializable]
    public class LiveTimelineKeyCharaPartsData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CharaParts;
        public List<string> rendererNames;
        public List<int> rendererHashes;
        public List<bool> rendererVisibles;
    }

    [Serializable]
    public class LiveTimelineKeyCharaPartsDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaPartsData> { }

    [Serializable]
    public class LiveTimelineCharaPartsData : ILiveTimelineGroupData
    {
        public const int DATA_LIST_SIZE = 20;
        public LiveTimelineKeyCharaPartsDataList centerKeys;
        public LiveTimelineKeyCharaPartsDataList left1Keys;
        public LiveTimelineKeyCharaPartsDataList right1Keys;
        public LiveTimelineKeyCharaPartsDataList left2Keys;
        public LiveTimelineKeyCharaPartsDataList right2Keys;
        public LiveTimelineKeyCharaPartsDataList place06Keys;
        public LiveTimelineKeyCharaPartsDataList place07Keys;
        public LiveTimelineKeyCharaPartsDataList place08Keys;
        public LiveTimelineKeyCharaPartsDataList place09Keys;
        public LiveTimelineKeyCharaPartsDataList place10Keys;
        public LiveTimelineKeyCharaPartsDataList place11Keys;
        public LiveTimelineKeyCharaPartsDataList place12Keys;
        public LiveTimelineKeyCharaPartsDataList place13Keys;
        public LiveTimelineKeyCharaPartsDataList place14Keys;
        public LiveTimelineKeyCharaPartsDataList place15Keys;
        public LiveTimelineKeyCharaPartsDataList place16Keys;
        public LiveTimelineKeyCharaPartsDataList place17Keys;
        public LiveTimelineKeyCharaPartsDataList place18Keys;
        public LiveTimelineKeyCharaPartsDataList place19Keys;
        public LiveTimelineKeyCharaPartsDataList place20Keys;
    }

    // character collision capsules for cloth.
    [Serializable]
    public class LiveTimelineKeyCharaCollisionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CharaCollision;
        public bool IsEnable;
        public Vector3 Position;
        public Vector3 Angle;
        public int CollisionType;
        public float Radius;
        public Vector3 Offset;
        public Vector3 Offset2;
        public bool IsInner;
        public bool IsApplyHead;
        public bool IsApplyTail;
        public LiveCharaPositionFlag CharacterFlag;
        public Quaternion Rotation;
        public int ATTR_PLANE_UP;
    }

    [Serializable]
    public class LiveTimelineKeyCharaCollisionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaCollisionData> { }

    [Serializable]
    public class LiveTimelineCharaCollisionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyCharaCollisionDataList keys;
    }

    // voice line triggers.
    [Serializable]
    public class LiveTimelineKeyVoiceData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Voice;
        public int CueId;
    }

    [Serializable]
    public class LiveTimelineKeyVoiceDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyVoiceData> { }

    // transparent camera overlays.
    [Serializable]
    public class LiveTimelineKeyTransparentCameraData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public bool Enable;
        public bool IsOrthographic;
        public bool IsDefaultTransform;
        public float OrthographicSize;
    }

    [Serializable]
    public class LiveTimelineKeyTransparentCameraDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyTransparentCameraData> { }

    // mini-character pip cameras.
    [Serializable]
    public class LiveTimelineKeyMiniCharaCameraData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public bool Enable;
        public float OrthographicSize;
    }

    [Serializable]
    public class LiveTimelineKeyMiniCharaCameraDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMiniCharaCameraData> { }

    [Serializable]
    public class LiveTimelineKeyMiniCharaMotionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public AnimationClip BodyAnimationClip;
        public int BodyHeadFrame;
        public float BodyPlaySpeed;
        public AnimationClip FacialAnimation;
        public int FacialHeadFrame;
        public float FacialPlaySpeed;
        public AnimationClip EarAnimation;
        public int EarHeadFrame;
        public float EarPlaySpeed;
        public AnimationClip PositionAnimationClip;
        public int PositionHeadFrame;
        public float PositionPlaySpeed;
        public bool Loop;
    }

    [Serializable]
    public class LiveTimelineKeyMiniCharaMotionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMiniCharaMotionData> { }

    [Serializable]
    public class LiveTimelineMiniCharaMotionData : ILiveTimelineGroupData
    {
        public LiveTimelineKeyMiniCharaMotionDataList keys;
        public int _index;
    }

    [Serializable]
    public class LiveTimelineKeyMiniCharaColorData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Event;
        public int TargetFlags;
        public int ColorType;
        public Color Color;
        public float ColorPower;
        public float OutlineWidthPower;
        public Color OutlineColor;
        public int OutlineColorBlend;
        public bool IsSyncBlinkLight;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
        public bool IsAdjustedBlinkLightColor;
        public LiveTimelineKeyLoopType _loopType;
    }

    [Serializable]
    public class LiveTimelineKeyMiniCharaColorDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMiniCharaColorData> { }

    [Serializable]
    public class LiveTimelineMiniCharaColorData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMiniCharaColorDataList keys;
    }

    // character node offset track (long tail; node payload kept opaque).
    [Serializable]
    public class LiveTimelineKeyCharaNodeOffsetData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.CharaNodeOffset;
        public bool _isUsePersonalityParam;
    }

    [Serializable]
    public class LiveTimelineKeyCharaNodeOffsetDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaNodeOffsetData> { }

    [Serializable]
    public class LiveTimelineCharaNodeOffsetData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyCharaNodeOffsetDataList keys;
        public int _characterIndex;
    }
}
