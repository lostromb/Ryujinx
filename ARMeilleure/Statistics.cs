using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ARMeilleure
{
    public static class Statistics
    {
        private const int ReportMaxFunctions = 100;

#pragma warning disable CS0169
        [ThreadStatic]
        private static Stopwatch _executionTimer;
#pragma warning restore CS0169

        private static ConcurrentDictionary<ulong, long> _ticksPerFunction;

        static Statistics()
        {
            _ticksPerFunction = new ConcurrentDictionary<ulong, long>();
        }

        [Conditional("M_PROFILE")]
        public static void InitializeTimer()
        {
            if (_executionTimer == null)
            {
                _executionTimer = new Stopwatch();
            }
        }

        [Conditional("M_PROFILE")]

        internal static void StartTimer()
        {
            _executionTimer.Restart();
        }

        [Conditional("M_PROFILE")]
        internal static void StopTimer(ulong funcAddr)
        {
            _executionTimer.Stop();

            long ticks = _executionTimer.ElapsedTicks;

            _ticksPerFunction.AddOrUpdate(funcAddr, ticks, (key, oldTicks) => oldTicks + ticks);
        }

        [Conditional("M_PROFILE")]
        internal static void ResumeTimer()
        {
            _executionTimer.Start();
        }

        [Conditional("M_PROFILE")]
        internal static void PauseTimer()
        {
            _executionTimer.Stop();
        }

        public static string GetReport()
        {
            int count = 0;

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(" Function address   | Time");
            sb.AppendLine("--------------------------");

            KeyValuePair<ulong, long>[] funcTable = _ticksPerFunction.ToArray();

            foreach (KeyValuePair<ulong, long> kv in funcTable.OrderByDescending(x => x.Value))
            {
                long timeInMs = (kv.Value * 1000) / Stopwatch.Frequency;

                sb.AppendLine($" 0x{kv.Key:X16} | {timeInMs} ms");

                if (count++ >= ReportMaxFunctions)
                {
                    break;
                }
            }

            return sb.ToString();
        }
    }
}