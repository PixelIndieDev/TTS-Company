using System.Threading;
using UnityEngine;

namespace TTSCompany.Components.Server.Components
{
    internal sealed class ActiveTTSState
    {
        internal Coroutine Coroutine;
        internal CancellationTokenSource Cts;
        internal ulong NetworkObjectId;
    }
}
