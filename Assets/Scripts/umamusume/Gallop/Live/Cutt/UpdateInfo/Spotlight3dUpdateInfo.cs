using UnityEngine;

namespace Gallop.Live.Cutt
{
    public struct Spotlight3dUpdateInfo
    {
        public bool isValid;

        public bool isActive;
        public Color color;
        public float colorPower;
        public float localHeight;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public Vector3 characterPosition;
        public int targetCameraType;
        public int targetCameraIndex;
        public string assetName;
        public int characterIndex;
    }
}
