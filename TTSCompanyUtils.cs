using System.Collections.Generic;
using System.Text.RegularExpressions;
using TTS_Company.Components;
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

        // -------------------- private utils --------------------
        internal static string[] SplitTextToSpeak(string textToSpeak)
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
