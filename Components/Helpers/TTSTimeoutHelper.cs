using System;
using System.Text.RegularExpressions;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components.Helpers
{
    internal static class TTSTimeoutHelper
    {
        internal static TimeSpan GetTTSTimeout(string textToSpeak, PiperVoiceSettings settings)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSTimeoutHelper), nameof(GetTTSTimeout));

            TimeSpan minimumTimeout = TimeSpan.FromSeconds(TTSConstants.TTS_TIMEOUT_MINIMUM_TIME);

            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                return minimumTimeout;
            }

            // estimate speaking duration
            string[] words = textToSpeak.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            float baseWordsPerSecond = 2.5f;

            // adjust speed
            float effectiveSpeechRate = settings.SpeechRate > 0.05f ? settings.SpeechRate : 1.0f;
            float estimatedSpeechDuration = (words.Length / baseWordsPerSecond) / effectiveSpeechRate;

            // estimate for silences for punctuation
            int sentenceCount = Regex.Matches(textToSpeak, @"[.!?]+").Count;
            if (sentenceCount == 0)
            {
                sentenceCount = 1; // treat as at least one sentence
            }
            float totalSilenceDuration = sentenceCount * settings.SentenceSilence;

            // TTS generation overhead buffer
            float totalBuffer = TTSConstants.TTS_TIMEOUT_BASE_BUFFER + (words.Length * TTSConstants.TTS_TIMEOUT_PER_WORD_BUFFER);

            // combine
            float totalTimeoutInSeconds = estimatedSpeechDuration + totalSilenceDuration + totalBuffer;
            LogConstants.TTS_TIMEOUT_HELPER_TIMEOUT_INFO.Log(nameof(TTSTimeoutHelper), textToSpeak, totalTimeoutInSeconds);
            // convert to TimeSpan
            TimeSpan calculatedTimeout = TimeSpan.FromSeconds(totalTimeoutInSeconds);

            // return calculated value
            return calculatedTimeout > minimumTimeout ? calculatedTimeout : minimumTimeout;
        }
    }
}
