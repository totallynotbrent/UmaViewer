using UnityEngine;

namespace Gallop.Live.Cutt
{
    // mirrors the game's AdditionalLightUpdateInfo field-for-field.
    public struct AdditionalLightUpdateInfo
    {
        public bool isValid;
        public int Index;
        public Vector3 Position;
        public Vector3 Rotate;
        public bool IsEnable;
        public LightType Type;
        public float Range;
        public int SpotAngle;
        public int IndirectMultiplier;
        public LightShadows ShadowType;
        public float Strength;
        public float Bias;
        public float NormalBias;
        public float NearPlane;
    }
}
