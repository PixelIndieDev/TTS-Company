using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TTS_Company.Components;
using TTS_Company.Components.Enums;
using TTS_Company.Components.Networking.Components.Structs;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company
{
    public static class TTSCompanyUtils
    {
        // matches sentence endings in ., !, or ?, or catches the trailing text
        private static readonly Regex SentenceRegex = new Regex(@"[^.!?]+[.!?]?", RegexOptions.Compiled);

        // -------------------- public utils --------------------
        // client side
        public static bool HasTTSVoiceModelBeenLoadedIntoMemory(string voiceModelName)
        {
            return TTSCompanyPlugin._tts.isVoiceModelLoaded(voiceModelName);
        }

        public static string GetRandomFoundTTSVoiceName()
        {
            return TTSCompanyPlugin._tts._server._memoryManager.GetRandomFoundTTSVoiceName();
        }
        public static string GetRandomLoadedTTSVoicename()
        {
            return TTSCompanyPlugin._tts._server._memoryManager.GetRandomLoadedTTSVoiceName();
        }

        public static bool IsNetworkObjectCurrentlySpeaking(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                return IsNetworkObjectCurrentlySpeaking(networkObject);
            }
            return false;
        }
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

        public static bool IsNetworkObjectAwaitingTTSGeneration(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                return IsNetworkObjectAwaitingTTSGeneration(networkObject);
            }
            return false;
        }
        public static bool IsNetworkObjectAwaitingTTSGeneration(NetworkObject networkObject)
        {
            return networkObject != null && TTSCompanyBackend.GeneratingNetworkObjectIds.ContainsKey(networkObject.NetworkObjectId);
        }

        public static TTSNetworkObjectState GetTTSNetworkObjectState(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                return GetTTSNetworkObjectState(networkObject);
            }
            return TTSNetworkObjectState.Invalid;
        }
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
