using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        private static readonly PiperVoiceSettings DefaultVoiceSettings = new PiperVoiceSettings();
        private static readonly TTSAudioSourceSettings DefaultAudioSourceSettings = new TTSAudioSourceSettings();

        // matches sentence endings in ., !, or ?, or catches the trailing text
        private static readonly Regex SentenceRegex = new Regex(@"[^.!?]+[.!?]?", RegexOptions.Compiled);

        // -------------------- preload voice models --------------------
        public async static Task<(bool Success, string Error)> PreloadTTSVoiceModelInMemory(string voiceModelName)
        {
            return await Plugin._tts.PreloadVoiceAsync(VoiceHelper.CleanupVoiceModelname(voiceModelName));
        }

        public static bool HasTTSVoiceModelBeenLoadedIntoMemory(string voiceModelName)
        {
            return Plugin._tts.isVoiceModelLoaded(voiceModelName);
        }

        // -------------------- add audio sources --------------------
        public static bool AddTTSAudioSourceOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, TTSAudioSourceSettings audioSourceSettings = null)
        {
            if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
            {
                LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), networkObjectRefOfSpeaker);
                return false;
            }

            audioSourceSettings = audioSourceSettings ?? DefaultAudioSourceSettings; // if null, use the default
            return TTSAudioSourceManager.AddPermanentTTSAudioSource(networkObject.gameObject, audioSourceName, audioSourceSettings);
        }

        // -------------------- generate TTS --------------------
        public static Coroutine PreGenerateTTS(string textToSpeak, PiperVoiceSettings voiceSettings = null)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(PreGenerateTTS));

            // if null, use the default
            voiceSettings = voiceSettings ?? DefaultVoiceSettings;

            ulong trackingKeyHash = HashHelper.GetTrackingKeyHash(textToSpeak, voiceSettings);
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

            newState.Coroutine = Plugin.instance.StartCoroutine(PreGenerateTTS(trackingKeyHash, textToSpeak, voiceSettings, newCts));

            ActiveTTSCoroutines[trackingKeyHash] = newState;
            return newState.Coroutine;
        }

        // -------------------- play TTS --------------------
        public static Coroutine SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, string[] textsToSpeak, PiperVoiceSettings voiceSettings = null, TTSAudioSourceSettings audioSourceSettings = null)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(SpeakTTSAtNetworkObject));

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return null;
            }

            // if null, use the default
            voiceSettings = voiceSettings ?? DefaultVoiceSettings;
            audioSourceSettings = audioSourceSettings ?? DefaultAudioSourceSettings;

            string combinedText = string.Join("|", textsToSpeak);
            ulong trackingKeyHash = HashHelper.GetTrackingKeyHash(combinedText, voiceSettings);

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

            TimeSpan totalTimeout = TimeSpan.Zero;
            foreach (var text in textsToSpeak)
            {
                totalTimeout += TTSTimeoutHelper.GetTTSTimeout(text, voiceSettings);
            }

            CancellationTokenSource newCts = new CancellationTokenSource(totalTimeout);
            ActiveTTSState newState = new ActiveTTSState { Cts = newCts };

            newState.Coroutine = Plugin.instance.StartCoroutine(SpeakMultipleTTSInternalRoutine(trackingKeyHash, networkObjectRefOfSpeaker, audioSourceName, textsToSpeak, voiceSettings, newCts));

            ActiveTTSCoroutines[trackingKeyHash] = newState;
            return newState.Coroutine;
        }

        public static Coroutine SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, string textToSpeak, PiperVoiceSettings voiceSettings = null, TTSAudioSourceSettings audioSourceSettings = null)
        {
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                return null;
            }

            MatchCollection matches = SentenceRegex.Matches(textToSpeak);
            List<string> sentences = new List<string>(matches.Count);

            // split sentences
            // this speeds up generation for paragraphs
            for (int i = 0; i < matches.Count; i++)
            {
                string trimmed = matches[i].Value.Trim();
                if (trimmed.Length > 0)
                {
                    sentences.Add(trimmed);
                }
            }

            // regex found nothing
            if (sentences.Count == 0)
            {
                sentences.Add(textToSpeak);
            }

            return SpeakTTSAtNetworkObject(networkObjectRefOfSpeaker, audioSourceName, sentences.ToArray(), voiceSettings, audioSourceSettings);
        }

        // -------------------- private functions --------------------
        private static IEnumerator SpeakMultipleTTSInternalRoutine(ulong trackingKeyHash, NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, string[] textsToSpeak, PiperVoiceSettings voiceSettings, CancellationTokenSource cts)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(SpeakMultipleTTSInternalRoutine));

            try
            {
                if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
                {
                    yield break;
                }

                GameObject receivedGameObject = networkObject.gameObject;
                if (receivedGameObject == null || !receivedGameObject.activeInHierarchy)
                {
                    yield break;
                }

                Task<TTSResult>[] ttsTasks = new Task<TTSResult>[textsToSpeak.Length];
                for (int i = 0; i < textsToSpeak.Length; i++)
                {
                    ttsTasks[i] = Plugin._tts.GenerateTTSAsync(textsToSpeak[i], voiceSettings, cts.Token);
                }

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
                    TTSAudioSourceManager.PlayAudioSource(receivedGameObject, audioSourceName, result.AudioClip);

                    yield return new WaitForSeconds(result.AudioClip.length);
                }
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

        private static IEnumerator PreGenerateTTS(ulong trackingKeyHash, string textToSpeak, PiperVoiceSettings voiceSettings, CancellationTokenSource cts)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(PreGenerateTTS));

            try
            {
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
