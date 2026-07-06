using TTSCompany.Components.Constants;
using TTSCompany.Components.Helpers;
using UnityEngine;

namespace TTSCompany.Components
{
    public sealed class PiperVoiceSettings // public as this needs to be able to be accessed by other mods
    {
        private string modelNameWithoutPathOrExtention = ExampleConstants.VOICE_MODEL_NAME;

        /// <summary>The file name of the .onnx voice model to use for speech generation</summary>
        [SerializeField]
        public string ModelName
        {
            get => modelNameWithoutPathOrExtention;
            set
            {
                string cleanedValue = VoiceHelper.CleanupVoiceModelname(value);
                if (modelNameWithoutPathOrExtention != cleanedValue)
                {
                    modelNameWithoutPathOrExtention = cleanedValue;
                }
            }
        }

        private float speechRate = 1.0f;
        /// <summary>Multiplier for how fast the voice speaks (values below 1.0 speed up speech, values above 1.0 slow it down, e.g. 0.5 = twice as fast, 2.0 = half speed)</summary>
        [SerializeField]
        public float SpeechRate
        {
            get => speechRate;
            set => speechRate = ClampHelper.ClampAndRound(value, 0.05f, 5.0f);
        }

        private float noiseScale = 0.67f;
        /// <summary>Controls pitch and intonation variation (higher values make the voice sound more expressive and varied, lower values make it more flat and monotone)</summary>
        [SerializeField]
        public float NoiseScale
        {
            get => noiseScale;
            set => noiseScale = ClampHelper.ClampAndRound(value, 0f, 1f);
        }

        private float noiseScaleW = 0.8f;
        /// <summary>Controls variation in phoneme timing and pacing (higher values add natural rhythmic variety, lower values produce a stiffer more robotic cadence)</summary>
        [SerializeField]
        public float NoiseScaleW
        {
            get => noiseScaleW;
            set => noiseScaleW = ClampHelper.ClampAndRound(value, 0f, 1f);
        }

        private float sentenceSilence = 0.2f;
        /// <summary>The pause, in seconds, inserted after sentence-ending punctuation ("!", ".", "?", etc)</summary>
        [SerializeField]
        public float SentenceSilence
        {
            get => sentenceSilence;
            set => sentenceSilence = ClampHelper.ClampAndRound(value, 0f, 5f);
        }

        private float punctuationSilence = 0.08f;
        /// <summary>The pause, in seconds, inserted after mid-sentence punctuation (",", ":", ";", etc)</summary>
        [SerializeField]
        public float PunctuationSilence
        {
            get => punctuationSilence;
            set => punctuationSilence = ClampHelper.ClampAndRound(value, 0f, 2.5f);
        }
    }
}