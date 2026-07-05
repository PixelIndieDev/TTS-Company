using System;
using System.Threading;
using System.Threading.Tasks;
using TTSCompany.Components.Helpers;

namespace TTSCompany.Components.Server.Components
{
    internal sealed class BusyGeneration
    {
        internal readonly CancellationTokenSource Cts = new CancellationTokenSource();
        internal readonly Task<TTSResult> Task;
        private readonly object _lock = new object();
        private int _waiterCount;
        private bool _finalized;

        internal BusyGeneration(Func<CancellationToken, Task<TTSResult>> factory)
        {
            Task = factory(Cts.Token);
        }

        internal bool TryAddBusy()
        {
            lock (_lock)
            {
                if (_finalized) return false;
                _waiterCount++;
                return true;
            }
        }

        internal void RemoveBusy()
        {
            lock (_lock)
            {
                if (--_waiterCount <= 0)
                {
                    _finalized = true;
                    CtsHelper.SafeCancel(Cts);
                }
            }
        }
    }
}
