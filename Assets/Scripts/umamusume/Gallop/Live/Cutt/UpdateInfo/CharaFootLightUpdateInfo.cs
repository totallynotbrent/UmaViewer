using UnityEngine;

namespace Gallop.Live.Cutt
{
    // mirrors the game's CharaFootLightUpdateInfo field-for-field.
    public struct CharaFootLightUpdateInfo
    {
        public bool isValid;
        public int CharacterIndex;
        public float hightMax;
        public Color lightColor;
        public int LightBlendMode;
        public int Easing;
    }
}
