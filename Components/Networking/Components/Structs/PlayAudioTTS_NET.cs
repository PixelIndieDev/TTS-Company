using UnityEngine;

namespace TTSCompany.Components.Networking.Components.Structs
{
    internal readonly struct PlayAudioTTS_NET
    {
        [SerializeField] internal readonly ulong _taskId;
        [SerializeField] internal readonly int _startIndex;
        [SerializeField] internal readonly int _endIndex;
        [SerializeField] internal readonly bool _isLastBatch;

        internal PlayAudioTTS_NET(ulong taskId, int startIndex, int endIndex, bool isLastBatch)
        {
            _taskId = taskId;
            _startIndex = startIndex;
            _endIndex = endIndex;
            _isLastBatch = isLastBatch;
        }
    }
}
