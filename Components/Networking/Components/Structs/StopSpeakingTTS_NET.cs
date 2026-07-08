using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal readonly struct StopSpeakingTTS_NET
    {
        [SerializeField] internal readonly NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal readonly ulong _callingAssemblyHash;

        internal StopSpeakingTTS_NET(NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash)
        {
            _networkObjectRefOfSpeaker = networkObjectRefOfSpeaker;
            _callingAssemblyHash = callingAssemblyHash;
        }
    }
}
