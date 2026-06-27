using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal struct SpawnTTSAudioSource_NET
    {
        [SerializeField] internal NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal ulong _callingAssemblyHash;
        [SerializeField] internal TTSAudioSourceSettings _audioSourceSettings;

        internal SpawnTTSAudioSource_NET(NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, TTSAudioSourceSettings audioSourceSettings)
        {
            _networkObjectRefOfSpeaker = networkObjectRefOfSpeaker;
            _callingAssemblyHash = callingAssemblyHash;
            _audioSourceSettings = audioSourceSettings;
        }
    }
}
