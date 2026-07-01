using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal readonly struct TTSSpeakTTS_NET
    {
        [SerializeField] internal readonly NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal readonly ulong _callingAssemblyHash;
        [SerializeField] internal readonly string[] _textsToSpeak;
        [SerializeField] internal readonly PiperVoiceSettings _voiceSettings;
        [SerializeField] internal readonly TTSAudioSourceSettings _audioSourceSettings;
        [SerializeField] internal readonly ulong _trackingKeyHash;

        internal TTSSpeakTTS_NET(NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, string[] textToSpeak, PiperVoiceSettings voiceSettings, TTSAudioSourceSettings audioSourceSettings, ulong trackingKeyHash)
        {
            _networkObjectRefOfSpeaker = networkObjectRefOfSpeaker;
            _callingAssemblyHash = callingAssemblyHash;
            _textsToSpeak = textToSpeak;
            _voiceSettings = voiceSettings;
            _audioSourceSettings = audioSourceSettings;
            _trackingKeyHash = trackingKeyHash;
        }
    }
}
