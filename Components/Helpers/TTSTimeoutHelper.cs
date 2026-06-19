using System;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components.Helpers
{
    internal static class TTSTimeoutHelper
    {
        internal static TimeSpan GetTTSTimeout(string[] textsToSpeak, PiperVoiceSettings settings)
        {
            LogConstants.CODE_TRIGGERED.Log(nameof(TTSTimeoutHelper), nameof(GetTTSTimeout));

            TimeSpan minimumTimeout = TimeSpan.FromSeconds(TTSConstants.TTS_TIMEOUT_MINIMUM_TIME);

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return minimumTimeout;
            }

            int totalWordCount = 0;
            int sentenceCount = 0;
            char[] wordSplitChars = new[] { ' ', '\r', '\n' };

            foreach (string segment in textsToSpeak)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                string[] words = segment.Split(wordSplitChars, StringSplitOptions.RemoveEmptyEntries);
                totalWordCount += words.Length;

                sentenceCount++;
            }

            if (totalWordCount == 0)
            {
                return minimumTimeout;
            }

            // estimate speaking duration
            float baseWordsPerSecond = 2.5f;
            // adjust speed
            float effectiveSpeechRate = settings.SpeechRate > 0.05f ? settings.SpeechRate : 1.0f;
            float estimatedSpeechDuration = (totalWordCount / baseWordsPerSecond) / effectiveSpeechRate;

            float totalSilenceDuration = sentenceCount * settings.SentenceSilence;
            float totalBuffer = TTSConstants.TTS_TIMEOUT_BASE_BUFFER + (totalWordCount * TTSConstants.TTS_TIMEOUT_PER_WORD_BUFFER);
            float totalTimeoutInSeconds = estimatedSpeechDuration + totalSilenceDuration + totalBuffer;

            LogConstants.TTS_TIMEOUT_HELPER_TIMEOUT_INFO.Log(nameof(TTSTimeoutHelper), string.Join("|", textsToSpeak), totalTimeoutInSeconds);

            TimeSpan calculatedTimeout = TimeSpan.FromSeconds(totalTimeoutInSeconds);
            return calculatedTimeout > minimumTimeout ? calculatedTimeout : minimumTimeout;
        }
    }
}
