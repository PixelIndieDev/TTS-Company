using Unity.Netcode;
using UnityEngine;

namespace TTSCompany.Components.Networking.Components.Structs
{
    internal readonly struct SpawnTTSAudioSource_NET
    {
        [SerializeField] internal readonly NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal readonly ulong _callingAssemblyHash;
        [SerializeField] internal readonly TTSAudioSourceSettings _audioSourceSettings;

        internal SpawnTTSAudioSource_NET(NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, TTSAudioSourceSettings audioSourceSettings)
        {
            _networkObjectRefOfSpeaker = networkObjectRefOfSpeaker;
            _callingAssemblyHash = callingAssemblyHash;
            _audioSourceSettings = audioSourceSettings;
        }
    }
}
