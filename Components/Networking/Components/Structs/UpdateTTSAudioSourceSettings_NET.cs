using Unity.Netcode;
using UnityEngine;

namespace TTSCompany.Components.Networking.Components.Structs
{
    internal struct UpdateTTSAudioSourceSettings_NET
    {
        [SerializeField] internal NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal ulong _callingAssemblyHash;
        [SerializeField] internal TTSAudioSourceSettings _audioSourceSettings;

        internal UpdateTTSAudioSourceSettings_NET(NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash, TTSAudioSourceSettings audioSourceSettings)
        {
            _networkObjectRefOfSpeaker = networkObjectRefOfSpeaker;
            _callingAssemblyHash = callingAssemblyHash;
            _audioSourceSettings = audioSourceSettings;
        }
    }
}
