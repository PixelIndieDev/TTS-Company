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

        // matches sentence endings in ., !, or ?, or catches the trailing text
        private static readonly Regex SentenceRegex = new Regex(@"[^.!?]+[.!?]?", RegexOptions.Compiled);

        // -------------------- preload voice models --------------------

        /// <summary>
        /// Asynchronously loads a specific Piper voice model into memory.
        /// </summary>
        /// <param name="voiceModelName">The name of the voice model to preload (e.g., "en_US-hfc_female-medium").</param>
        /// <returns>A task that returns a tuple indicating whether the preloading succeeded, and an error message if it failed.</returns>
        public async static Task<(bool Success, string Error)> PreloadTTSVoiceModelInMemory(string voiceModelName)
        {
            return await Plugin._tts.PreloadVoiceAsync(VoiceHelper.CleanupVoiceModelname(voiceModelName));
        }

        /// <summary>
        /// Checks whether a specific Piper voice model has been successfully loaded into memory.
        /// </summary>
        /// <param name="voiceModelName">The name of the voice model to check (e.g., "en_US-hfc_female-medium").</param>
        /// <returns><c>true</c> if the voice model is currently loaded in memory; otherwise, <c>false</c>.</returns>
        public static bool HasTTSVoiceModelBeenLoadedIntoMemory(string voiceModelName)
        {
            return Plugin._tts.isVoiceModelLoaded(voiceModelName);
        }

        // -------------------- add audio sources --------------------

        /// <summary>
        /// Attaches a permanent TTS audio source component to a specific networked object.
        /// </summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the Unity Netcode <see cref="NetworkObject"/> that will parent the audio source.</param>
        /// <param name="audioSourceName">A unique string identifier/name for this specific audio source.</param>
        /// <param name="audioSourceSettings">Optional configuration settings for the spatial/audio properties. Falls back to default settings if null.</param>
        /// <returns><c>true</c> if the audio source was successfully added; otherwise, <c>false</c>.</returns>
        public static bool AddTTSAudioSourceOnNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, TTSAudioSourceSettings audioSourceSettings = default)
        {
            if (!networkObjectRefOfSpeaker.TryGet(out NetworkObject networkObject))
            {
                LogConstants.API_NETWORK_OBJECT_NOT_FOUND.Log(nameof(TTSCompanyAPI), networkObjectRefOfSpeaker);
                return false;
            }

            return TTSAudioSourceManager.AddPermanentTTSAudioSource(networkObject.gameObject, audioSourceName, audioSourceSettings);
        }

        // -------------------- generate TTS --------------------

        /// <summary>
        /// Pre-generates and caches the TTS audio for a string of text without playing it. Splits the text into individual sentences internally.
        /// </summary>
        /// <param name="textToSpeak">The full block of text to generate speech for.</param>
        /// <param name="voiceSettings">Optional Piper voice configuration (model, speed, etc.). Falls back to default settings if null.</param>
        /// <returns>The running Unity <see cref="Coroutine"/> handling the background generation, or <c>null</c> if the input is empty.</returns>
        public static Coroutine PreGenerateTTS(string textToSpeak, PiperVoiceSettings voiceSettings = null)
        {
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                return null;
            }

            return PreGenerateTTS(SplitTextToSpeak(textToSpeak), voiceSettings);
        }

        /// <summary>
        /// Pre-generates and caches the TTS audio for an array of text fragments without playing them.
        /// </summary>
        /// <param name="textsToSpeak">An array of text fragments to generate speech for.</param>
        /// <param name="voiceSettings">Optional Piper voice configuration (model, speed, etc.). Falls back to default settings if null.</param>
        /// <returns>The running Unity <see cref="Coroutine"/> handling the background generation, or <c>null</c> if the array is empty.</returns>
        public static Coroutine PreGenerateTTS(string[] textsToSpeak, PiperVoiceSettings voiceSettings = null)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(PreGenerateTTS));

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return null;
            }

            // if null, use the default
            voiceSettings = voiceSettings ?? DefaultVoiceSettings;

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

            TimeSpan totalTimeout = TTSTimeoutHelper.GetTTSTimeout(textsToSpeak, voiceSettings);

            CancellationTokenSource newCts = new CancellationTokenSource(totalTimeout);
            ActiveTTSState newState = new ActiveTTSState { Cts = newCts };

            newState.Coroutine = Plugin.instance.StartCoroutine(PreGenerateTTS(trackingKeyHash, textsToSpeak, voiceSettings, newCts));

            ActiveTTSCoroutines[trackingKeyHash] = newState;
            return newState.Coroutine;
        }

        // -------------------- play TTS --------------------

        /// <summary>
        /// Generates and plays an array of text fragments in sequence from a designated audio source on a networked object.
        /// </summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the <see cref="NetworkObject"/> that should emit the audio.</param>
        /// <param name="audioSourceName">The identifier name of the specific audio source channel to use for playback.</param>
        /// <param name="textsToSpeak">An array of text fragments to be spoken in chronological order.</param>
        /// <param name="voiceSettings">Optional Piper voice configuration. Falls back to default settings if null.</param>
        /// <param name="audioSourceSettings">Optional audio spatial configuration. Falls back to default settings if null.</param>
        /// <returns>The running Unity <see cref="Coroutine"/> managing the generation and playback sequence, or <c>null</c> if the array is empty.</returns>
        public static Coroutine SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, string[] textsToSpeak, PiperVoiceSettings voiceSettings = null, TTSAudioSourceSettings audioSourceSettings = default)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(SpeakTTSAtNetworkObject));

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return null;
            }

            // if null, use the default
            voiceSettings = voiceSettings ?? DefaultVoiceSettings;

            ulong trackingKeyHash = HashHelper.GetTrackingKeyHash(networkObjectRefOfSpeaker.NetworkObjectId, audioSourceName);

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

            TimeSpan totalTimeout = TTSTimeoutHelper.GetTTSTimeout(textsToSpeak, voiceSettings);

            CancellationTokenSource newCts = new CancellationTokenSource(totalTimeout);
            ActiveTTSState newState = new ActiveTTSState { Cts = newCts };

            newState.Coroutine = Plugin.instance.StartCoroutine(SpeakMultipleTTSInternalRoutine(trackingKeyHash, networkObjectRefOfSpeaker, audioSourceName, textsToSpeak, voiceSettings, newCts));

            ActiveTTSCoroutines[trackingKeyHash] = newState;
            return newState.Coroutine;
        }

        /// <summary>
        /// Generates and plays back a block of text on a designated audio source on a networked object. Splits text into sentences internally.
        /// </summary>
        /// <param name="networkObjectRefOfSpeaker">A reference to the <see cref="NetworkObject"/> that should emit the audio.</param>
        /// <param name="audioSourceName">The identifier name of the specific audio source channel to use for playback.</param>
        /// <param name="textToSpeak">The full block of text to be converted to speech and played.</param>
        /// <param name="voiceSettings">Optional Piper voice configuration. Falls back to default settings if null.</param>
        /// <param name="audioSourceSettings">Optional audio spatial configuration. Falls back to default settings if null.</param>
        /// <returns>The running Unity <see cref="Coroutine"/> managing the generation and playback sequence, or <c>null</c> if text is empty.</returns>
        public static Coroutine SpeakTTSAtNetworkObject(NetworkObjectReference networkObjectRefOfSpeaker, string audioSourceName, string textToSpeak, PiperVoiceSettings voiceSettings = null, TTSAudioSourceSettings audioSourceSettings = default)
        {
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                return null;
            }

            return SpeakTTSAtNetworkObject(networkObjectRefOfSpeaker, audioSourceName, SplitTextToSpeak(textToSpeak), voiceSettings, audioSourceSettings);
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

        private static IEnumerator PreGenerateTTS(ulong trackingKeyHash, string[] textsToSpeak, PiperVoiceSettings voiceSettings, CancellationTokenSource cts)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSCompanyAPI), nameof(PreGenerateTTS));

            try
            {
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

                if (ActiveTTSCoroutines.TryGetValue(trackingKeyHash, out ActiveTTSState current) && current.Cts == cts)
                {
                    ActiveTTSCoroutines.Remove(trackingKeyHash);
                }
            }
        }

        private static string[] SplitTextToSpeak(string textToSpeak)
        {
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

            return sentences.ToArray();
        }
    }
}