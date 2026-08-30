using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PostFilmUpdateInfo
    {
        public string TimelineName;
        public int TimelineNameHash;

        public PostFilmMode filmMode;
        public PostColorType colorType;
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
        public LiveTimelineKeyLoopType loopType;
        public int loopCount;
        public int loopExecutedCount;
        public int loopIntervalFrame;
        public bool isPasteLoopUnit;
        public bool isChangeLoopInterpolate;
        public LiveTimelineKeyPostFilmData.LayerMode layerMode;
        public int movieResId;
        public int movieFrameOffset;
        public float movieSpeed;
        public LiveTimelineKeyPostFilmData.ColorBlend colorBlend;
        public float colorBlendFactor;
    }

    public delegate void PostFilmUpdateInfoDelegate(
        LiveTimelineKeyPostFilmData data,
        ref PostFilmUpdateInfo updateInfo,
        float currentLiveTime);
}