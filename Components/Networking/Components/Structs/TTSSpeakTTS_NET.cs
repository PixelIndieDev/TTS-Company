using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal struct TTSSpeakTTS_NET
    {
        [SerializeField] internal NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal ulong _callingAssemblyHash;
        [SerializeField] internal string[] _textsToSpeak;
        [SerializeField] internal PiperVoiceSettings _voiceSettings;
        [SerializeField] internal TTSAudioSourceSettings _audioSourceSettings;
        [SerializeField] internal ulong _trackingKeyHash;

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
