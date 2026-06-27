using System.IO;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components.Helpers
{
    internal static class ZipHelper
    {
        internal static bool CheckForPiperTTS() // return true when succesfully loaded or created and loaded
        {
            return File.Exists(TTSConstants.PIPER_EXECUTABLE_LOCATION);
        }

        internal static bool CheckForDefaultVoiceModels() // return true when succesfully loaded or created and loaded
        {
            return Directory.Exists(TTSConstants.TTS_DEFAULT_VOICE_MODELS_FOLDER_LOCATION);
        }
    }
}
