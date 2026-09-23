namespace Gallop.Live.Cutt
{
    // voice cue trigger from the voice track; the game resolves the cue through its
    // sound manager, the viewer logs it once per cue.
    public struct VoiceUpdateInfo
    {
        public bool isValid;
        public int CueId;
        public float Time;
    }
}
