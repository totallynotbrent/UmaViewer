using UnityEngine;

namespace Gallop.Live.Cutt
{
    // mirrors the game's facial toon update state; applied per character.
    public struct FacialToonUpdateInfo
    {
        public bool isValid;
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
}
