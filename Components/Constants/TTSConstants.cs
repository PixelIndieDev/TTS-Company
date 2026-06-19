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
        internal const string PIPER_EXE_NAME = "piper-server.exe";
        internal static readonly string PIPER_FOLDER_LOCATION = Path.Combine(TTS_COMPANY_EXECUTABLE_LOCATION, "PiperTTS");
        internal static readonly string PIPER_EXECUTABLE_LOCATION = Path.Combine(PIPER_FOLDER_LOCATION, PIPER_EXE_NAME);

        internal const int PIPER_SERVER_STARTUP_TIMEOUT_MS = 15000;
        internal const int PIPER_SERVER_REQUEST_TIMEOUT_MS = 30000;
        internal const int PIPER_SERVER_SHUTDOWN_TIMEOUT_MS = 2000;

        // TTS voices
        internal const string TTS_VOICE_MODELS_FOLDER = "TTS-Company-Voices";
        internal static readonly string TTS_VOICE_MODELS_FOLDER_LOCATION = Path.Combine(Paths.PluginPath, TTS_VOICE_MODELS_FOLDER);

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
        internal const string TTS_SERVER_UNAVAILABLE = "TTS server is not available";
        internal const string TTS_VOICE_MODEL_NAME_EMPTY = "voice model name must not be empty";

        internal const string TTS_VALI_TEXT_TO_SPEAK = "TTS text cannot be empty";
        internal const string TTS_VALI_SETTINGS = "PiperVoiceSettings cannot be NULL";
        internal const string TTS_VALI_MODEL_NAME = "Voice model name must be set in settings";
        internal const string TTS_VALI_MODEL_INVALID = "Piper voice model not found or valid";
        internal const string TTS_VALI_SPEECH_RATE = "Speech rate must be > 0";

        internal const string TTS_MEM_MANAGER_UNKNOWN_ASSEMBLY = "Could not determine calling assembly";

        // debug
        internal const string DEBUG_AUDIOSOURCE_NAME = "DEBUG_KEYBIND";
    }
}
