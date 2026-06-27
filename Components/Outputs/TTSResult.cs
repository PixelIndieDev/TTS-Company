using UnityEngine;

namespace TTS_Company.Components
{
    public sealed class TTSResult
    {
        /// <summary>Unity AudioClip ready for playback. Null if conversion failed.</summary>
        public AudioClip AudioClip { get; set; } = null;

        /// <summary>True when audio was generated successfully.</summary>
        public bool Success { get; set; } = false;
    }
}
