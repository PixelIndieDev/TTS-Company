using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Networking;
using TTS_Company.Components.Networking.Components.Structs;
using TTS_Company.Components.Server.Components;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components
{
    internal static class TTSCompanyBackend
    {
        // Keep track of all running running coroutines
        private static readonly ConcurrentDictionary<ulong, ActiveTTSState> ActiveTTSCoroutines = new ConcurrentDictionary<ulong, ActiveTTSState>();
        private static readonly ConcurrentDictionary<ulong, CancellationTokenSource> ActivePreGenTasks = new ConcurrentDictionary<ulong, CancellationTokenSource>();

        internal static readonly ConcurrentDictionary<ulong, SpeakTTSAudioClipCache> WantedAudioClips = new ConcurrentDictionary<ulong, SpeakTTSAudioClipCache>();
        internal static readonly ConcurrentDictionary<ulong, bool> SpeakingNetworkObjectIds = new ConcurrentDictionary<ulong, bool>();
        internal static readonly ConcurrentDictionary<ulong, bool> GeneratingNetworkObjectIds = new ConcurrentDictionary<ulong, bool>();

        internal static void SpeakTTSAtNetworkObject_OnClient(TTSSpeakTTS_PLUS_NET data)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyBackend), nameof(SpeakTTSAtNetworkObject_OnClient));

            if (ActiveTTSCoroutines.TryGetValue(data._trackingKeyHash, out ActiveTTSState activeState))
            {
                if (activeState.Cts != null)
                {
                    activeState.Cts.Cancel();
                }
                if (activeState.Coroutine != null)
                {
                    TTSCompanyPlugin.instance.StopCoroutine(activeState.Coroutine);
                }

                GeneratingNetworkObjectIds.TryRemove(activeState.NetworkObjectId, out _);
                ActiveTTSCoroutines.TryRemove(data._trackingKeyHash, out _);
            }

            CancellationTokenSource newCts = new CancellationTokenSource();
            ActiveTTSState newState = new ActiveTTSState
            {
                Cts = newCts,
                NetworkObjectId = data._networkObjectRefOfSpeaker.NetworkObjectId
            };

            if (TTSCompanyPlugin.instance == null)
            {
                return;
            }

            newState.Coroutine = TTSCompanyPlugin.instance.StartCoroutine(SpeakMultipleTTSInternalRoutine(data._sessionId, data._trackingKeyHash, data._networkObjectRefOfSpeaker, data._callingAssemblyHash, data._textsToSpeak, data._voiceSettings, newCts));
            ActiveTTSCoroutines[data._trackingKeyHash] = newState;
            GeneratingNetworkObjectIds.TryAdd(data._networkObjectRefOfSpeaker.NetworkObjectId, false);
        }

        internal static IEnumerator SpeakMultipleTTSInternalRoutine(ulong sessionId, ulong trackingKeyHash, NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, string[] textsToSpeak, PiperVoiceSettings voiceSettings, CancellationTokenSource cts)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyBackend), nameof(SpeakMultipleTTSInternalRoutine));

            try
            {
                Task<TTSResult>[] ttsTasks = new Task<TTSResult>[textsToSpeak.Length];
                for (int i = 0; i < textsToSpeak.Length; i++)
                {
                    ttsTasks[i] = TTSCompanyPlugin._tts.GenerateTTSAsync(textsToSpeak[i], voiceSettings, cts.Token);
                }

                // client network
                TTSCompanyNetworking.CreateClientTask(sessionId, networkObjectRefOfSpeaker, callingAssemblyHash, textsToSpeak, cts);

                for (int i = 0; i < ttsTasks.Length; i++)
                {
                    Task<TTSResult> currentTask = ttsTasks[i];

                    yield return new WaitUntil(() => currentTask.IsCompleted);

                    // if any audio clip failed, then cancel talking
                    if (currentTask.IsFaulted || currentTask.IsCanceled || !currentTask.Result.Success || currentTask.Result.AudioClip == null)
                    {
                        cts.Cancel();
                        yield break;
                    }

                    TTSResult result = currentTask.Result;
                    TTSCompanyNetworking.UpdateClientTask(sessionId, i, result.AudioClip);

                    // send update to server
                    TTSCompanyNetworking.Request_Server_UpdateSentenceProgress(new SentenceProgressData_NET(sessionId, i, true));
                }
            }
            finally
            {
                cts.Dispose();

                if (ActiveTTSCoroutines.TryGetValue(trackingKeyHash, out ActiveTTSState current) && current.Cts == cts)
                {
                    ActiveTTSCoroutines.TryRemove(trackingKeyHash, out _);
                    GeneratingNetworkObjectIds.TryRemove(networkObjectRefOfSpeaker.NetworkObjectId, out _);
                }
            }
        }

        internal static void PlaySpeakTTSAtNetworkObject_OnClient(ulong taskid, NetworkObjectReference networkObjectReference, ulong callingAssemblyHash, AudioClip[] audioClip, bool isFinalBatch)
        {
            if (audioClip == null || !networkObjectReference.TryGet(out NetworkObject networkObject))
            {
                return;
            }

            GameObject receivedGameObject = networkObject.gameObject;
            if (receivedGameObject == null || !receivedGameObject.activeInHierarchy)
            {
                return;
            }

            if (WantedAudioClips.TryGetValue(taskid, out SpeakTTSAudioClipCache cache))
            {
                cache.AddAudioClips(audioClip);
            }
            else
            {
                cache = new SpeakTTSAudioClipCache(receivedGameObject, callingAssemblyHash, audioClip);
                WantedAudioClips.TryAdd(taskid, cache);
                SpeakingNetworkObjectIds.TryAdd(networkObject.NetworkObjectId, false);
            }

            if (isFinalBatch)
            {
                cache.MarkLastBatch();
            }
        }

        internal static IEnumerator PreGenerateTTS(ulong trackingKeyHash, string[] textsToSpeak, PiperVoiceSettings voiceSettings, CancellationTokenSource cts)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyBackend), nameof(PreGenerateTTS));

            if (ActivePreGenTasks.TryRemove(trackingKeyHash, out CancellationTokenSource existingCts))
            {
                existingCts.Cancel();
            }
            ActivePreGenTasks[trackingKeyHash] = cts;

            try
            {
                Task<TTSResult>[] ttsTasks = new Task<TTSResult>[textsToSpeak.Length];
                for (int i = 0; i < textsToSpeak.Length; i++)
                {
                    ttsTasks[i] = TTSCompanyPlugin._tts.GenerateTTSAsync(textsToSpeak[i], voiceSettings, cts.Token);
                }

                for (int i = 0; i < ttsTasks.Length; i++)
                {
                    Task<TTSResult> currentTask = ttsTasks[i];

                    yield return new WaitUntil(() => currentTask.IsCompleted);

                    // if any audio clip failed, then cancel talking
                    if (currentTask.IsFaulted || currentTask.IsCanceled || !currentTask.Result.Success)
                    {
                        cts.Cancel();
                        yield break;
                    }
                }
            }
            finally
            {
                cts.Dispose();
                ActivePreGenTasks.TryRemove(trackingKeyHash, out _);
            }
        }
    }
}
