using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Ryujinx.Audio.Backends.SDL2
{
    [EventSource(Name = "Ryujinx.SDL2")]
    public sealed class SDL2EventSource : EventSource
    {
        public static readonly SDL2EventSource Instance = new SDL2EventSource();

        private IncrementingEventCounter _bytesQueuedCounter;

        private SDL2EventSource()
        {
            _bytesQueuedCounter = new IncrementingEventCounter("sdl-audio-bytes-queued", this)
            {
                DisplayName = "Audio Bytes Queued",
                DisplayUnits = "b"
            };
        }

        public void LogBytesQueued(double bytes)
        {
            _bytesQueuedCounter?.Increment(bytes);
        }

        protected override void Dispose(bool disposing)
        {
            _bytesQueuedCounter?.Dispose();
            _bytesQueuedCounter = null;
            base.Dispose(disposing);
        }
    }
}
