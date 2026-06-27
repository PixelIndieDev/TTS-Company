using System.Threading;

namespace TTS_Company.Components.Networking.Components.Classes.TTSTask
{
    internal class TTSTask_Base
    {
        internal ulong _taskId;
        internal ulong _callingAssemblyHash;
        internal string[] _textsToSpeak;
        internal PiperVoiceSettings _voiceSettings;

        internal bool _cancelled;
        internal CancellationTokenSource _cts;

        internal TTSTask_Base() { }
    }
}
