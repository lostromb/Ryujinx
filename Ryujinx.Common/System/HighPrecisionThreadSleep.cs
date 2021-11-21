using Ryujinx.Common.Logging;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Common.System
{
    /// <summary>
    /// <para>
    /// Implements a global background timer which we can use to implement the equivalent
    /// to Thread.Sleep() with more precision than what is normally provided by the runtime.
    /// </para>
    /// <para>
    /// Each C# app domain has an internal timer that runs at 60hz that is used for
    /// waking up threads from Thread.Sleep, updating DateTime.Now, and a few other things.
    /// What this means for us is that if we try to do Thread.Sleep(1) to wait just a tiny bit,
    /// the wait actually extends to 16ms, which could be enough to cause stutter or throw off
    /// the caller. This class provides a static interface to a global (platform-specific) timer
    /// which tries to make such short waits more precise.
    /// </para>
    /// </summary>
    public static class HighPrecisionThreadSleep
    {
        /// <summary>
        /// Non-null on Windows platforms
        /// </summary>
        private static readonly WindowsMultimediaThreadSleep _winmmTimer;

        /// <summary>
        /// Static constructor means that we potentially start a background timer as soon as this type is accessed.
        /// This could potentially have some implications for reflection or JIT, if some other process
        /// is dynamically loading this assembly, it will also invoke this static constructor. But
        /// we can live with it.
        /// </summary>
        static HighPrecisionThreadSleep()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _winmmTimer = new WindowsMultimediaThreadSleep();
            }
        }

        /// <summary>
        /// Higher-precision alternative to <see cref="Thread.Sleep"/>. Tells the caller to
        /// go to sleep and to try and wake up after the specified number of milliseconds have elapsed.
        /// </summary>
        /// <param name="milliseconds">The time to wait, in milliseconds</param>
        /// <param name="cancelToken">A cancel token for the sleep</param>
        public static void Wait(int milliseconds, CancellationToken cancelToken = default)
        {
            if (_winmmTimer != null)
            {
                _winmmTimer.Wait(milliseconds, cancelToken);
            }
            else
            {
                // Fallback to thread sleep on other platforms
                Thread.Sleep(milliseconds);
            }
        }
    }
}