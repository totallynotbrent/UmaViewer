using UnityEngine;

namespace Gallop.Live.Cutt
{
    public struct PropsUpdateInfo
    {
        public bool isValid;

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
    }
}
