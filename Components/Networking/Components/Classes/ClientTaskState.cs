using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Networking.Components
{
    internal sealed class ClientTaskState
    {
        internal string[] _sentences;
        internal AudioClip[] _generatedClips;
        internal ulong _callingAssemblyHash;
        internal float[] _pauseDurations;
        internal CancellationTokenSource _cts;
        internal NetworkObjectReference _networkObjectReference;
    }
}
