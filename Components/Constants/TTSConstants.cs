using System.IO;
using System.Reflection;
using TTS_Company.Components.Enums;

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
        internal static readonly string TTS_DEFAULT_VOICE_MODELS_FOLDER_LOCATION = Path.Combine(TTS_COMPANY_EXECUTABLE_LOCATION, TTS_VOICE_MODELS_FOLDER);

        // TTS voice clips cache
        private const string TTS_VOICE_CACHE_SOUNDCLIPS_FOLDER = "TTS-Company-Voices-Cache";
        internal static readonly string TTS_VOICE_CACHE_SOUNDCLIPS_PATH = Path.Combine(TTS_COMPANY_EXECUTABLE_LOCATION, TTS_VOICE_CACHE_SOUNDCLIPS_FOLDER);

        // TTS Task
        internal const float TTS_START_SPEAKING_AT_MULTIPLIER = 0.35f; // 35%
        internal const int TTS_MINIMUM_START_INDEX = 0;
        internal const int TTS_MAXIMUM_START_INDEX = 3; // capped at 'TTS_MAXIMUM_START_INDEX' makes it so that the tts doesn't lock up too much (with the talking part) when requested an entire novel

        // TTS timout
        // in seconds
        internal const float TTS_TIMEOUT_MINIMUM_TIME = 4.0f;

        private const float TTS_TIMEOUT_BASE_BUFFER = 4.0f;
        private const float TTS_TIMEOUT_PER_WORD_BUFFER = 0.05f;
        private const float TTS_PLAYBACK_TIMEOUT_BUFFER = 1.4f;

        internal static float TTS_TIMEOUT_BASE_BUFFER_SCALED = 0.0f;
        internal static float TTS_TIMEOUT_PER_WORD_BUFFER_SCALED = 0.0f;
        internal static float TTS_PLAYBACK_TIMEOUT_BUFFER_SECONDS_SCALED = 0.0f;

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
        internal const string TTS_MEM_MANAGER_UNKNOWN_MODEL_LOCATION = "Voice model file location value not found";

        // debug
        internal const string DEBUG_AUDIOSOURCE_NAME = "DEBUG_KEYBIND";

        internal static void UpdateTimeoutBuffers()
        {
            TTS_TIMEOUT_BASE_BUFFER_SCALED = TTS_TIMEOUT_BASE_BUFFER * GetTimeoutBufferScaling(true);
            TTS_TIMEOUT_PER_WORD_BUFFER_SCALED = TTS_TIMEOUT_PER_WORD_BUFFER * GetTimeoutBufferScaling(false);
            TTS_PLAYBACK_TIMEOUT_BUFFER_SECONDS_SCALED = TTS_PLAYBACK_TIMEOUT_BUFFER * GetTimeoutBufferScaling(true);
        }

        private static float GetTimeoutBufferScaling(bool isBase)
        {
            switch (TTSCompanyPlugin.configEntryTimeoutBuffer.Value)
            {
                case TimeoutBufferScaling.Low:
                    return isBase ? 0.6f : 0.95f;
                default: // normal priority
                    return isBase ? 1.0f : 1.0f;
                case TimeoutBufferScaling.High:
                    return isBase ? 1.4f : 1.1f;
                case TimeoutBufferScaling.Max:
                    return isBase ? 1.8f : 1.2f;
            }
        }

        internal static float GetGenerationDurationScaling()
        {
            switch (TTSCompanyPlugin.configEntryTimeoutBuffer.Value)
            {
                case TimeoutBufferScaling.Low:
                    return 0.95f;
                default: // normal priority
                    return 1.0f;
                case TimeoutBufferScaling.High:
                    return 1.4f;
                case TimeoutBufferScaling.Max:
                    return 2.0f;
            }
        }
    }
}
