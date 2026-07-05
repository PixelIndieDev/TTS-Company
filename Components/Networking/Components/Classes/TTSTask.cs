using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TTSCompany.Components.Constants;
using Unity.Netcode;
using UnityEngine;

namespace TTSCompany.Components.Networking.Components
{
    internal sealed class TTSTask
    {
        internal ulong _taskId;
        internal ulong _callingAssemblyHash;
        internal string[] _textsToSpeak;
        internal PiperVoiceSettings _voiceSettings;
        internal NetworkObjectReference _speakingObject;

        // the snapshot of clients we are waiting on
        internal ConcurrentDictionary<ulong, bool> _snapshotClientIds; // ignore the bool

        // per-client per-sentence completion: clientId -> bool[sentenceIndex]
        internal ConcurrentDictionary<ulong, bool[]> _completionList;

        // next sentence index host is waiting to "release" for playback
        internal int _textsWaited = 0;
        internal int _amountOfTexts = 0; // responsible for max array index
        internal int _startSpeakingAtAmountOfFinishedTasks = 0; // Without this, the tts is more likely to stop mid sentence to wait, and if you wait for all tts to finish, then it delays the tts too much.
        internal int _lastStartSpeakingIndex = 0;

        internal bool _cancelled;
        internal CancellationTokenSource _cts;

        internal TTSTask(ulong[] expectedClients, int amountOfTexts)
        {
            _amountOfTexts = amountOfTexts - 1;
            _startSpeakingAtAmountOfFinishedTasks = Mathf.CeilToInt(Mathf.Clamp(amountOfTexts * TTSConstants.TTS_START_SPEAKING_AT_MULTIPLIER, TTSConstants.TTS_MINIMUM_START_INDEX, TTSConstants.TTS_MAXIMUM_START_INDEX));

            Dictionary<ulong, bool> clientDictionary = expectedClients.ToDictionary(id => id, id => false);
            _snapshotClientIds = new ConcurrentDictionary<ulong, bool>(clientDictionary);

            Dictionary<ulong, bool[]> completionDictionary = expectedClients.ToDictionary(id => id, _ => new bool[amountOfTexts]);
            _completionList = new ConcurrentDictionary<ulong, bool[]>(completionDictionary);
        }
    }
}
