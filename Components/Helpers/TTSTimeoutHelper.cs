using System;
using TTSCompany.Components.Constants;

namespace TTSCompany.Components.Helpers
{
    internal static class TTSTimeoutHelper
    {
        private const float baseWordsPerSecond = 2.5f;
        private static readonly char[] wordSplitChars = new[] { ' ', '\r', '\n' };

        internal static TimeSpan GetPlaybackTimeout(string[] textsToSpeak, PiperVoiceSettings settings)
        {
            TimeSpan minimumTimeout = TimeSpan.FromSeconds(TTSConstants.TTS_TIMEOUT_MINIMUM_TIME);

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return minimumTimeout;
            }

            int totalWordCount = 0;
            int sentenceCount = 0;

            foreach (string segment in textsToSpeak)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                totalWordCount += segment.Split(wordSplitChars, StringSplitOptions.RemoveEmptyEntries).Length;
                sentenceCount++;
            }

            if (totalWordCount == 0)
            {
                return minimumTimeout;
            }

            float effectiveSpeechRate = settings.SpeechRate > 0.05f ? settings.SpeechRate : 1.0f;

            float estimatedPlaybackDuration = (totalWordCount / baseWordsPerSecond) / effectiveSpeechRate;
            float totalSilence = sentenceCount * settings.SentenceSilence;

            float total = estimatedPlaybackDuration + totalSilence + TTSConstants.TTS_PLAYBACK_TIMEOUT_BUFFER_SECONDS_SCALED;

            TimeSpan calculated = TimeSpan.FromSeconds(total);
            return calculated > minimumTimeout ? calculated : minimumTimeout;
        }

        internal static TimeSpan GetGenerationTimeout(string[] textsToSpeak, PiperVoiceSettings settings)
        {
            TimeSpan minimumTimeout = TimeSpan.FromSeconds(TTSConstants.TTS_TIMEOUT_MINIMUM_TIME);

            if (textsToSpeak == null || textsToSpeak.Length == 0)
            {
                return minimumTimeout;
            }

            int totalWordCount = 0;

            foreach (string segment in textsToSpeak)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }
                totalWordCount += segment.Split(wordSplitChars, StringSplitOptions.RemoveEmptyEntries).Length;
            }

            if (totalWordCount == 0)
            {
                return minimumTimeout;
            }

            float effectiveSpeechRate = settings.SpeechRate > 0.05f ? settings.SpeechRate : 1.0f;
            float estimatedSpeakingDuration = (totalWordCount / baseWordsPerSecond) / effectiveSpeechRate;
            float estimatedGenerationDuration = estimatedSpeakingDuration * TTSConstants.GetGenerationDurationScaling();
            float perWordBuffer = totalWordCount * TTSConstants.TTS_TIMEOUT_PER_WORD_BUFFER_SCALED;
            float total = estimatedGenerationDuration + perWordBuffer + TTSConstants.TTS_TIMEOUT_BASE_BUFFER_SCALED;

            TimeSpan calculated = TimeSpan.FromSeconds(total);
            return calculated > minimumTimeout ? calculated : minimumTimeout;
        }
    }
}
