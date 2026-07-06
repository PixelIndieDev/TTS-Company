using UnityEngine;
using UnityEngine.Audio;

namespace TTSCompany.Components
{
    public sealed class TTSAudioSourceSettings // public as this needs to be able to be accessed by other mods
    {
        /// <summary>If true, skips any audio filter effects (e.g. reverb, distortion) applied to this audio source's GameObject</summary>
        [SerializeField] public bool BypassEffects { get; set; } = false;

        /// <summary>If true, skips any effects applied to the Audio Listener</summary>
        [SerializeField] public bool BypassListenerEffects { get; set; } = false;

        /// <summary>If true, ignores any reverb zones the audio would otherwise pass through</summary>
        [SerializeField] public bool BypassReverbZones { get; set; } = false;

        private int priority = 128;
        /// <summary>Playback priority relative to other sounds (Lower values are higher priority, 0 is highest priority, 256 is lowest)</summary>
        [SerializeField]
        public int Priority
        {
            get => priority;
            set => priority = Mathf.Clamp(value, 0, 256);
        }

        private float volume = 1.0f;
        /// <summary>The playback volume</summary>
        [SerializeField]
        public float Volume
        {
            get => volume;
            set => volume = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float spatialBlend = 1.0f;
        /// <summary>How much the sound is treated as 3D (positional) versus 2D (0.0 is fully 2D, 1.0 is fully 3D/spatial)</summary>
        [SerializeField]
        public float SpatialBlend
        {
            get => spatialBlend;
            set => spatialBlend = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float reverbZoneMix = 1.0f;
        /// <summary>How much of the audio signal is routed to reverb zones</summary>
        [SerializeField]
        public float ReverbZoneMix
        {
            get => reverbZoneMix;
            set => reverbZoneMix = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float dopplerLevel = 0.0f;
        /// <summary>How strong the Doppler effect is when the source or listener is moving</summary>
        [SerializeField]
        public float DopplerLevel
        {
            get => dopplerLevel;
            set => dopplerLevel = Mathf.Clamp(value, 0.0f, 1.0f);
        }

        private float minDistance = 1.0f;
        /// <summary>The distance within which the sound stays at full volume without attenuating (3D sounds only)</summary>
        [SerializeField]
        public float MinDistance
        {
            get => minDistance;
            set => minDistance = Mathf.Clamp(value, 0.0f, 128.0f);
        }

        private float maxDistance = 40.0f;
        /// <summary>The distance beyond which the sound stops attenuating further (3D sounds only)</summary>
        [SerializeField]
        public float MaxDistance
        {
            get => maxDistance;
            set => maxDistance = Mathf.Clamp(value, 1.0f, 128.0f);
        }

        /// <summary>How the volume fades out with distance between MinDistance and MaxDistance (e.g. Linear, Logarithmic, or Custom)</summary>
        [SerializeField] public AudioRolloffMode RolloffMode { get; set; } = AudioRolloffMode.Linear;

        /// <summary>The Audio Mixer Group this source routes its output through, if any</summary>
        [SerializeField] public AudioMixerGroup OutputAudioMixerGroup { get; set; } = null;

        /// <summary>A custom volume-over-distance curve, used when RolloffMode is set to Custom</summary>
        [SerializeField] public (AudioSourceCurveType type, AnimationCurve curve)? CustomCurve { get; set; } = null;
    }
}
