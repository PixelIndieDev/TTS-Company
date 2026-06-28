namespace TTS_Company.Components.Constants
{
    internal static class OggConstants
    {
        internal const int OGG_BITRATE = 16000;
        internal const int OGG_SAMPLE_RATE = 24000; // 22050 hz is outputted by medium TTS quality models, concentus can't handle that
        internal const int OGG_CHANNELS_AMOUNT = 1; // TTS is always mono
    }
}
