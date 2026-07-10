using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using TTSCompany.Components;
using TTSCompany.Components.Enums;
using TTSCompany.Components.Helpers;
using TTSCompany.Components.Networking.Components.Structs;
using Unity.Netcode;
using UnityEngine;

namespace TTSCompany
{
    public static class TTSCompanyUtils
    {
        // matches sentence endings in ., !, or ?, or catches the trailing text
        private static readonly Regex SentenceRegex = new Regex(@"[^.!?]+[.!?]?", RegexOptions.Compiled);

        // -------------------- public utils --------------------
        // client side
        /// <summary>Checks whether a TTS voice model is currently loaded into memory</summary>
        /// <param name="voiceModelName">The file name of the voice model to check</param>
        /// <returns><c>true</c> if the model is currently loaded into memory, <c>false</c> otherwise</returns>
        public static bool HasTTSVoiceModelBeenLoadedIntoMemory(string voiceModelName)
        {
            return TTSCompanyPlugin._tts.isVoiceModelLoaded(voiceModelName);
        }

        /// <summary>Returns the name of a random TTS voice found by the library, whether or not that voice model is currently loaded into memory</summary>
        /// <returns>The <c>string</c> name of a random TTS voice model</returns>
        public static string GetRandomFoundTTSVoiceName()
        {
            return TTSCompanyPlugin._tts._server._memoryManager.GetRandomFoundTTSVoiceName();
        }

        /// <summary>Returns an array of names for all TTS voices found by the library, whether or not they are currently loaded into memory</summary>
        /// <returns>An array of <c>string</c> names of all found TTS voice models</returns>
        public static string[] GetAllFoundTTSVoiceNames()
        {
            return TTSCompanyPlugin._tts._server._memoryManager.GetAllFoundTTSVoiceNames();
        }

        /// <summary>Returns the name of a random TTS voice that is currently loaded into memory</summary>
        /// <returns>The <c>string</c> name of a random TTS voice model</returns>
        public static string GetRandomLoadedTTSVoiceName()
        {
            return TTSCompanyPlugin._tts._server._memoryManager.GetRandomLoadedTTSVoiceName();
        }

        /// <summary>Returns an array of names for all TTS voices that are currently loaded into memory</summary>
        /// <returns>An array of <c>string</c> names of all loaded TTS voice models</returns>
        public static string[] GetAllLoadedTTSVoiceNames()
        {
            return TTSCompanyPlugin._tts._server._memoryManager.GetAllLoadedTTSVoiceNames();
        }

        /// <summary>Checks whether a network object is currently playing TTS audio</summary>
        /// <param name="gameObject">The GameObject to check</param>
        /// <returns><c>true</c> if the object is currently speaking TTS audio, <c>false</c> otherwise</returns>
        public static bool IsNetworkObjectCurrentlySpeaking(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                return IsNetworkObjectCurrentlySpeaking(networkObject);
            }
            return false;
        }
        /// <summary>Checks whether a network object is currently playing TTS audio</summary>
        /// <param name="networkObject">The NetworkObject to check</param>
        /// <returns><c>true</c> if the object is currently speaking TTS audio, <c>false</c> otherwise</returns>
        public static bool IsNetworkObjectCurrentlySpeaking(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                return false;
            }

            GameObject target = networkObject.gameObject;
            foreach (SpeakTTSAudioClipCache cache in TTSCompanyBackend.WantedAudioClips.Values)
            {
                if (cache._foundNetworkObject == target)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Checks whether a network object is currently waiting on TTS audio to finish generating</summary>
        /// <param name="gameObject">The GameObject to check</param>
        /// <returns><c>true</c> if the object is currently waiting on TTS generation to complete, <c>false</c> otherwise</returns>
        public static bool IsNetworkObjectAwaitingTTSGeneration(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                return IsNetworkObjectAwaitingTTSGeneration(networkObject);
            }
            return false;
        }
        /// <summary>Checks whether a network object is currently waiting on TTS audio to finish generating</summary>
        /// <param name="networkObject">The NetworkObject to check</param>
        /// <returns><c>true</c> if the object is currently waiting on TTS generation to complete, <c>false</c> otherwise</returns>
        public static bool IsNetworkObjectAwaitingTTSGeneration(NetworkObject networkObject)
        {
            return networkObject != null && TTSCompanyBackend.GeneratingNetworkObjectIds.ContainsKey(networkObject.NetworkObjectId);
        }

        /// <summary>Returns the current TTSNetworkObjectState of a network object (e.g. Invalid, Idle, GeneratingTTS, ActivelySpeaking)</summary>
        /// <param name="gameObject">The GameObject to check the state of</param>
        /// <returns>The object's current <c>TTSNetworkObjectState</c></returns>
        public static TTSNetworkObjectState GetTTSNetworkObjectState(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                return GetTTSNetworkObjectState(networkObject);
            }
            return TTSNetworkObjectState.Invalid;
        }
        /// <summary>Returns the current TTSNetworkObjectState of a network object (e.g. Invalid, Idle, GeneratingTTS, ActivelySpeaking)</summary>
        /// <param name="networkObject">The NetworkObject to check the state of</param>
        /// <returns>The object's current <c>TTSNetworkObjectState</c></returns>
        public static TTSNetworkObjectState GetTTSNetworkObjectState(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                return TTSNetworkObjectState.Invalid;
            }

            if (IsNetworkObjectCurrentlySpeaking(networkObject))
            {
                return TTSNetworkObjectState.ActivelySpeaking;
            }

            if (IsNetworkObjectAwaitingTTSGeneration(networkObject))
            {
                return TTSNetworkObjectState.GeneratingTTS;
            }

            return TTSNetworkObjectState.Idle;
        }

        // -------------------- private utils --------------------
        internal static string[] SplitTextToSpeak(string textToSpeak)
        {
            if (string.IsNullOrEmpty(textToSpeak))
            {
                return Array.Empty<string>();
            }

            MatchCollection matches = SentenceRegex.Matches(textToSpeak);
            List<string> sentences = new List<string>(matches.Count);

            // split sentences
            // this speeds up generation for paragraphs
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                ReadOnlySpan<char> matchSpan = textToSpeak.AsSpan(match.Index, match.Length);
                ReadOnlySpan<char> trimmed = matchSpan.Trim();

                if (trimmed.Length > 0)
                {
                    sentences.Add(trimmed.ToString());
                }
            }

            // regex found nothing
            if (sentences.Count == 0)
            {
                sentences.Add(textToSpeak);
            }

            return sentences.ToArray();
        }

        internal static void GetAudioHashes(NetworkObjectReference networkObjectRefOfSpeaker, bool useGlobalAudioSource, out ulong callingAHash, out ulong trackingKeyHash)
        {
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
        }

        internal static float DetermineEndPause(string sentenceText, float sentenceSilence, float punctuationSilence)
        {
            if (string.IsNullOrEmpty(sentenceText))
            {
                return 0f;
            }

            string trimmed = sentenceText.TrimEnd();
            int i = trimmed.Length - 1;

            while (i >= 0 && char.IsWhiteSpace(sentenceText[i]))
            {
                i--;
            }

            while (i >= 0)
            {
                char c = sentenceText[i];
                if (c == '"' || c == '\'' || c == ')' || c == ']')
                {
                    i--;
                }
                else
                {
                    break;
                }
            }

            if (i < 0)
            {
                return 0f;
            }

            switch (sentenceText[i])
            {
                case '.':
                case '!':
                case '?':
                    return sentenceSilence;
                case ',':
                case ';':
                case ':':
                    return punctuationSilence;
                default:
                    return 0f;
            }
        }
    }
}
