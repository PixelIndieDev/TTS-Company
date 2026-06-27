using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal class CancelAudioTTS_NET
    {
        [SerializeField] internal ulong _taskId;
        [SerializeField] internal string _reason;

        internal CancelAudioTTS_NET(ulong taskId, string reason)
        {
            _taskId = taskId;
            _reason = reason;
        }
    }
}
