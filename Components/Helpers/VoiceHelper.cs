using System;

namespace TTSCompany.Components.Helpers
{
    internal static class VoiceHelper
    {
        internal static string CleanupVoiceModelname(string voiceModelname)
        {
            string cleanedValue = voiceModelname != null && voiceModelname.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ? voiceModelname.Substring(0, voiceModelname.Length - 5) : voiceModelname;

            if (cleanedValue != null)
            {
                return cleanedValue;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
