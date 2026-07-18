using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TTSCompany.Components.Constants;
using TTSCompany.Components.Helpers;
using TTSCompany.Components.Networking;
using TTSCompany.Components.Networking.Components.Structs;
using TTSCompany.Components.Server.Components;
using Unity.Netcode;
using UnityEngine;

namespace TTSCompany.Components
{
    internal static class TTSCompanyBackend
    {
        // Keep track of all running running coroutines
        private static readonly ConcurrentDictionary<ulong, ActiveTTSState> ActiveTTSCoroutines = new ConcurrentDictionary<ulong, ActiveTTSState>();
        private static readonly ConcurrentDictionary<ulong, CancellationTokenSource> ActivePreGenTasks = new ConcurrentDictionary<ulong, CancellationTokenSource>();

        internal static readonly ConcurrentDictionary<ulong, SpeakTTSAudioClipCache> WantedAudioClips = new ConcurrentDictionary<ulong, SpeakTTSAudioClipCache>();
        internal static readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, byte>> SpeakingNetworkObjectIds = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, byte>>();
        internal static readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, byte>> GeneratingNetworkObjectIds = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, byte>>();

        internal static readonly ConcurrentQueue<ulong> NewSpeakerQueue = new ConcurrentQueue<ulong>();

        internal static void SpeakTTSAtNetworkObject_OnClient(TTSSpeakTTS_PLUS_NET data)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyBackend), nameof(SpeakTTSAtNetworkObject_OnClient));

            if (ActiveTTSCoroutines.TryGetValue(data._trackingKeyHash, out ActiveTTSState activeState))
            {
                if (activeState.Cts != null)
                {
                    CtsHelper.SafeCancel(activeState.Cts);
                }
                if (activeState.Coroutine != null)
                {
                    TTSCompanyPlugin.instance.StopCoroutine(activeState.Coroutine);
                }

                RemoveAssemblyTracking(GeneratingNetworkObjectIds, activeState.NetworkObjectId, data._callingAssemblyHash);
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

            newState.Coroutine = TTSCompanyPlugin.instance.StartCoroutine(SpeakMultipleTTSInternalRoutine(data._sessionId, data._trackingKeyHash, data._networkObjectRefOfSpeaker, data._callingAssemblyHash, data._textsToSpeak, data._voiceSettings, data._noiseRangeMultiplier, newCts));
            ActiveTTSCoroutines[data._trackingKeyHash] = newState;
            AddAssemblyTracking(GeneratingNetworkObjectIds, data._networkObjectRefOfSpeaker.NetworkObjectId, data._callingAssemblyHash);
        }

        internal static IEnumerator SpeakMultipleTTSInternalRoutine(ulong sessionId, ulong trackingKeyHash, NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, string[] textsToSpeak, PiperVoiceSettings voiceSettings, float noiseRangeMultiplier, CancellationTokenSource cts)
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
                TTSCompanyNetworking.CreateClientTask(sessionId, networkObjectRefOfSpeaker, callingAssemblyHash, textsToSpeak, voiceSettings.SentenceSilence, voiceSettings.PunctuationSilence, noiseRangeMultiplier, cts);

                for (int i = 0; i < ttsTasks.Length; i++)
                {
                    Task<TTSResult> currentTask = ttsTasks[i];

                    yield return new WaitUntil(() => currentTask.IsCompleted);

                    // if any audio clip failed, then cancel talking
                    if (currentTask.IsFaulted || currentTask.IsCanceled || !currentTask.Result.Success || currentTask.Result.AudioClip == null)
                    {
                        CtsHelper.SafeCancel(cts);
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
                    RemoveAssemblyTracking(GeneratingNetworkObjectIds, networkObjectRefOfSpeaker.NetworkObjectId, callingAssemblyHash);
                }
            }
        }

        internal static void PlaySpeakTTSAtNetworkObject_OnClient(ulong taskid, NetworkObjectReference networkObjectReference, ulong callingAssemblyHash, AudioClip[] audioClip, float[] pauseDurations, bool isFinalBatch, float noiseRangeMultiplier)
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
                cache.AddAudioClips(audioClip, pauseDurations);
            }
            else
            {
                cache = new SpeakTTSAudioClipCache(receivedGameObject, callingAssemblyHash, audioClip, pauseDurations, noiseRangeMultiplier);
                WantedAudioClips.TryAdd(taskid, cache);
                AddAssemblyTracking(SpeakingNetworkObjectIds, networkObject.NetworkObjectId, callingAssemblyHash);
                NewSpeakerQueue.Enqueue(taskid);
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
                CtsHelper.SafeCancel(existingCts);
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
                        CtsHelper.SafeCancel(cts);
                        yield break;
                    }

                    TTSResult result = currentTask.Result;
                    if (result.AudioClip != null)
                    {
                        Object.Destroy(result.AudioClip);
                    }
                }
            }
            finally
            {
                cts.Dispose();
                if (ActivePreGenTasks.TryGetValue(trackingKeyHash, out CancellationTokenSource current) && current == cts)
                {
                    ActivePreGenTasks.TryRemove(trackingKeyHash, out _);
                }
            }
        }

        internal static void AddAssemblyTracking(ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, byte>> tracker, ulong networkObjectId, ulong assemblyHash)
        {
            ConcurrentDictionary<ulong, byte> assemblyHashes = tracker.GetOrAdd(networkObjectId, _ => new ConcurrentDictionary<ulong, byte>());
            assemblyHashes.TryAdd(assemblyHash, 0);
        }

        internal static void RemoveAssemblyTracking(ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, byte>> tracker, ulong networkObjectId, ulong assemblyHash)
        {
            if (!tracker.TryGetValue(networkObjectId, out ConcurrentDictionary<ulong, byte> assemblyHashes))
            {
                return;
            }

            assemblyHashes.TryRemove(assemblyHash, out _);
            if (assemblyHashes.IsEmpty)
            {
                tracker.TryRemove(networkObjectId, out _);
            }
        }

        // -------------------- patch calls --------------------
        internal static void OnReturnedToMainMenu()
        {
            foreach (ActiveTTSState state in ActiveTTSCoroutines.Values)
            {
                state.Cts?.SafeCancel();
            }
            ActiveTTSCoroutines.Clear();

            foreach (SpeakTTSAudioClipCache cache in WantedAudioClips.Values)
            {
                while (cache._audioQueue.TryDequeue(out QueuedClip queued))
                {
                    if (queued.Clip != null)
                    {
                        Object.Destroy(queued.Clip);
                    }
                }
            }
            WantedAudioClips.Clear();
            while (NewSpeakerQueue.TryDequeue(out _))
            {
                // dont' do anything here, we are clearing it out
            }

            SpeakingNetworkObjectIds.Clear();
            GeneratingNetworkObjectIds.Clear();

            TTSCompanyNetworking.ClearClientTasks();
            TTSCompanyNetworking.ClearServerTasks();
        }
    }
}
