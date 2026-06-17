using System;
using System.Threading;
using System.Threading.Tasks;

namespace TTS_Company.Components.Server.Components
{
    internal sealed class BusyGeneration
    {
        internal readonly CancellationTokenSource Cts = new CancellationTokenSource();
        internal readonly Task<TTSResult> Task;
        private int _waiterCount;

        internal BusyGeneration(Func<CancellationToken, Task<TTSResult>> factory)
        {
            Task = factory(Cts.Token);
        }

        internal void AddBusy() => Interlocked.Increment(ref _waiterCount);

        internal void RemoveBusy()
        {
            if (Interlocked.Decrement(ref _waiterCount) <= 0)
            {
                Cts.Cancel();
            }
        }
    }
}
