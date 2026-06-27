using UnityEngine;

namespace TTS_Company.Components.Networking.Components.Structs
{
    internal struct SentenceProgressData_NET
    {
        [SerializeField] internal ulong _sessionId;
        [SerializeField] internal int _textIndex;
        [SerializeField] internal bool _success; // false = generation failed/cancelled for this client

        internal SentenceProgressData_NET(ulong sessionId, int textIndex, bool success)
        {
            _sessionId = sessionId;
            _textIndex = textIndex;
            _success = success;
        }
    }
}
