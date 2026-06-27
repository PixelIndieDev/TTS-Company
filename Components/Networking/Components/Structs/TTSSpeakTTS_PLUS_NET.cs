using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal struct TTSSpeakTTS_PLUS_NET
    {
        [SerializeField] internal NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal ulong _callingAssemblyHash;
        [SerializeField] internal string[] _textsToSpeak;
        [SerializeField] internal PiperVoiceSettings _voiceSettings;
        [SerializeField] internal TTSAudioSourceSettings _audioSourceSettings;
        [SerializeField] internal ulong _trackingKeyHash;
        [SerializeField] internal ulong _sessionId;

        internal TTSSpeakTTS_PLUS_NET(TTSSpeakTTS_NET nonLiteVersion, ulong sessionId)
        {
            _networkObjectRefOfSpeaker = nonLiteVersion._networkObjectRefOfSpeaker;
            _textsToSpeak = nonLiteVersion._textsToSpeak;
            _voiceSettings = nonLiteVersion._voiceSettings;
            _audioSourceSettings = nonLiteVersion._audioSourceSettings;
            _trackingKeyHash = nonLiteVersion._trackingKeyHash;
            _callingAssemblyHash = nonLiteVersion._callingAssemblyHash;
            _sessionId = sessionId;
        }
    }
}
