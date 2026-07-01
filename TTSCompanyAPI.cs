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
    public static class TTSCompanyAPI
    {
        private static readonly PiperVoiceSettings DefaultVoiceSettings = new PiperVoiceSettings();
        private static readonly TTSAudioSourceSettings DefaultTTSAudioSourceSettings = new TTSAudioSourceSettings();

        // -------------------- preload voice models --------------------
        // client side
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<(bool Success, string Error)> PreloadTTSVoiceModelInMemory(string voiceModelName)
        {
            voiceModelName = VoiceHelper.CleanupVoiceModelname(voiceModelName);
            LogConstants.API_TRIGGER_PRELOAD_VOICE_MODEL.Log(nameof(TTSCompanyAPI), voiceModelName);

            ulong callingAssemblyHash = HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly());
            return await TTSCompanyPlugin._tts.PreloadVoiceAsync(voiceModelName, callingAssemblyHash);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<(bool Success, string Error)> UnloadTTSVoiceModelInMemory(string voiceModelName)
        {
            voiceModelName = VoiceHelper.CleanupVoiceModelname(voiceModelName);
            LogConstants.API_TRIGGER_UNLOAD_VOICE_MODEL.Log(nameof(TTSCompanyAPI), voiceModelName);

            ulong callingAssemblyHash = HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly());
            return await TTSCompanyPlugin._tts.UnloadVoiceAsync(voiceModelName, callingAssemblyHash);
        }

        // -------------------- add audio sources --------------------
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddTTSAudioSourceOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE, TTSAudioSourceSettings audioSourceSettings = APIDefaultsConstants.TTS_AUDIO_SOURCE_SETTING_DEFAULT)
        {
            audioSourceSettings = audioSourceSettings ?? DefaultTTSAudioSourceSettings;
            ulong callerHash = useGlobalAudioSource ? HashHelper.GlobalCallerHash : HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly());

            if (LNetworkUtils.IsConnected) // is in-game, do normal server stuff
            {
                TTSCompanyNetworking.Request_Server_SpawnTTSSource(new SpawnTTSAudioSource_NET(networkObjectRefOfSpeaker, callerHash, audioSourceSettings));
            }
            else // is NOT in-game, so do it without networking
            {
                if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
                {
                    LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), networkObjectRefOfSpeaker);
                    return;
                }

                TTSAudioSourceManager.AddPermanentTTSAudioSource(networkObject.gameObject, callerHash, audioSourceSettings);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RemoveTTSAudioSourceOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker)
        {
            ulong callerHash = HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly());

            if (LNetworkUtils.IsConnected) // is in-game, do normal server stuff
            {
                TTSCompanyNetworking.Request_Server_DespawnTTSSource(new DespawnTTSAudioSource_NET(networkObjectRefOfSpeaker, callerHash));
            }
            else // is NOT in-game, so do it without networking
            {
                if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
                {
                    LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), networkObjectRefOfSpeaker);
                    return;
                }

                TTSAudioSourceManager.RemovePermanentTTSAudioSource(networkObject.gameObject, callerHash);
            }
        }

        // -------------------- speak tts --------------------
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string[] textsToSpeak, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT, TTSAudioSourceSettings audioSourceSettings = APIDefaultsConstants.TTS_AUDIO_SOURCE_SETTING_DEFAULT)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(SpeakTTSAtNetworkObject));

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return;
            }

            // if null, use the default
            voiceSettings = voiceSettings ?? DefaultVoiceSettings;

            ulong callingAHash;
            ulong trackingKeyHash;
            if (useGlobalAudioSource)
            {
                callingAHash = HashHelper.GlobalCallerHash;
                trackingKeyHash = HashHelper.GetTrackingKeyHash(networkObjectRefOfSpeaker.NetworkObjectId);
            } 
            else
            {
                Assembly callingA = Assembly.GetCallingAssembly();
                callingAHash = HashHelper.GetCallingAssemblyHash(callingA);
                trackingKeyHash = HashHelper.GetTrackingKeyHash(networkObjectRefOfSpeaker.NetworkObjectId, callingA);
            }

            TTSCompanyNetworking.Request_Server_SpeakTTS(new TTSSpeakTTS_NET(networkObjectRefOfSpeaker, callingAHash, textsToSpeak, voiceSettings, audioSourceSettings, trackingKeyHash));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string textToSpeak, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT, TTSAudioSourceSettings audioSourceSettings = APIDefaultsConstants.TTS_AUDIO_SOURCE_SETTING_DEFAULT)
        {
            SpeakTTSAtNetworkObject(networkObjectRefOfSpeaker, TTSCompanyUtils.SplitTextToSpeak(textToSpeak), useGlobalAudioSource, voiceSettings, audioSourceSettings);
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

            ulong trackingKeyHash = HashHelper.GetTrackingKeyHash(string.Join("|", textsToSpeak), voiceSettings);

            CancellationTokenSource ttsCts = new CancellationTokenSource(TTSTimeoutHelper.GetGenerationTimeout(textsToSpeak, voiceSettings));
            TTSCompanyPlugin.instance.StartCoroutine(TTSCompanyBackend.PreGenerateTTS(trackingKeyHash, textsToSpeak, voiceSettings, ttsCts));
        }
    }
}
