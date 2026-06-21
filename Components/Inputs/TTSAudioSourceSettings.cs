using UnityEngine;

namespace TTS_Company.Components
{
    public struct TTSAudioSourceSettings // public as this needs to be able to be accessed by other mods
    {
        public bool _bypassEffects;
        public bool _bypassListenerEffects;
        public bool _bypassReverbZones;

        public int _priority;
        public float _volume;

        public float _spatialBlend;
        public float _reverbZoneMix;

        public float _dopplerLevel;
        public float _minDistance;
        public float _maxDistance;

        public AudioRolloffMode _rolloffMode;

        internal TTSAudioSourceSettings(bool bypassEffects = false, bool bypassListenerEffects = false, bool bypassReverbZones = false, int priority = 128, float volume = 1.0f, float spatialBlend = 1.0f, float reverbZoneMix = 1.0f, float dopplerLevel = 1.0f, float minDistance = 1.0f, float maxDistance = 40.0f, AudioRolloffMode rolloffMode = AudioRolloffMode.Linear)
        {
            _bypassEffects = bypassEffects;
            _bypassListenerEffects = bypassListenerEffects;
            _bypassReverbZones = bypassReverbZones;

            _priority = priority;
            _volume = volume;

            _spatialBlend = spatialBlend;
            _reverbZoneMix = reverbZoneMix;

            _dopplerLevel = dopplerLevel;
            _minDistance = minDistance;
            _maxDistance = maxDistance;

            _rolloffMode = rolloffMode;
        }
    }
}
