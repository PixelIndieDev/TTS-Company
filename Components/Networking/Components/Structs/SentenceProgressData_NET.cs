using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal readonly struct SentenceProgressData_NET
    {
        [SerializeField] internal readonly ulong _sessionId;
        [SerializeField] internal readonly int _textIndex;
        [SerializeField] internal readonly bool _success; // false = generation failed/cancelled for this client

        internal SentenceProgressData_NET(ulong sessionId, int textIndex, bool success)
        {
            _sessionId = sessionId;
            _textIndex = textIndex;
            _success = success;
        }
    }
}
