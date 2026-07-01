using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal readonly struct DespawnTTSAudioSource_NET
    {
        [SerializeField] internal readonly NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal readonly ulong _callingAssemblyHash;

        internal DespawnTTSAudioSource_NET(NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash)
        {
            _networkObjectRefOfSpeaker = networkObjectRefOfSpeaker;
            _callingAssemblyHash = callingAssemblyHash;
        }
    }
}
