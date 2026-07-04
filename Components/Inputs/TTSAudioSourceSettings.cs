using UnityEngine;
using UnityEngine.Audio;

namespace TTS_Company.Components
{
    public sealed class TTSAudioSourceSettings // public as this needs to be able to be accessed by other mods
    {
        [SerializeField] public bool _bypassEffects { get; set; } = false;
        [SerializeField] public bool _bypassListenerEffects { get; set; } = false;
        [SerializeField] public bool _bypassReverbZones { get; set; } = false;

        private int priority = 128;
        [SerializeField]
        public int Priority
        {
            get => priority;
            set => priority = Mathf.Clamp(value, 0, 255);
        }

        private float volume = 1.0f;
        [SerializeField]
        public float Volume
        {
            get => volume;
            set => volume = Mathf.Clamp(value, 0.0f, 1.0f);
        }
        [SerializeField] public bool _mute { get; set; } = false;

        private float spatialBlend = 1.0f;
        [SerializeField]
        public float SpatialBlend
        {
            get => spatialBlend;
            set => spatialBlend = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float reverbZoneMix = 1.0f;
        [SerializeField]
        public float ReverbZoneMix
        {
            get => reverbZoneMix;
            set => reverbZoneMix = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float dopplerLevel = 0.0f;
        [SerializeField]
        public float DopplerLevel
        {
            get => dopplerLevel;
            set => dopplerLevel = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float minDistance = 1.0f;
        [SerializeField] public float _minDistance { get; set; } = 1.0f;
        public float MinDistance
        {
            get => minDistance;
            set => minDistance = Mathf.Clamp(value, 0.0f, 128.0f);
        }

        private float maxDistance = 40.0f;
        [SerializeField]
        public float MaxDistance
        {
            get => maxDistance;
            set => maxDistance = Mathf.Clamp(value, 0.0f, 128.0f);
        }

        [SerializeField] public AudioRolloffMode _rolloffMode { get; set; } = AudioRolloffMode.Linear;

        [SerializeField] public AudioMixerGroup _outputAudioMixerGroup { get; set; } = null;
        [SerializeField] public (AudioSourceCurveType type, AnimationCurve curve)? _customCurve { get; set; } = null;
    }
}
