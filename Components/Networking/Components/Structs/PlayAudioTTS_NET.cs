using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal struct PlayAudioTTS_NET
    {
        [SerializeField] internal ulong _taskId;
        [SerializeField] internal int _startIndex;
        [SerializeField] internal int _endIndex;

        internal PlayAudioTTS_NET(ulong taskId, int startIndex, int endIndex)
        {
            _taskId = taskId;
            _startIndex = startIndex;
            _endIndex = endIndex;
        }
    }
}
