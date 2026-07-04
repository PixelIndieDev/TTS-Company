using TTS_Company.Components.Constants;
using TTS_Company.Components.Helpers;
using UnityEngine;

namespace TTS_Company.Components
{
    public sealed class PiperVoiceSettings // public as this needs to be able to be accessed by other mods
    {
        private string modelNameWithoutPathOrExtention = ExampleConstants.VOICE_MODEL_NAME;

        /// <summary>Name of the .onnx model file</summary>
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
        /// <summary>Speech speed multiplier. 1.0 = normal, 0.5 = double speed, 2.0 = half speed</summary>
        [SerializeField]
        public float SpeechRate
        {
            get => speechRate;
            set => speechRate = ClampHelper.ClampAndRound(value, 0.05f, 5.0f);
        }

        private float noiseScale = 0.67f;
        /// <summary>Controls pitch and intonation variance. Higher values increase emotional expressiveness, while lower values make the voice more stable but potentially monotone</summary>
        [SerializeField]
        public float NoiseScale
        {
            get => noiseScale;
            set => noiseScale = ClampHelper.ClampAndRound(value, 0f, 1f);
        }

        private float noiseScaleW = 0.8f;
        /// <summary>Controls phoneme duration and pacing variance. Higher values add natural rhythmic changes, while lower values make the speech cadence rigid and robotic.</summary>
        [SerializeField]
        public float NoiseScaleW
        {
            get => noiseScaleW;
            set => noiseScaleW = ClampHelper.ClampAndRound(value, 0f, 1f);
        }

        private float sentenceSilence = 0.2f;
        /// <summary>The time in seconds for how long the model does silence between sentences</summary>
        [SerializeField]
        public float SentenceSilence
        {
            get => sentenceSilence;
            set => sentenceSilence = ClampHelper.ClampAndRound(value, 0f, 5f);
        }

        private float punctuationSilence = 0.08f;
        /// <summary>The time in seconds for how long the model does silence after punctuations</summary>
        [SerializeField]
        public float PunctuationSilence
        {
            get => punctuationSilence;
            set => punctuationSilence = ClampHelper.ClampAndRound(value, 0f, 2.5f);
        }
    }
}