using UnityEngine;

namespace TTS_Company.Components
{
    public sealed class TTSAudioSourceSettings // public as this needs to be able to be accessed by other mods
    {
        [SerializeField] public bool _bypassEffects { get; set; } = false;
        [SerializeField] public bool _bypassListenerEffects { get; set; } = false;
        [SerializeField] public bool _bypassReverbZones { get; set; } = false;

        [SerializeField] public int _priority { get; set; } = 128;
        [SerializeField] public float _volume { get; set; } = 1.0f;

        [SerializeField] public float _spatialBlend { get; set; } = 1.0f;
        [SerializeField] public float _reverbZoneMix { get; set; } = 1.0f;

        [SerializeField] public float _dopplerLevel { get; set; } = 1.0f;
        [SerializeField] public float _minDistance { get; set; } = 1.0f;
        [SerializeField] public float _maxDistance { get; set; } = 40.0f;

        [SerializeField] public AudioRolloffMode _rolloffMode { get; set; } = AudioRolloffMode.Linear;
    }
}
