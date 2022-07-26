using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using Durandal.Common.MathExt;
using System.Threading.Tasks;

namespace Ryujinx.Common.Profiling
{
    [EventSource(Name = "Ryujinx")]
    public sealed class RyujinxEventSource : EventSource
    {
        public static RyujinxEventSource Instance = new RyujinxEventSource();

        private EventCounter _presentFrameCounter;
        private MovingPercentile _presentFrameAnomalyDetector;

        private RyujinxEventSource()
        {
        }

        protected override void OnEventCommand(EventCommandEventArgs args)
        {
            if (args.Command == EventCommand.Enable)
            {
                _presentFrameAnomalyDetector = new MovingPercentile(100, 0.25, 0.50, 0.75, 0.95);
                _presentFrameCounter = new EventCounter("PresentFrame", this)
                {
                    DisplayName = "[HLE] Time Between Game Frames",
                    DisplayUnits = "ms",
                };
            }
        }

        [Event(ProfilingEventIds.PresentFrame, Level = EventLevel.Informational, Keywords = EventKeywords.None)]
        public void PresentFrame(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.PresentFrame, TimeMs);
                if (_presentFrameCounter != null)
                {
                    _presentFrameCounter.WriteMetric(TimeMs);
                }

                if (_presentFrameAnomalyDetector != null)
                {
                    _presentFrameAnomalyDetector.Add(TimeMs);
                    double median = _presentFrameAnomalyDetector.GetPercentile(0.5);
                    double p99 = _presentFrameAnomalyDetector.GetPercentile(0.99);
                    if (_presentFrameAnomalyDetector.NumSamples >= 100 &&
                        TimeMs >= p99 &&
                        p99 > (median * 2))
                    {
                        PresentFrameAnomaly(TimeMs);
                    }
                }
            }
        }

        [Event(ProfilingEventIds.PresentFrameAnomaly, Level = EventLevel.Warning, Keywords = EventKeywords.None)]
        private void PresentFrameAnomaly(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.PresentFrameAnomaly, TimeMs);
            }
        }

        [Event(ProfilingEventIds.CompileGraphicsShader, Level = EventLevel.Informational, Keywords = EventKeywords.None)]
        public void CompileGraphicsShader(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.CompileGraphicsShader, TimeMs);
            }
        }

        [Event(ProfilingEventIds.CompileComputeShader, Level = EventLevel.Informational, Keywords = EventKeywords.None)]
        public void CompileComputeShader(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.CompileComputeShader, TimeMs);
            }
        }

        [Event(ProfilingEventIds.AllocInternal, Level = EventLevel.Informational, Keywords = EventKeywords.None)]
        public void AllocInternal(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.AllocInternal, TimeMs);
            }
        }

        [Event(ProfilingEventIds.AllocInternal2, Level = EventLevel.Informational, Keywords = EventKeywords.None)]
        public void AllocInternal2(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.AllocInternal2, TimeMs);
            }
        }

        [Event(ProfilingEventIds.MapViewOfFile3, Level = EventLevel.Informational, Keywords = EventKeywords.None)]
        public void MapViewOfFile3(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.MapViewOfFile3, TimeMs);
            }
        }

        [Event(ProfilingEventIds.CreateSharedMemory, Level = EventLevel.Informational, Keywords = EventKeywords.None)]
        public void CreateSharedMemory(double TimeMs)
        {
            if (IsEnabled())
            {
                WriteEvent(ProfilingEventIds.CreateSharedMemory, TimeMs);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _presentFrameCounter?.Dispose();
            _presentFrameCounter = null;
            base.Dispose(disposing);
        }
    }
}
