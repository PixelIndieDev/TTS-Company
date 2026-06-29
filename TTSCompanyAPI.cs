using LethalNetworkAPI.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TTS_Company.Components;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Helpers;
using TTS_Company.Components.Managers;
using TTS_Company.Components.Networking;
using TTS_Company.Components.Networking.Components.Structs;
using Unity.Netcode;

namespace TTS_Company
{
    internal static class TTSCompanyAPI
    {
        private static readonly PiperVoiceSettings DefaultVoiceSettings = new PiperVoiceSettings();
        private static readonly TTSAudioSourceSettings DefaultTTSAudioSourceSettings = new TTSAudioSourceSettings();

        // -------------------- preload voice models --------------------
        // client side
        public static async Task<(bool Success, string Error)> PreloadTTSVoiceModelInMemory(string voiceModelName)
        {
            voiceModelName = VoiceHelper.CleanupVoiceModelname(voiceModelName);
            LogConstants.API_TRIGGER_PRELOAD_VOICE_MODEL.Log(nameof(TTSCompanyAPI), voiceModelName);
            return await TTSCompanyPlugin._tts.PreloadVoiceAsync(voiceModelName);
        }

        public static async Task<(bool Success, string Error)> UnloadTTSVoiceModelInMemory(string voiceModelName)
        {
            voiceModelName = VoiceHelper.CleanupVoiceModelname(voiceModelName);
            LogConstants.API_TRIGGER_UNLOAD_VOICE_MODEL.Log(nameof(TTSCompanyAPI), voiceModelName);
            return await TTSCompanyPlugin._tts.UnloadVoiceAsync(VoiceHelper.CleanupVoiceModelname(voiceModelName));
        }

        // -------------------- add audio sources --------------------
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddTTSAudioSourceOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, TTSAudioSourceSettings audioSourceSettings = null)
        {
            audioSourceSettings = audioSourceSettings ?? DefaultTTSAudioSourceSettings;

            if (LNetworkUtils.IsConnected) // is in-game, do normal server stuff
            {
                TTSCompanyNetworking.Request_Server_SpawnTTSSource(new SpawnTTSAudioSource_NET(networkObjectRefOfSpeaker, HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly()), audioSourceSettings));
            }
            else // is NOT in-game, so do it without networking
            {
                if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
                {
                    LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), networkObjectRefOfSpeaker);
                    return;
                }
                TTSAudioSourceManager.AddPermanentTTSAudioSource(networkObject.gameObject, HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly()), audioSourceSettings);
            }
        }

        // -------------------- speak tts --------------------
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string[] textsToSpeak, PiperVoiceSettings voiceSettings = null, TTSAudioSourceSettings audioSourceSettings = null)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(SpeakTTSAtNetworkObject));

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return;
            }

            // if null, use the default
            voiceSettings = voiceSettings ?? DefaultVoiceSettings;

            Assembly callingA = Assembly.GetCallingAssembly();
            ulong callingAHash = HashHelper.GetCallingAssemblyHash(callingA);
            ulong trackingKeyHash = HashHelper.GetTrackingKeyHash(networkObjectRefOfSpeaker.NetworkObjectId, callingA);

            TTSCompanyNetworking.Request_Server_SpeakTTS(new TTSSpeakTTS_NET(networkObjectRefOfSpeaker, callingAHash, textsToSpeak, voiceSettings, audioSourceSettings, trackingKeyHash));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string textToSpeak, PiperVoiceSettings voiceSettings = null, TTSAudioSourceSettings audioSourceSettings = null)
        {
            SpeakTTSAtNetworkObject(networkObjectRefOfSpeaker, TTSCompanyUtils.SplitTextToSpeak(textToSpeak), voiceSettings, audioSourceSettings);
        }

        // -------------------- generate TTS --------------------
        public static void PreGenerateTTS(string textToSpeak, PiperVoiceSettings voiceSettings = null)
        {
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                return;
            }
            PreGenerateTTS(TTSCompanyUtils.SplitTextToSpeak(textToSpeak), voiceSettings);
        }

        public static void PreGenerateTTS(string[] textsToSpeak, PiperVoiceSettings voiceSettings = null)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(PreGenerateTTS));

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return;
            }

            // if null, use the default
            voiceSettings = voiceSettings ?? DefaultVoiceSettings;

            Assembly callingA = Assembly.GetCallingAssembly();
            ulong callingAHash = HashHelper.GetCallingAssemblyHash(callingA);
            ulong trackingKeyHash = HashHelper.GetTrackingKeyHash(string.Join("|", textsToSpeak), voiceSettings);

            CancellationTokenSource ttsCts = new CancellationTokenSource(TTSTimeoutHelper.GetGenerationTimeout(textsToSpeak, voiceSettings));
            TTSCompanyPlugin.instance.StartCoroutine(TTSCompanyBackend.PreGenerateTTS(trackingKeyHash, textsToSpeak, voiceSettings, ttsCts));
        }
    }
}
