using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TTSCompany.Components.Helpers
{
    internal static class HashHelper
    {
        private const string GlobalCallerName = "TTSCompanyGlobalAudioSourceCaller";
        internal static readonly ulong GlobalCallerHash = CalculateGlobalCallerHash();

        private const ulong Prime = 0x100000001b3;
        private const ulong OffsetBasis = 0xcbf29ce484222325;

        private static readonly ConcurrentDictionary<ulong, ulong> _assemblyHashCache = new ConcurrentDictionary<ulong, ulong>();

        internal static ulong GetTrackingKeyHash(ulong networkObjectId, Assembly callingAssembly)
        {
            unchecked
            {
                ulong assemblyHashUlong = GetCallingAssemblyHash(callingAssembly);
                if (!_assemblyHashCache.TryGetValue(assemblyHashUlong, out ulong hash))
                {
                    hash = assemblyHashUlong;
                    _assemblyHashCache.TryAdd(assemblyHashUlong, hash);
                }

                CombineULong(ref hash, networkObjectId);
                return hash;
            }
        }

        internal static ulong GetTrackingKeyHash(ulong networkObjectId)
        {
            unchecked
            {
                if (!_assemblyHashCache.TryGetValue(GlobalCallerHash, out ulong hash))
                {
                    hash = GlobalCallerHash;
                    _assemblyHashCache.TryAdd(GlobalCallerHash, hash);
                }

                CombineULong(ref hash, networkObjectId);
                return hash;
            }
        }

        internal static ulong GetTrackingKeyHash(string textToSpeak, PiperVoiceSettings settings)
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

                return hash;
            }
        }

        internal static ulong GetCallingAssemblyHash(Assembly callingAssembly)
        {
            unchecked
            {
                ulong hash = OffsetBasis;

                CombineString(ref hash, callingAssembly.GetName().Name);
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
                }

                return $"{hash:X16}.ogg";
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CombineString(ref ulong hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            for (int i = 0; i < value.Length; i++)
            {
                uint c = value[i];
                hash = (hash ^ (c & 0xFF)) * Prime;
                hash = (hash ^ (c >> 8)) * Prime;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CombineFloat(ref ulong hash, float value)
        {
            uint bits = Unsafe.As<float, uint>(ref value);
            hash = (hash ^ (bits & 0xFF)) * Prime;
            hash = (hash ^ ((bits >> 8) & 0xFF)) * Prime;
            hash = (hash ^ ((bits >> 16) & 0xFF)) * Prime;
            hash = (hash ^ (bits >> 24)) * Prime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CombineULong(ref ulong hash, ulong value)
        {
            hash = (hash ^ (value & 0xFF)) * Prime;
            hash = (hash ^ ((value >> 8) & 0xFF)) * Prime;
            hash = (hash ^ ((value >> 16) & 0xFF)) * Prime;
            hash = (hash ^ ((value >> 24) & 0xFF)) * Prime;
            hash = (hash ^ ((value >> 32) & 0xFF)) * Prime;
            hash = (hash ^ ((value >> 40) & 0xFF)) * Prime;
            hash = (hash ^ ((value >> 48) & 0xFF)) * Prime;
            hash = (hash ^ (value >> 56)) * Prime;
        }

        private static ulong CalculateGlobalCallerHash()
        {
            unchecked
            {
                ulong hash = OffsetBasis;

                CombineString(ref hash, GlobalCallerName);
                return hash;
            }
        }
    }
}
