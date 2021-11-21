using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Common.System
{
    /// <summary>
    /// High precision wait provider which uses P/Invoke to access Windows multimedia timers.
    /// This timer is then used to accurately pulse waiting events to achieve precise delays with low overhead.
    /// See https://docs.microsoft.com/en-US/windows/win32/multimedia/multimedia-timers
    /// </summary>
    public class WindowsMultimediaThreadSleep
    {
        private const int MAX_WAITING_THREADS = 65535;
        private readonly SemaphoreSlim _tickSignal;
        private readonly TimerEventHandler _callbackHandler;
        private int _numWaitingThreads = 0;
        private long _previousTickTime;
        private uint _timerId;
        private int _disposed = 0;

        public WindowsMultimediaThreadSleep()
        {
            _tickSignal = new SemaphoreSlim(0, MAX_WAITING_THREADS);
            _previousTickTime = TimeGetTime();
            _callbackHandler = TimerHandler; // assign the callback method to an explicit variable to keep the reference alive in the garbage collector

            // Start the timer
            uint err = TimeBeginPeriod(1);
            if (err != 0)
            {
                throw new InvalidOperationException("Multimedia timer operation returned HRESULT " + err);
            }

            _timerId = TimeSetEvent(1, 0, _callbackHandler, IntPtr.Zero, eventType: 1);
        }

        ~WindowsMultimediaThreadSleep()
        {
            Dispose(false);
        }

        public void Wait(double milliseconds, CancellationToken cancelToken)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(WindowsMultimediaThreadSleep));
            }

            long targetTicks = (long)((milliseconds - 0.5) * (double)TimeSpan.TicksPerMillisecond);
            Stopwatch elapsedTime = Stopwatch.StartNew();
            Interlocked.Increment(ref _numWaitingThreads);
            try
            {
                while (elapsedTime.ElapsedTicks < targetTicks && _disposed == 0)
                {
                    _tickSignal.Wait();
                    cancelToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _numWaitingThreads);
            }
        }

        /// <summary>
        /// This implementation is not actually async since the task callbacks and signaling would cause too much allocation.
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public async Task WaitAsync(double milliseconds, CancellationToken cancelToken)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(WindowsMultimediaThreadSleep));
            }

            long targetTicks = (long)((milliseconds - 0.5) * (double)TimeSpan.TicksPerMillisecond);
            Stopwatch elapsedTime = Stopwatch.StartNew();
            Interlocked.Increment(ref _numWaitingThreads);
            try
            {
                while (elapsedTime.ElapsedTicks < targetTicks && _disposed == 0)
                {
                    // don't use cancellation token on the tick signal itself because it's running at very high precision,
                    // at which we point we don't care about the granularity of the cancel, and using the token causes lots of allocations.
                    // We could make this a synchronous wait to avoid Task<T> allocations, but doing so could also potentially block threads and cause thread pool starvation
                    await _tickSignal.WaitAsync().ConfigureAwait(false);
                    cancelToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _numWaitingThreads);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Prevent multiple disposal
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            // Stop the timer
            uint err = TimeKillEvent(_timerId);
            if (err != 0)
            {
                err = TimeEndPeriod(1);
            }

            // Wait for all waiting threads to finish
            Stopwatch timeout = Stopwatch.StartNew();
            while (_numWaitingThreads > 0 && timeout.ElapsedMilliseconds < 2000)
            {
                SignalAllThreads();
            }

            if (disposing)
            {
                _tickSignal?.Dispose();
            }
        }

        private void SignalAllThreads()
        {
            int waitingThreads = Math.Min(_numWaitingThreads - _tickSignal.CurrentCount, MAX_WAITING_THREADS);
            if (waitingThreads > 0)
            {
                _tickSignal.Release(waitingThreads);
            }
        }

        private void TimerHandler(int id, int msg, IntPtr user, int dw1, int dw2)
        {
            try
            {
                const double TOLERANCE = 0.95;
                long currentTime = TimeGetTime();

                if (currentTime - _previousTickTime >= TOLERANCE)
                {
                    _previousTickTime = currentTime;
                    SignalAllThreads();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception in high precision callback timer: " + e.Message);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TimeCaps
        {
            public uint wPeriodMin;
            public uint wPeriodMax;
        };

        private delegate void TimerEventHandler(int id, int msg, IntPtr user, int dw1, int dw2);

        [DllImport("winmm.dll", EntryPoint = "timeGetDevCaps", SetLastError = true)]
        private static extern uint TimeGetDevCaps(ref TimeCaps timeCaps, uint sizeTimeCaps);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeGetTime")]
        private static extern uint TimeGetTime();

        [DllImport("winmm.dll", EntryPoint = "timeSetEvent")]
        private static extern uint TimeSetEvent(int delay, int resolution, TimerEventHandler handler, IntPtr user, int eventType);

        [DllImport("winmm.dll", EntryPoint = "timeKillEvent")]
        private static extern uint TimeKillEvent(uint id);
    }
}
