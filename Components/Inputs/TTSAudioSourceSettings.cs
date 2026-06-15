using UnityEngine;

namespace TTS_Company.Components
{
    public class TTSAudioSourceSettings // public as this needs to be able to be accessed by other mods
    {
        public bool bypassEffects { get; set; } = false;
        public bool bypassListenerEffects { get; set; } = false;
        public bool bypassReverbZones { get; set; } = false;

        public int priority { get; set; } = 128;
        public float volume { get; set; } = 1.0f;

        public float spatialBlend { get; set; } = 1.0f;
        public float reverbZoneMix { get; set; } = 1.0f;

        public float dopplerLevel { get; set; } = 1.0f;
        public float minDistance { get; set; } = 1.0f;
        public float maxDistance { get; set; } = 40.0f;
        public AudioRolloffMode rolloffMode { get; set; } = AudioRolloffMode.Linear;
    }
}
