using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TTS_Company.Components;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Helpers;
using TTS_Company.Components.Managers;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company
{
    public static class TTSCompanyAPI
    {
        // Keep track of all running running coroutines
        private static readonly Dictionary<ulong, ActiveTTSState> ActiveTTSCoroutines = new Dictionary<ulong, ActiveTTSState>();

        private class ActiveTTSState
        {
            public Coroutine Coroutine;
            public CancellationTokenSource Cts;
        }

        public static bool AddTTSAudioSourceOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, TTSAudioSourceSettings audioSourceSettings = null)
        {
            if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
            {
                LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), networkObjectRefOfSpeaker);
                return false;
            }

            audioSourceSettings = audioSourceSettings ?? new TTSAudioSourceSettings(); // if null, use the default
            return TTSAudioSourceManager.AddPermanentTTSAudioSource(networkObject.gameObject, audioSourceName, audioSourceSettings);
        }

        public static Coroutine SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, string textToSpeak, PiperVoiceSettings voiceSettings = null, TTSAudioSourceSettings audioSourceSettings = null)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(SpeakTTSAtNetworkObject));

            // if null, use the default
            voiceSettings = voiceSettings ?? new PiperVoiceSettings();
            audioSourceSettings = audioSourceSettings ?? new TTSAudioSourceSettings();

            ulong networkObjectId = 0;
            if (networkObjectRefOfSpeaker.TryGet(out NetworkObject netObj))
            {
                networkObjectId = netObj.NetworkObjectId;
            }

            ulong trackingKeyHash = HashHelper.GetTrackingKeyHash(networkObjectId, audioSourceName);

            if (ActiveTTSCoroutines.TryGetValue(trackingKeyHash, out ActiveTTSState activeState))
            {
                if (activeState.Cts != null)
                {
                    activeState.Cts.Cancel();
                }

                if (activeState.Coroutine != null)
                {
                    Plugin.instance.StopCoroutine(activeState.Coroutine);
                }

                ActiveTTSCoroutines.Remove(trackingKeyHash);
            }

            CancellationTokenSource newCts = new CancellationTokenSource(TTSTimeoutHelper.GetTTSTimeout(textToSpeak, voiceSettings));
            ActiveTTSState newState = new ActiveTTSState { Cts = newCts };

            newState.Coroutine = Plugin.instance.StartCoroutine(SpeakTTSInternalRoutine(trackingKeyHash, networkObjectRefOfSpeaker, audioSourceName, textToSpeak, voiceSettings, newCts));

            ActiveTTSCoroutines[trackingKeyHash] = newState;
            return newState.Coroutine;
        }

        private static IEnumerator SpeakTTSInternalRoutine(ulong trackingKeyHash, NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, string textToSpeak, PiperVoiceSettings voiceSettings, CancellationTokenSource cts)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(SpeakTTSInternalRoutine));

            try
            {
                if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
                {
                    yield break;
                }

                GameObject receivedGameObject = networkObject.gameObject;
                Task<TTSResult> ttsTask = Plugin._tts.GenerateTTSAsync(textToSpeak, voiceSettings, cts.Token);

                yield return new WaitUntil(() => ttsTask.IsCompleted);

                if (ttsTask.IsFaulted || ttsTask.IsCanceled)
                {
                    yield break;
                }

                TTSResult result = ttsTask.Result;
                if (!result.Success)
                {
                    yield break;
                }

                if (receivedGameObject == null || !receivedGameObject.activeInHierarchy)
                {
                    yield break;
                }

                TTSAudioSourceManager.PlayAudioSource(receivedGameObject, audioSourceName, result.AudioClip);
            }
            finally
            {
                cts.Dispose();

                if (ActiveTTSCoroutines.TryGetValue(trackingKeyHash, out ActiveTTSState current) && current.Cts == cts)
                {
                    ActiveTTSCoroutines.Remove(trackingKeyHash);
                }
            }
        }
    }
}
