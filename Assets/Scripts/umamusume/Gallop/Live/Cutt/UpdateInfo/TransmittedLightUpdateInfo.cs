namespace Gallop.Live.Cutt
{
    // mirrors the game's TransmittedLightUpdateInfo field-for-field.
    public struct TransmittedLightUpdateInfo
    {
        public bool isValid;
        public bool IsEnabled;
        public int Iterations;
        public float Intensity;
        public float Threshold;
        public float BlurSpread;
        public float BlendMode;
    }
}
