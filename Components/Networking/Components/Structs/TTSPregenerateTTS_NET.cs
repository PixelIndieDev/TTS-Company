using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal readonly struct TTSPregenerateTTS_NET
    {
        [SerializeField] internal readonly ulong _callingAssemblyHash;
        [SerializeField] internal readonly string[] _textsToSpeak;
        [SerializeField] internal readonly PiperVoiceSettings _voiceSettings;
        [SerializeField] internal readonly ulong _trackingKeyHash;

        internal TTSPregenerateTTS_NET(ulong callingAssemblyHash, string[] textToSpeak, PiperVoiceSettings voiceSettings, ulong trackingKeyHash)
        {
            _callingAssemblyHash = callingAssemblyHash;
            _textsToSpeak = textToSpeak;
            _voiceSettings = voiceSettings;
            _trackingKeyHash = trackingKeyHash;
        }
    }
}
