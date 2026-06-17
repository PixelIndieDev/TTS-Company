using BepInEx;
using System.IO;
using System.Reflection;

namespace TTS_Company.Components.Constants
{
    internal static class TTSConstants
    {
        // executable
        internal static readonly string TTS_COMPANY_EXECUTABLE_LOCATION = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // piper
        internal const string PIPER_EXE_NAME = "piper.exe";
        internal static readonly string PIPER_FOLDER_LOCATION = Path.Combine(TTS_COMPANY_EXECUTABLE_LOCATION, "PiperTTS");
        internal static readonly string PIPER_EXECUTABLE_LOCATION = Path.Combine(PIPER_FOLDER_LOCATION, PIPER_EXE_NAME);

        internal const int PIPER_SERVER_STARTUP_TIMEOUT_MS = 15000;
        internal const int PIPER_SERVER_REQUEST_TIMEOUT_MS = 30000;
        internal const int PIPER_SERVER_SHUTDOWN_TIMEOUT_MS = 2000;

        // TTS voices
        private const string TTS_VOICE_FOLDER = "TTS-Company-Voices";
        internal static readonly string TTS_VOICE_FOLDER_PREFIX = Path.Combine(Paths.PluginPath, TTS_VOICE_FOLDER);

        // TTS voice clips cache
        private const string TTS_VOICE_CACHE_SOUNDCLIPS_FOLDER = "TTS-Company-Voices-Cache";
        internal static readonly string TTS_VOICE_CACHE_SOUNDCLIPS_PATH = Path.Combine(TTS_COMPANY_EXECUTABLE_LOCATION, TTS_VOICE_CACHE_SOUNDCLIPS_FOLDER);

        // TTS timout
        // in seconds
        internal const float TTS_TIMEOUT_MINIMUM_TIME = 4.0f;

        internal const float TTS_TIMEOUT_BASE_BUFFER = 4.0f;
        internal const float TTS_TIMEOUT_PER_WORD_BUFFER = 0.05f;

        // errors
        // errors for in the TTSResult
        internal const string TTS_GENERATION_TO_AUDIO_CLIP_NO_SUCCESS = "OGG -> AudioClip conversion failed";
        internal const string TTS_GENERATION_OGG_FILE_CREATION_NO_SUCCESS = "Processes succeeded but no OGG file was produced";
        internal const string TTS_GENERATION_CANCELLED = "TTS generation was cancelled";

        // debug
        internal const string DEBUG_AUDIOSOURCE_NAME = "DEBUG_KEYBIND";
    }
}
