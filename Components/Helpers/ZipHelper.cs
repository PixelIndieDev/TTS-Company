using System.IO;
using System.IO.Compression;
using System.Reflection;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components.Helpers
{
    internal static class ZipHelper
    {
        private static Assembly assembly = Assembly.GetExecutingAssembly();

        internal static bool CheckForPiperTTS() // return true when succesfully loaded or created and loaded
        {
            if (!File.Exists(TTSConstants.PIPER_EXECUTABLE_LOCATION))
            {
                LogConstants.ZIP_HELPER_MISSING_EXE.Log(nameof(ZipHelper), TTSConstants.PIPER_EXE_NAME, "piperTTS");

                // delete existing folder, start fresh
                if (Directory.Exists(TTSConstants.PIPER_FOLDER_LOCATION))
                {
                    Directory.Delete(TTSConstants.PIPER_FOLDER_LOCATION, recursive: true);
                    LogConstants.ZIP_HELPER_DELETED_FOLDER.Log(nameof(ZipHelper), "PiperTTS");
                }

                Directory.CreateDirectory(TTSConstants.PIPER_FOLDER_LOCATION);

                const string resourcePath = "TTS_Company.Assets.piperTTS.zip";
                using (Stream resource = assembly.GetManifestResourceStream(resourcePath))
                {
                    if (resource == null)
                    {
                        LogConstants.ZIP_HELPER_RESOURCE_NULL.Log(nameof(ZipHelper), resourcePath);
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
    }
}
