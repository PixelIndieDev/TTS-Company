using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal struct DespawnTTSAudioSource_NET
    {
        [SerializeField] internal NetworkObjectReference _networkObjectRefOfSpeaker;
        [SerializeField] internal ulong _callingAssemblyHash;

        internal DespawnTTSAudioSource_NET(NetworkObjectReference networkObjectRefOfSpeaker, ulong callingAssemblyHash)
        {
            _networkObjectRefOfSpeaker = networkObjectRefOfSpeaker;
            _callingAssemblyHash = callingAssemblyHash;
        }
    }
}
