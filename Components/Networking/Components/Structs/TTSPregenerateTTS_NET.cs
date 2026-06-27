using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal struct TTSPregenerateTTS_NET
    {
        [SerializeField] internal ulong _callingAssemblyHash;
        [SerializeField] internal string[] _textsToSpeak;
        [SerializeField] internal PiperVoiceSettings _voiceSettings;
        [SerializeField] internal ulong _trackingKeyHash;

        internal TTSPregenerateTTS_NET(ulong callingAssemblyHash, string[] textToSpeak, PiperVoiceSettings voiceSettings, ulong trackingKeyHash)
        {
            _callingAssemblyHash = callingAssemblyHash;
            _textsToSpeak = textToSpeak;
            _voiceSettings = voiceSettings;
            _trackingKeyHash = trackingKeyHash;
        }
    }
}
