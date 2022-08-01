﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx
{
    public static class StopwatchExtensions
    {
        /// <summary>
        /// Retrieves the most precise count of total milliseconds elapsed from the given Stopwatch.
        /// </summary>
        /// <param name="watch"></param>
        /// <returns></returns>
        public static double ElapsedMillisecondsPrecise(this Stopwatch watch)
        {
            return ((double)watch.ElapsedTicks * 1000d / (double)Stopwatch.Frequency);
        }
    }
}
