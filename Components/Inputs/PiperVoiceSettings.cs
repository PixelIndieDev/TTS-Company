using System.IO;
using TTS_Company.Components.Constants;
using TTS_Company.Components.Helpers;
using UnityEngine;

namespace TTS_Company.Components
{
    public sealed class PiperVoiceSettings // public as this needs to be able to be accessed by other mods
    {
        [SerializeField] private string modelNameWithoutPathOrExtention = ExampleConstants.VOICE_MODEL_NAME; // lowercase as private
        [SerializeField] private string modelPath; // lowercase as private

        public PiperVoiceSettings()
        {
            UpdateModelPath();
        }

        /// <summary>Name0 of the .onnx model file</summary>
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
                    UpdateModelPath();
                }
            }
        }

        /// <summary>Path to the .onnx model file</summary>
        [SerializeField]
        public string ModelPath
        {
            get => modelPath;
            private set => modelPath = value;
        }

        private void UpdateModelPath()
        {
            ModelPath = Path.Combine(TTSConstants.TTS_VOICE_MODELS_FOLDER_LOCATION, (modelNameWithoutPathOrExtention + ".onnx"));
        }

        /// <summary>Speech speed multiplier. 1.0 = normal, 0.5 = half speed, 2.0 = double speed</summary>
        [SerializeField] public float SpeechRate { get; set; } = 1.0f;

        /// <summary>Noise scale for vocoder. Controls audio variation (default 0.667)</summary>
        [SerializeField] public float NoiseScale { get; set; } = 0.667f;

        /// <summary>Noise scale for phoneme duration (default 0.8)</summary>
        [SerializeField] public float NoiseScaleW { get; set; } = 0.8f;

        /// <summary>How many seconds are added after each sentence</summary>
        [SerializeField] public float SentenceSilence { get; set; } = 0.2f;
    }
}