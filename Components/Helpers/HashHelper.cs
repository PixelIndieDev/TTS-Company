using System.Runtime.InteropServices;

namespace TTS_Company.Components.Helpers
{
    internal static class HashHelper
    {
        private const ulong Prime = 0x100000001b3;
        private const ulong OffsetBasis = 0xcbf29ce484222325;

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatConverter
        {
            [FieldOffset(0)] public float FloatValue;
            [FieldOffset(0)] public int IntValue;
        }

        internal static ulong GetTrackingKeyHash( ulong networkObjectId, string audioSourceName)
        {
            unchecked
            {
                ulong hash = OffsetBasis;

                CombineULong(ref hash, networkObjectId);
                CombineString(ref hash, audioSourceName);

                return hash;
            }
        }

        internal static string GetHashTTSFileNameWithFileType(string textToSpeak, PiperVoiceSettings settings)
        {
            unchecked
            {
                ulong hash = OffsetBasis;

                CombineString(ref hash, textToSpeak);

                if (settings != null)
                {
                    CombineString(ref hash, settings.ModelName);
                    CombineFloat(ref hash, settings.SpeechRate);
                    CombineFloat(ref hash, settings.NoiseScale);
                    CombineFloat(ref hash, settings.NoiseScaleW);
                    CombineFloat(ref hash, settings.SentenceSilence);
                }

                return $"{hash:X16}.ogg";
            }
        }

        private static void CombineString(ref ulong hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                hash ^= (byte)(c & 0xFF);
                hash *= Prime;
                hash ^= (byte)(c >> 8);
                hash *= Prime;
            }
        }

        private static void CombineFloat(ref ulong hash, float value)
        {
            // Multply by 10 and round to nearest integer
            // Example: 1.234f -> 12.34f -> 12
            int roundedBits = UnityEngine.Mathf.RoundToInt(value * 10f);

            hash ^= (byte)(roundedBits & 0xFF);
            hash *= Prime;
            hash ^= (byte)((roundedBits >> 8) & 0xFF);
            hash *= Prime;
            hash ^= (byte)((roundedBits >> 16) & 0xFF);
            hash *= Prime;
            hash ^= (byte)((roundedBits >> 24) & 0xFF);
            hash *= Prime;
        }

        private static void CombineULong(ref ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (byte)(value & 0xFF);
                hash *= Prime;
                value >>= 8;
            }
        }
    }
}
