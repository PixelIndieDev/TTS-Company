using System;
using System.IO;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components
{
    public class PiperVoiceSettings // public as this needs to be able to be accessed by other mods
    {
        /// <summary>Path to the .onnx model file (e.g. "en_US-amy-medium").</summary>
        private string modelNameWithoutPathOrExtention = "en_US-hfc_female-medium"; // lowercase as private
        public string ModelName
        {
            get => Path.Combine(TTSConstants.TTS_VOICE_FOLDER_PREFIX, (modelNameWithoutPathOrExtention + ".onnx"));
            set => modelNameWithoutPathOrExtention = value != null && value.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) ? value.Substring(0, value.Length - 5) : value;
        }

        /// <summary>Speech speed multiplier. 1.0 = normal, 0.5 = half speed, 2.0 = double speed.</summary>
        public float SpeechRate { get; set; } = 1.0f;

        /// <summary>Noise scale for vocoder. Controls audio variation (default 0.667).</summary>
        public float NoiseScale { get; set; } = 0.667f;

        /// <summary>Noise scale for phoneme duration (default 0.8).</summary>
        public float NoiseScaleW { get; set; } = 0.8f;

        /// <summary>How many seconds are added after each sentence</summary>
        public float SentenceSilence { get; set; } = 0.2f;
    }
}