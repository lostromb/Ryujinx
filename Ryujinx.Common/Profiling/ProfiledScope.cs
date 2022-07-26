using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Common.Profiling
{
    public class ProfiledScope : IDisposable
    {
        private readonly Stopwatch _timer;
        private readonly double _minValueToReport;
        private readonly Action<double> _eventSink;

        public ProfiledScope(Action<double> eventSink, double minimumValueToReport = 0)
        {
            _timer = Stopwatch.StartNew();
            _minValueToReport = minimumValueToReport;
            _eventSink = eventSink;
        }

        public void Dispose()
        {
            _timer.Stop();
            double value = _timer.ElapsedMillisecondsPrecise();
            if (value >= _minValueToReport)
            {
                _eventSink(value);
            }
        }
    }
}
