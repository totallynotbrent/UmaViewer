namespace Gallop.Live.Cutt
{
    // mirrors the game's LensDistortionUpdateInfo field-for-field.
    public struct LensDistortionUpdateInfo
    {
        public bool isValid;
        public float Intensity;
        public float IntensityX;
        public float IntensityY;
        public float CenterX;
        public float CenterY;
        public float Scale;
    }
}
