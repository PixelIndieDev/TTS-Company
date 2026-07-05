using Unity.Netcode;
using UnityEngine;

namespace TTSCompany.Components.Networking.Components.Structs
{
    internal readonly struct TTSSpeakTTS_PLUS_NET
    {
        [SerializeField] internal readonly NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal readonly ulong _callingAssemblyHash;
        [SerializeField] internal readonly string[] _textsToSpeak;
        [SerializeField] internal readonly PiperVoiceSettings _voiceSettings;
        [SerializeField] internal readonly ulong _trackingKeyHash;
        [SerializeField] internal readonly ulong _sessionId;

        internal TTSSpeakTTS_PLUS_NET(TTSSpeakTTS_NET nonLiteVersion, ulong sessionId)
        {
            _networkObjectRefOfSpeaker = nonLiteVersion._networkObjectRefOfSpeaker;
            _textsToSpeak = nonLiteVersion._textsToSpeak;
            _voiceSettings = nonLiteVersion._voiceSettings;
            _trackingKeyHash = nonLiteVersion._trackingKeyHash;
            _callingAssemblyHash = nonLiteVersion._callingAssemblyHash;
            _sessionId = sessionId;
        }
    }
}
