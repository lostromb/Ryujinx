using OpenTK.Audio.OpenAL;
using Ryujinx.Audio.Backends.Common;
using Ryujinx.Audio.Common;
using Ryujinx.Common.Utilities;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Xml.Linq;

namespace Ryujinx.Audio.Backends.OpenAL
{
    class OpenALHardwareDeviceSession : HardwareDeviceSessionOutputBase
    {
        private const ulong DESIRED_BUFFERED_SAMPLES = Constants.TargetSampleRate * 25L / 1000; // Target 50ms of audio latency (for actual game audio)
        private const ulong MIN_BUFFERED_SAMPLES = Constants.TargetSampleRate * 10L / 1000; // At 10ms or below, inject silence
        private OpenALHardwareDeviceDriver _driver;
        private int _sourceId;
        private ALFormat _targetFormat;
        private bool _isActive;
        private Queue<OpenALAudioBuffer> _queuedBuffers;
        private ulong _playedSampleCount;
        private ulong _samplesBuffered;

        private object _lock = new object();

        public OpenALHardwareDeviceSession(OpenALHardwareDeviceDriver driver, IVirtualMemoryManager memoryManager, SampleFormat requestedSampleFormat, uint requestedSampleRate, uint requestedChannelCount, float requestedVolume) : base(memoryManager, requestedSampleFormat, requestedSampleRate, requestedChannelCount)
        {
            _driver = driver;
            _queuedBuffers = new Queue<OpenALAudioBuffer>();
            _sourceId = AL.GenSource();
            _targetFormat = GetALFormat();
            _isActive = false;
            _playedSampleCount = 0;
            _samplesBuffered = 0;
            SetVolume(requestedVolume);
        }

        private ALFormat GetALFormat()
        {
            switch (RequestedSampleFormat)
            {
                case SampleFormat.PcmInt16:
                    switch (RequestedChannelCount)
                    {
                        case 1:
                            return ALFormat.Mono16;
                        case 2:
                            return ALFormat.Stereo16;
                        case 6:
                            return ALFormat.Multi51Chn16Ext;
                        default:
                            throw new NotImplementedException($"Unsupported channel config {RequestedChannelCount}");
                    }
                default:
                    throw new NotImplementedException($"Unsupported sample format {RequestedSampleFormat}");
            }
        }

        public override void PrepareToClose() { }

        private void StartIfNotPlaying()
        {
            AL.GetSource(_sourceId, ALGetSourcei.SourceState, out int stateInt);

            ALSourceState State = (ALSourceState)stateInt;

            if (State != ALSourceState.Playing)
            {
                OpenALEventSource.Instance.StreamStarted();
                AL.SourcePlay(_sourceId);
            }
        }

        public override void QueueBuffer(AudioBuffer buffer)
        {
            lock (_lock)
            {
                OpenALAudioBuffer driverBuffer = new OpenALAudioBuffer
                {
                    DriverIdentifier = buffer.DataPointer,
                    BufferId = AL.GenBuffer(),
                    SampleCount = GetSampleCount(buffer)
                };

                AL.BufferData(driverBuffer.BufferId, _targetFormat, buffer.Data, (int)RequestedSampleRate);

                _queuedBuffers.Enqueue(driverBuffer);
                _samplesBuffered += driverBuffer.SampleCount;

                AL.SourceQueueBuffer(_sourceId, driverBuffer.BufferId);
                OpenALEventSource.Instance.BufferReceived(driverBuffer.BufferId, (int)driverBuffer.SampleCount);

                if (_isActive)
                {
                    StartIfNotPlaying();
                }

                OpenALEventSource.Instance.LogBytesQueued((double)buffer.DataSize);
            }
        }

        public override void SetVolume(float volume)
        {
            lock (_lock)
            {
                AL.Source(_sourceId, ALSourcef.Gain, volume);
            }
        }

        public override float GetVolume()
        {
            AL.GetSource(_sourceId, ALSourcef.Gain, out float volume);

            return volume;
        }

        public override void Start()
        {
            lock (_lock)
            {
                _isActive = true;

                StartIfNotPlaying();
            }
        }

        public override void Stop()
        {
            lock (_lock)
            {
                SetVolume(0.0f);

                AL.SourceStop(_sourceId);

                _isActive = false;
            }
        }

        public override void UnregisterBuffer(AudioBuffer buffer) { }

        public override bool WasBufferFullyConsumed(AudioBuffer buffer)
        {
            lock (_lock)
            {
                foreach (var driverBuffer in _queuedBuffers)
                {
                    if (driverBuffer.DriverIdentifier == buffer.DataPointer)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public override ulong GetPlayedSampleCount()
        {
            lock (_lock)
            {
                return _playedSampleCount;
            }
        }

        public bool Update()
        {
            OpenALEventSource.Instance.UpdateStarted((double)_samplesBuffered * 1000 / (double)RequestedSampleRate);
            lock (_lock)
            {
                if (_isActive)
                {
                    AL.GetSource(_sourceId, ALGetSourcei.BuffersProcessed, out int releasedCount);

                    if (releasedCount > 0)
                    {
                        int[] bufferIds = new int[releasedCount];

                        AL.SourceUnqueueBuffers(_sourceId, releasedCount, bufferIds);

                        int i = 0;

                        while (i < bufferIds.Length && _queuedBuffers.TryPeek(out OpenALAudioBuffer buffer))
                        {
                            OpenALEventSource.Instance.BufferDequeued(buffer.BufferId);
                            if (buffer.DriverIdentifier != 0)
                            {
                                _playedSampleCount += buffer.SampleCount;
                            }

                            _samplesBuffered -= buffer.SampleCount;
                            _queuedBuffers.Dequeue();
                            i++;
                        }

                        Debug.Assert(i == bufferIds.Length, "Unknown buffer ids found!");

                        AL.DeleteBuffers(bufferIds);
                    }

                    while (_samplesBuffered < MIN_BUFFERED_SAMPLES)
                    {
                        // Inject silence to make sure we have enough initial buffer to have smooth game audio
                        OpenALAudioBuffer silenceBuffer = new OpenALAudioBuffer
                        {
                            DriverIdentifier = 0,
                            BufferId = AL.GenBuffer(),
                            SampleCount = Constants.TargetSampleCount,
                        };

                        byte[] silenceData = new byte[Constants.TargetSampleCount * RequestedChannelCount * Constants.TargetSampleSize];
                        AL.BufferData(silenceBuffer.BufferId, _targetFormat, silenceData, (int)RequestedSampleRate);
                        AL.SourceQueueBuffer(_sourceId, silenceBuffer.BufferId);
                        _queuedBuffers.Enqueue(silenceBuffer);
                        OpenALEventSource.Instance.SilenceInjected((int)silenceBuffer.SampleCount);
                        _samplesBuffered += silenceBuffer.SampleCount;
                    }

                    if (_samplesBuffered < DESIRED_BUFFERED_SAMPLES)
                    {
                        OpenALEventSource.Instance.UpdateSignaled();
                    }

                    return _samplesBuffered < DESIRED_BUFFERED_SAMPLES;
                }

                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _driver.Unregister(this))
            {
                lock (_lock)
                {
                    PrepareToClose();
                    Stop();

                    AL.DeleteSource(_sourceId);
                }
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
