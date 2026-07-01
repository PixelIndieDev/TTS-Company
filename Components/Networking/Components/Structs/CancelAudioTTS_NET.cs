using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal readonly struct CancelAudioTTS_NET
    {
        [SerializeField] internal readonly ulong _taskId;
        [SerializeField] internal readonly string _reason;

        internal CancelAudioTTS_NET(ulong taskId, string reason)
        {
            _taskId = taskId;
            _reason = reason;
        }
    }
}
