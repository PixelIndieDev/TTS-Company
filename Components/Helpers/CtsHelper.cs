using System;
using System.Threading;
using TTS_Company.Components.Constants;

namespace TTS_Company.Components.Helpers
{
    internal static class CtsHelper
    {
        internal static void SafeCancel(this CancellationTokenSource cts)
        {
            if (cts == null)
            {
                return;
            }

            try 
            {
                cts.Cancel(); 
            }
            catch (ObjectDisposedException)
            {
                LogConstants.CODE_GENERIC_CATCH.Log(nameof(CtsHelper), nameof(SafeCancel));
            }
        }
    }
}
