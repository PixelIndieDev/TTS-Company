using System.IO;
using System.IO.Compression;
using System.Reflection;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components.Helpers
{
    internal static class ZipHelper
    {
        private static Assembly assembly = Assembly.GetExecutingAssembly();

        private const string zipName_piper = "piperTTS";
        private const string zipName_voiceModels = "voiceModels";

        private readonly static string resourcePath_piper = $"TTS_Company.Assets.{zipName_piper}.zip";
        private readonly static string resourcePath_voiceModels = $"TTS_Company.Assets.{zipName_voiceModels}.zip";

        internal static bool CheckForPiperTTS() // return true when succesfully loaded or created and loaded
        {
            if (!File.Exists(TTSConstants.PIPER_EXECUTABLE_LOCATION))
            {
                LogConstants.ZIP_HELPER_MISSING_EXE.Log(nameof(ZipHelper), TTSConstants.PIPER_EXE_NAME, zipName_piper);

                // delete existing folder, start fresh
                if (Directory.Exists(TTSConstants.PIPER_FOLDER_LOCATION))
                {
                    Directory.Delete(TTSConstants.PIPER_FOLDER_LOCATION, recursive: true);
                    LogConstants.ZIP_HELPER_DELETED_FOLDER.Log(nameof(ZipHelper), zipName_piper);
                }

                Directory.CreateDirectory(TTSConstants.PIPER_FOLDER_LOCATION);

                using (Stream resource = assembly.GetManifestResourceStream(resourcePath_piper))
                {
                    if (resource == null)
                    {
                        LogConstants.ZIP_HELPER_RESOURCE_NULL.Log(nameof(ZipHelper), resourcePath_piper);
                        return false;
                    }

                    using (ZipArchive archive = new ZipArchive(resource, ZipArchiveMode.Read))
                    {
                        archive.ExtractToDirectory(TTSConstants.PIPER_FOLDER_LOCATION);
                    }
                }

                return File.Exists(TTSConstants.PIPER_EXECUTABLE_LOCATION); // return if it now exists
            }
            else
            {
                LogConstants.ZIP_HELPER_EXE_EXISTS.Log(nameof(ZipHelper), TTSConstants.PIPER_EXE_NAME);
                return true; // file already exists
            }
        }

        internal static bool CheckForDefaultVoiceModels() // return true when succesfully loaded or created and loaded
        {
            if (!Directory.Exists(TTSConstants.TTS_VOICE_MODELS_FOLDER_LOCATION))
            {
                LogConstants.ZIP_HELPER_MISSING_EXE.Log(nameof(ZipHelper), TTSConstants.TTS_VOICE_MODELS_FOLDER, zipName_voiceModels);

                Directory.CreateDirectory(TTSConstants.TTS_VOICE_MODELS_FOLDER_LOCATION);

                using (Stream resource = assembly.GetManifestResourceStream(resourcePath_voiceModels))
                {
                    if (resource == null)
                    {
                        LogConstants.ZIP_HELPER_RESOURCE_NULL.Log(nameof(ZipHelper), resourcePath_voiceModels);
                        return false;
                    }

                    using (ZipArchive archive = new ZipArchive(resource, ZipArchiveMode.Read))
                    {
                        archive.ExtractToDirectory(TTSConstants.TTS_VOICE_MODELS_FOLDER_LOCATION);
                    }
                }

                return Directory.Exists(TTSConstants.TTS_VOICE_MODELS_FOLDER_LOCATION); // return if it now exists
            }
            else
            {
                LogConstants.ZIP_HELPER_EXE_EXISTS.Log(nameof(ZipHelper), zipName_voiceModels);
                return true; // file already exists
            }
        }
    }
}
