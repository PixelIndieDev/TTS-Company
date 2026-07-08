using LethalNetworkAPI.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TTSCompany.Components;
using TTSCompany.Components.Constants;
using TTSCompany.Components.Helpers;
using TTSCompany.Components.Managers;
using TTSCompany.Components.Networking;
using TTSCompany.Components.Networking.Components.Structs;
using Unity.Netcode;
using UnityEngine;

namespace TTSCompany
{
    public static class TTSCompanyAPI
    {
        private static readonly PiperVoiceSettings DefaultVoiceSettings = new PiperVoiceSettings();
        private static readonly TTSAudioSourceSettings DefaultTTSAudioSourceSettings = new TTSAudioSourceSettings();

        // -------------------- preload voice models --------------------
        // client side
        /// <summary>Asynchronously loads a TTS voice model into memory so it's ready to generate speech</summary>
        /// <param name="voiceModelName">The file name of the .onnx voice model to load</param>
        /// <returns>
        /// <list type="bullet">
        /// <item><description><c>Success</c>: <c>true</c> if the model loaded successfully, <c>false</c> otherwise</description></item>
        /// <item><description><c>Error</c>: <c>"ok"</c> if the operation succeeded, or an error message describing what went wrong</description></item>
        /// </list>
        /// </returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<(bool Success, string Error)> PreloadTTSVoiceModelInMemory(string voiceModelName)
        {
            voiceModelName = VoiceHelper.CleanupVoiceModelname(voiceModelName);
            LogConstants.API_TRIGGER_PRELOAD_VOICE_MODEL.Log(nameof(TTSCompanyAPI), voiceModelName);

            ulong callingAssemblyHash = HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly());
            return await TTSCompanyPlugin._tts.PreloadVoiceAsync(voiceModelName, callingAssemblyHash);
        }

        /// <summary>Asynchronously unloads a previously loaded TTS voice model from memory</summary>
        /// <param name="voiceModelName">The file name of the .onnx voice model to unload</param>
        /// <returns>
        /// <list type="bullet">
        /// <item><description><c>Success</c>: <c>true</c> if the model unloaded successfully, <c>false</c> otherwise</description></item>
        /// <item><description><c>Error</c>: <c>"ok"</c> if the operation succeeded, or an error message describing what went wrong</description></item>
        /// </list>
        /// </returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<(bool Success, string Error)> UnloadTTSVoiceModelInMemory(string voiceModelName)
        {
            voiceModelName = VoiceHelper.CleanupVoiceModelname(voiceModelName);
            LogConstants.API_TRIGGER_UNLOAD_VOICE_MODEL.Log(nameof(TTSCompanyAPI), voiceModelName);

            ulong callingAssemblyHash = HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly());
            return await TTSCompanyPlugin._tts.UnloadVoiceAsync(voiceModelName, callingAssemblyHash);
        }

        // -------------------- add audio sources --------------------
        /// <summary>Adds a TTS audio source component to a network object, allowing it to generate and play TTS audio</summary>
        /// <param name="objectRefOfSpeaker">The local GameObject instance that owns the TTS audio source</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        /// <param name="audioSourceSettings">A TTSAudioSourceSettings object controlling playback (volume, spatial blend, rolloff, etc.) for this audio source</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddTTSAudioSourceOnNetworkObject(GameObject objectRefOfSpeaker, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT, TTSAudioSourceSettings audioSourceSettings = APIDefaultsConstants.TTS_AUDIO_SOURCE_SETTING_DEFAULT)
        {
            if (objectRefOfSpeaker.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                NetworkObjectReference reference = new NetworkObjectReference(networkObject);
                AddTTSAudioSourceOnNetworkObject(reference, useGlobalAudioSource, audioSourceSettings);
            }
        }
        /// <summary>Adds a TTS audio source component to a network object, allowing it to generate and play TTS audio</summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the network object that owns the TTS audio source</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        /// <param name="audioSourceSettings">A TTSAudioSourceSettings object controlling playback (volume, spatial blend, rolloff, etc.) for this audio source</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddTTSAudioSourceOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT, TTSAudioSourceSettings audioSourceSettings = APIDefaultsConstants.TTS_AUDIO_SOURCE_SETTING_DEFAULT)
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

        // -------------------- remove audio sources --------------------
        /// <summary>Remove a TTS audio source component of a network object</summary>
        /// <param name="objectRefOfSpeaker">The local GameObject instance that owns the TTS audio source</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RemoveTTSAudioSourceOnNetworkObject(GameObject objectRefOfSpeaker)
        {
            if (objectRefOfSpeaker.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                NetworkObjectReference reference = new NetworkObjectReference(networkObject);
                RemoveTTSAudioSourceOnNetworkObject(reference);
            }
        }
        /// <summary>Remove a TTS audio source component of a network object</summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the network object that owns the TTS audio source</param>
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

        // -------------------- update audio sources --------------------
        /// <summary>Updates the audio source settings of a TTS audio source component on a network object</summary>
        /// <param name="objectRefOfSpeaker">The local GameObject instance that owns the TTS audio source</param>
        /// <param name="audioSourceSettings">A TTSAudioSourceSettings object controlling playback (volume, spatial blend, rolloff, etc.) for this audio source</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdateTTSAudioSourceSettingsOnNetworkObject(GameObject objectRefOfSpeaker, TTSAudioSourceSettings audioSourceSettings, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT)
        {
            if (objectRefOfSpeaker.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                NetworkObjectReference reference = new NetworkObjectReference(networkObject);
                UpdateTTSAudioSourceSettingsOnNetworkObject(reference, audioSourceSettings, useGlobalAudioSource);
            }
        }
        /// <summary>Updates the audio source settings of a TTS audio source component on a network object</summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the network object that owns the TTS audio source</param>
        /// <param name="audioSourceSettings">A TTSAudioSourceSettings object controlling playback (volume, spatial blend, rolloff, etc.) for this audio source</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdateTTSAudioSourceSettingsOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, TTSAudioSourceSettings audioSourceSettings, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT)
        {
            audioSourceSettings = audioSourceSettings ?? DefaultTTSAudioSourceSettings;
            ulong callerHash = useGlobalAudioSource ? HashHelper.GlobalCallerHash : HashHelper.GetCallingAssemblyHash(Assembly.GetCallingAssembly());

            if (LNetworkUtils.IsConnected) // is in-game, do normal server stuff
            {
                TTSCompanyNetworking.Request_Server_UpdateTTSAudioSourceSettings(new UpdateTTSAudioSourceSettings_NET(networkObjectRefOfSpeaker, callerHash, audioSourceSettings));
            }
            else // is NOT in-game, so do it without networking
            {
                if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
                {
                    LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), networkObjectRefOfSpeaker);
                    return;
                }

                TTSAudioSourceManager.UpdateTTSAudioSourceSettings(networkObject.gameObject, callerHash, audioSourceSettings);
            }
        }

        // -------------------- speak tts --------------------
        /// <summary>Generates and plays TTS audio at a network object, if the TTS audio source is found on the network object</summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the network object that owns the TTS audio source</param>
        /// <param name="textsToSpeak">An array of text lines to convert to speech</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        /// <param name="voiceSettings">A PiperVoiceSettings object controlling voice parameters (speaking rate, model, expressiveness, etc.) used to generate this audio</param>
        /// <param name="noiseRangeMultiplier">A multiplier applied to the amplitude-calculated range (clamped by the AudioSource's min/max distance) at which entities can hear this audio</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string[] textsToSpeak, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT, float noiseRangeMultiplier = APIDefaultsConstants.NOISE_RANGE_MULTIPLIER_DEFAULT)
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

            TTSCompanyNetworking.Request_Server_SpeakTTS(new TTSSpeakTTS_NET(networkObjectRefOfSpeaker, callingAHash, textsToSpeak, voiceSettings, trackingKeyHash, noiseRangeMultiplier));
        }
        /// <summary>Generates and plays TTS audio at a network object, if the TTS audio source is found on the network object</summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the network object that owns the TTS audio source</param>
        /// <param name="textToSpeak">A single line of text to convert to speech</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        /// <param name="voiceSettings">A PiperVoiceSettings object controlling voice parameters (speaking rate, model, expressiveness, etc.) used to generate this audio</param>
        /// <param name="noiseRangeMultiplier">A multiplier applied to the amplitude-calculated range (clamped by the AudioSource's min/max distance) at which entities can hear this audio</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string textToSpeak, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT, float noiseRangeMultiplier = APIDefaultsConstants.NOISE_RANGE_MULTIPLIER_DEFAULT)
        {
            SpeakTTSAtNetworkObject(networkObjectRefOfSpeaker, TTSCompanyUtils.SplitTextToSpeak(textToSpeak), useGlobalAudioSource, voiceSettings);
        }
        /// <summary>Generates and plays TTS audio at a network object, if the TTS audio source is found on the network object</summary>
        /// <param name="objectRefOfSpeaker">The local GameObject instance that owns the TTS audio source</param>
        /// <param name="textToSpeak">A single line of text to convert to speech</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        /// <param name="voiceSettings">A PiperVoiceSettings object controlling voice parameters (speaking rate, model, expressiveness, etc.) used to generate this audio</param>
        /// <param name="noiseRangeMultiplier">A multiplier applied to the amplitude-calculated range (clamped by the AudioSource's min/max distance) at which entities can hear this audio</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(GameObject objectRefOfSpeaker, string textToSpeak, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT, float noiseRangeMultiplier = APIDefaultsConstants.NOISE_RANGE_MULTIPLIER_DEFAULT)
        {
            SpeakTTSAtNetworkObject(objectRefOfSpeaker, TTSCompanyUtils.SplitTextToSpeak(textToSpeak), useGlobalAudioSource, voiceSettings);
        }
        /// <summary>Generates and plays TTS audio at a network object, if the TTS audio source is found on the network object</summary>
        /// <param name="objectRefOfSpeaker">The local GameObject instance that owns the TTS audio source</param>
        /// <param name="textsToSpeak">An array of text lines to convert to speech</param>
        /// <param name="useGlobalAudioSource">Whether to use the shared global TTS audio source, or a separate one owned by your assembly</param>
        /// <param name="voiceSettings">A PiperVoiceSettings object controlling voice parameters (speaking rate, model, expressiveness, etc.) used to generate this audio</param>
        /// <param name="noiseRangeMultiplier">A multiplier applied to the amplitude-calculated range (clamped by the AudioSource's min/max distance) at which entities can hear this audio</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpeakTTSAtNetworkObject(GameObject objectRefOfSpeaker, string[] textsToSpeak, bool useGlobalAudioSource = APIDefaultsConstants.USE_GLOBAL_AUDIO_SOURCE_DEFAULT, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT, float noiseRangeMultiplier = APIDefaultsConstants.NOISE_RANGE_MULTIPLIER_DEFAULT)
        {
            if (objectRefOfSpeaker.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                NetworkObjectReference reference = new NetworkObjectReference(networkObject);
                SpeakTTSAtNetworkObject(reference, textsToSpeak, useGlobalAudioSource, voiceSettings);
            }
        }

        // -------------------- generate TTS --------------------
        /// <summary>Generates and caches TTS audio lines ahead of time for instant playback later, this does not play any audio</summary>
        /// <param name="textToSpeak">A single line of text to convert to speech</param>
        /// <param name="voiceSettings">A PiperVoiceSettings object controlling voice parameters (speaking rate, model, expressiveness, etc.) used to generate this audio</param>
        public static void PreGenerateTTS(string textToSpeak, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT)
        {
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                return;
            }
            PreGenerateTTS(TTSCompanyUtils.SplitTextToSpeak(textToSpeak), voiceSettings);
        }
        /// <summary>Generates and caches TTS audio lines ahead of time for instant playback later, this does not play any audio</summary>
        /// <param name="textsToSpeak">An array of text lines to convert to speech</param>
        /// <param name="voiceSettings">A PiperVoiceSettings object controlling voice parameters (speaking rate, model, expressiveness, etc.) used to generate this audio</param>
        public static void PreGenerateTTS(string[] textsToSpeak, PiperVoiceSettings voiceSettings = APIDefaultsConstants.PIPER_VOICE_SETTING_DEFAULT)
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
