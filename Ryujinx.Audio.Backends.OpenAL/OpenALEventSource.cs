using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Ryujinx.Audio.Backends.OpenAL
{
    [EventSource(Name = "Ryujinx.OpenAL")]
    public sealed class OpenALEventSource : EventSource
    {
        public static readonly OpenALEventSource Instance = new OpenALEventSource();

        private EventCounter _driverThreadTimeSpentInUpdate;
        private EventCounter _driverThreadTimeSpentInSleep;
        private IncrementingEventCounter _bytesQueuedCounter;
        private IncrementingEventCounter _bufferUpdateSignalRateCounter;

        private OpenALEventSource()
        {
            _bytesQueuedCounter = new IncrementingEventCounter("oal-audio-bytes-queued", this)
            {
                DisplayName = "Audio Bytes Queued",
                DisplayUnits = "b"
            };
            _bufferUpdateSignalRateCounter = new IncrementingEventCounter("oal-audio-signal-rate", this)
            {
                DisplayName = "Buffer Signal Rate"
            };
            _driverThreadTimeSpentInUpdate = new EventCounter("oal-audio-driver-time", this)
            {
                DisplayName = "Audio Driver Time In Update",
                DisplayUnits = "ms"
            };
            _driverThreadTimeSpentInSleep = new EventCounter("oal-audio-driver-time-sleep", this)
            {
                DisplayName = "Audio Driver Time In Sleep",
                DisplayUnits = "ms"
            };
        }

        public void LogBytesQueued(double bytes)
        {
            _bytesQueuedCounter?.Increment(bytes);
        }

        public void LogBufferUpdateSignal()
        {
            _bufferUpdateSignalRateCounter?.Increment();
        }

        public void LogDriverTimeSpentInUpdate(double ms)
        {
            _driverThreadTimeSpentInUpdate.WriteMetric(ms);
        }

        public void LogDriverTimeSpentInSleep(double ms)
        {
            _driverThreadTimeSpentInSleep.WriteMetric(ms);
        }

        [Event(10, Message = "Buffer Received", Keywords = EventKeywords.None, Level = EventLevel.Informational)]
        public void BufferReceived(int BufferId, int Length)
        {
            WriteEvent(10, BufferId, Length);
        }

        [Event(11, Message = "Buffer Dequeued", Keywords = EventKeywords.None, Level = EventLevel.Informational)]
        public void BufferDequeued(int BufferId)
        {
            WriteEvent(11, BufferId);
        }

        [Event(12, Message = "Update Started", Keywords = EventKeywords.None, Level = EventLevel.Informational)]
        public void UpdateStarted(double MsBuffered)
        {
            WriteEvent(12, MsBuffered);
        }

        [Event(13, Message = "Update Signaled", Keywords = EventKeywords.None, Level = EventLevel.Informational)]
        public void UpdateSignaled()
        {
            WriteEvent(13);
        }

        [Event(14, Message = "Stream Started", Keywords = EventKeywords.None, Level = EventLevel.Informational)]
        public void StreamStarted()
        {
            WriteEvent(14);
        }

        [Event(15, Message = "Silence Injected", Keywords = EventKeywords.None, Level = EventLevel.Informational)]
        public void SilenceInjected(int Length)
        {
            WriteEvent(15, Length);
        }

        protected override void Dispose(bool disposing)
        {
            _bytesQueuedCounter?.Dispose();
            _bytesQueuedCounter = null;
            _bufferUpdateSignalRateCounter?.Dispose();
            _bufferUpdateSignalRateCounter = null;
            _driverThreadTimeSpentInUpdate?.Dispose();
            _driverThreadTimeSpentInUpdate = null;
            _driverThreadTimeSpentInSleep?.Dispose();
            _driverThreadTimeSpentInSleep = null;
            base.Dispose(disposing);
        }
    }
}
