using System.IO;

namespace TTS_Company.Components.Constants
{
    internal static class FFmpegConstants
    {
        internal const string FFMPEG_EXE_NAME = "ffmpeg-ogg-only.exe";
        internal static readonly string FFMPEG_FOLDER_LOCATION = Path.Combine(TTSConstants.TTS_COMPANY_EXECUTABLE_LOCATION, "FFmpeg");
        internal static readonly string FFMPEG_EXE_FILE_LOCATION = Path.Combine(FFMPEG_FOLDER_LOCATION, FFMPEG_EXE_NAME);

        internal const int FFMPEG_OGG_QUALITY_LEVEL = 1; // Value 0 - 10
        internal const int FFMPEG_OGG_SAMPLE_RATE = 22050; // 22050 as it uses medium TTS quality models
        internal const int FFMPEG_OGG_CHANNELS_AMOUNT = 1; // TTS is always mono
    }
}
