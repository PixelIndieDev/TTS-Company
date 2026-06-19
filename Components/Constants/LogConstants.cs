using BepInEx.Logging;

namespace TTS_Company.Components.Constants
{
    internal static class LogConstants
    {
        internal readonly struct LogMessage
        {
            internal string Message { get; }
            internal LogLevel Level { get; }

            internal LogMessage(LogLevel level, string message)
            {
                Level = level;
                Message = message;
            }

            internal void Log(params object[] args) => Plugin.logSource.Log(Level, string.Format(DEFAULT_ERROR_LOG_PREFIX + Message, args));
        }

        // -------------------- prefix --------------------
        private const string DEFAULT_ERROR_LOG_PREFIX = "{0} | "; // easier to see what class the error originated from

        // -------------------- general debug --------------------
        internal static readonly LogMessage CODE_TRIGGERED = new LogMessage(LogLevel.Debug, "{1} was triggered");
        internal static readonly LogMessage CODE_INPUT_VARIABLES_INVALID = new LogMessage(LogLevel.Debug, "In {1}, check {2} found invalid variables");
        internal static readonly LogMessage CODE_NEW_VALUE_SET = new LogMessage(LogLevel.Debug, "New value for {1} = {2}");
        internal static readonly LogMessage CODE_GENERIC_CANCELLED = new LogMessage(LogLevel.Debug, "{1} was cancelled");
        // errors
        internal static readonly LogMessage CODE_GENERIC_EXCEPTION = new LogMessage(LogLevel.Error, "{1} got exception: {2}");
        internal static readonly LogMessage CODE_GENERIC_ERROR = new LogMessage(LogLevel.Error, "{1} got error: {2}");
        internal static readonly LogMessage CODE_GENERIC_CATCH = new LogMessage(LogLevel.Warning, "{1} was catched");
        internal static readonly LogMessage CODE_GENERIC_FAIL = new LogMessage(LogLevel.Error, "{1} failed with error: {2}");

        // -------------------- TTS timeout helper --------------------
        // debug
        internal static readonly LogMessage TTS_TIMEOUT_HELPER_TIMEOUT_INFO = new LogMessage(LogLevel.Debug, "textToSpeak = {1}, totalTimeoutInSeconds = {2}");

        // -------------------- zip helper --------------------
        internal static readonly LogMessage ZIP_HELPER_MISSING_EXE = new LogMessage(LogLevel.Info, "{1} not found, now trying to unzip {2}.zip");
        // errors
        internal static readonly LogMessage ZIP_HELPER_RESOURCE_NULL = new LogMessage(LogLevel.Fatal, "The following resource was null: {1}");
        // debug
        internal static readonly LogMessage ZIP_HELPER_DELETED_FOLDER = new LogMessage(LogLevel.Debug, "{1} folder was found and was deleted");
        internal static readonly LogMessage ZIP_HELPER_EXE_EXISTS = new LogMessage(LogLevel.Debug, "{1} exists");

        // -------------------- TTS Generator --------------------
        internal static readonly LogMessage TTS_GENERATOR_FOUND_CACHED_TTS = new LogMessage(LogLevel.Info, "Found {1} in cache");
        internal static readonly LogMessage TTS_GENERATOR_GENERATING_TTS = new LogMessage(LogLevel.Info, "Generating TTS for text: '{1}' with hash: '{2}'");
        internal static readonly LogMessage TTS_GENERATOR_TTS_CANCELLED = new LogMessage(LogLevel.Info, "TTS got cancelled for text: '{1}' with hash: '{2}'");
        // errors
        internal static readonly LogMessage TTS_GENERATOR_UNZIP_FAILED = new LogMessage(LogLevel.Fatal, "{1}.exe not found and could not be unzipped");
        internal static readonly LogMessage TTS_GENERATOR_PROCESS_FAILED_TO_STOP = new LogMessage(LogLevel.Error, "Process {1} failed to exit");
        internal static readonly LogMessage TTS_GENERATOR_FAILED_TO_DELETE_0KB_CACHE = new LogMessage(LogLevel.Error, "Failed to delete 0KB cache file with hash: '{1}' - {2}");
        internal static readonly LogMessage TTS_GENERATOR_FFMPEG_EXITED_PREMATURE = new LogMessage(LogLevel.Fatal, "FFmpeg exited prematurely");
        internal static readonly LogMessage TTS_GENERATOR_NO_CACHED_AUDIO_FOUND = new LogMessage(LogLevel.Error, "Cached audio file with hash: '{1}' not found at: {2}");
        // debug
        internal static readonly LogMessage TTS_GENERATOR_RUN_PIPER_ARGUMENTS = new LogMessage(LogLevel.Debug, "Arguments for {1} are: {2}");
        internal static readonly LogMessage TTS_GENERATOR_DELETE_0KB_CACHE = new LogMessage(LogLevel.Debug, "Deleting 0KB cache file with hash: '{1}'");

        // -------------------- plugin --------------------
        internal static readonly LogMessage PLUGIN_LOADED = new LogMessage(LogLevel.Info, "{1} + (version - {2}) : loaded successfully");
        internal static readonly LogMessage PLUGIN_ON_QUIT = new LogMessage(LogLevel.Info, "Game wants to quit, stopping background processes of {1} + (version - {2})");
        // errors
        internal static readonly LogMessage PLUGIN_TTS_COULD_NOT_BE_INITIALIZED = new LogMessage(LogLevel.Fatal, "{1} could not be initialized");

        // -------------------- TTS Company API --------------------
        internal static readonly LogMessage API_NETWORK_OBJECT_NOT_FOUND = new LogMessage(LogLevel.Info, "NetworkObject {1} not found");

        // -------------------- Piper TTS Server --------------------
        internal static readonly LogMessage PIPER_TTS_SERVER_SUCCESS_STARTUP = new LogMessage(LogLevel.Info, "Started piper tts server on port {1} (pid {2})");
        internal static readonly LogMessage PIPER_TTS_SERVER_STOPPED = new LogMessage(LogLevel.Info, "Stopped piper tts server");
        internal static readonly LogMessage PIPER_TTS_LOADED_VOICE_MODEL = new LogMessage(LogLevel.Info, "Loaded voice model '{1}' using {2}");
        // errors
        internal static readonly LogMessage PIPER_TTS_SERVER_EXE_NOT_FOUND = new LogMessage(LogLevel.Fatal, "Server executable not found at: {1}");
        internal static readonly LogMessage PIPER_TTS_SERVER_FAILED_TO_START = new LogMessage(LogLevel.Fatal, "Failed to start piper tts server process with exception: {1}");
        internal static readonly LogMessage PIPER_TTS_SERVER_VOICE_FOLDER_NOT_FOUND = new LogMessage(LogLevel.Fatal, "Voice model directory not found at: {1}");
        internal static readonly LogMessage PIPER_TTS_SERVER_STARTUP_ISSUE = new LogMessage(LogLevel.Fatal, "Server process exited during startup (exit code {1} | stderr: {2})");
        internal static readonly LogMessage PIPER_TTS_SERVER_OUTPUT_DRAIN = new LogMessage(LogLevel.Warning, "Piper tts server drain: {1}");
        internal static readonly LogMessage PIPER_TTS_VOICE_MODEL_NOT_LOADED = new LogMessage(LogLevel.Warning, "Voice model '{1}' was not loaded beforehand as it has 0 assemblies that want it");

        // -------------------- voice model memory manager --------------------
        internal static readonly LogMessage VOICE_MODEL_MEM_MANAGER_POOL_LIMIT_REACHED = new LogMessage(LogLevel.Info, "Memory pool limit reached, unloaded {1}");
        // errors
        internal static readonly LogMessage VOICE_MODEL_MEM_MANAGER_NO_MODEL_TO_EVICT = new LogMessage(LogLevel.Error, "Memory pool exceeded, but no models available to evict for {1}");
        // debug
        internal static readonly LogMessage VOICE_MODEL_MEM_MANAGER_FOUND_VOICE_MODEL_WITH_SIZE = new LogMessage(LogLevel.Debug, "Found voice model named: '{1}' with file size: '{2}'");
    }
}
