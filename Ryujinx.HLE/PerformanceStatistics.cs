using Ryujinx.Common;
using Ryujinx.HLE.HOS.Diagnostics.Demangler.Ast;
using Ryujinx.HLE.HOS.Services.Arp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Ryujinx.HLE
{
    public class PerformanceStatistics
    {
        private const int PercentileSampleSize = 8000;
        private readonly MovingPercentile _frametimePercentiles;
        private static CancellationTokenSource _currentThreadCancel;
        private double _numPerfSamples = 0;

        private const int FrameTypeGame   = 0;
        private const int PercentTypeFifo = 0;

        private double[] _frameRate;
        private double[] _accumulatedFrameTime;
        private double[] _previousFrameTime;

        private double[] _averagePercent;
        private double[] _accumulatedActiveTime;
        private double[] _percentLastEndTime;
        private double[] _percentStartTime;

        private long[]   _framesRendered;
        private double[] _percentTime;

        private object[] _frameLock;
        private object[] _percentLock;

        private double _ticksToSeconds;

        private System.Timers.Timer _resetTimer;

        public PerformanceStatistics()
        {
            _frametimePercentiles = new MovingPercentile(PercentileSampleSize, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9);
            _frameRate            = new double[1];
            _accumulatedFrameTime = new double[1];
            _previousFrameTime    = new double[1];

            _averagePercent        = new double[1];
            _accumulatedActiveTime = new double[1];
            _percentLastEndTime    = new double[1];
            _percentStartTime      = new double[1];

            _framesRendered = new long[1];
            _percentTime    = new double[1];

            _frameLock   = new object[] { new object() };
            _percentLock = new object[] { new object() };

            _resetTimer = new System.Timers.Timer(750);

            _resetTimer.Elapsed += ResetTimerElapsed;
            _resetTimer.AutoReset = true;

            _resetTimer.Start();

            _ticksToSeconds = 1.0 / PerformanceCounter.TicksPerSecond;

            // cancel any previously running statistics thread
            if (_currentThreadCancel != null)
            {
                _currentThreadCancel.Cancel();
                _currentThreadCancel.Dispose();
            }

            _currentThreadCancel = new CancellationTokenSource();

            Task.Run(async () =>
            {
                CancellationToken cancelTokenClosure = _currentThreadCancel.Token;
                try
                {
                    while (!cancelTokenClosure.IsCancellationRequested)
                    {
                        await Task.Delay(10000, cancelTokenClosure);
                        lock (_frameLock[0])
                        {
                            if (_frametimePercentiles.NumSamples < 100)
                            {
                                continue;
                            }

                            // Calculate statistics
                            double medianFrametime = _frametimePercentiles.GetPercentile(0.5);
                            double medianFps = 1000 / Math.Max(1, medianFrametime);
                            double p99Frametime = _frametimePercentiles.GetPercentile(0.99);
                            double p99Fps = 1000 / Math.Max(1, p99Frametime);

                            // Any drop below either 30fps, or 1/3 the fps of median, counts as a stutter frame
                            double stutterThreshold = Math.Max(34, medianFrametime * 3);
                            double msSpentInStutter = 0;
                            double meanFrametime = 0;
                            double frametimeVariance = 0;
                            foreach (double measurement in _frametimePercentiles.GetMeasurements())
                            {
                                meanFrametime += measurement;
                                if (measurement > stutterThreshold)
                                {
                                    msSpentInStutter += measurement;
                                }
                            }

                            meanFrametime /= (double)_frametimePercentiles.NumSamples;
                            double meanFps = 1000 / Math.Max(1, meanFrametime);

                            foreach (double measurement in _frametimePercentiles.GetMeasurements())
                            {
                                double frametimeDelta = measurement - meanFrametime;
                                frametimeVariance += frametimeDelta * frametimeDelta;
                            }

                            frametimeVariance /= _frametimePercentiles.NumSamples;
                            double frametimeStdDev = Math.Sqrt(frametimeVariance);
                            double msToRenderAllFramesAtMeanRate = meanFrametime * _frametimePercentiles.NumSamples;
                            double stutterPercentage = 100 * msSpentInStutter / Math.Max(1, msToRenderAllFramesAtMeanRate);
                            double cycles = _numPerfSamples / (double)PercentileSampleSize;

                            Console.WriteLine("Statistics at cycle " + cycles);
                            Console.WriteLine("FPS Mean | Median | p99% | Frametime StdDev ms | Stutter %");
                            Console.WriteLine("---------|--------|------|------------------|---------");
                            Console.WriteLine("{0:F2} | {1:F2} | {2:F2} | {3:F2} | {4:F2}",
                                meanFps,
                                medianFps,
                                p99Fps,
                                frametimeStdDev,
                                stutterPercentage);
                            Console.WriteLine("{0} | {1} | {2} | {3} | {4}",
                                ConvertNumberToRating(meanFps, FpsRatings, "Perfect"),
                                ConvertNumberToRating(medianFps, FpsRatings, "Perfect"),
                                ConvertNumberToRating(p99Fps, Fps99Ratings, "Perfect"),
                                ConvertNumberToRating(frametimeStdDev, FrametimeDeviationRatings, "Distracting"),
                                ConvertNumberToRating(stutterPercentage, StutterPercentageRatings, "Awful Stutter"));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Performance statistics thread stopped");
                }
            });
        }

        private static readonly Tuple<double, string>[] FpsRatings = new Tuple<double, string>[]
        {
            new Tuple<double, string>(10, "Unplayable"),
            new Tuple<double, string>(20, "Bad"),
            new Tuple<double, string>(24, "Playable"),
            new Tuple<double, string>(29, "Good"),
            new Tuple<double, string>(55, "Great"),
        };

        private static readonly Tuple<double, string>[] Fps99Ratings = new Tuple<double, string>[]
        {
            new Tuple<double, string>(7, "Unplayable"),
            new Tuple<double, string>(14, "Bad"),
            new Tuple<double, string>(18, "Playable"),
            new Tuple<double, string>(25, "Good"),
            new Tuple<double, string>(45, "Great"),
        };

        private static readonly Tuple<double, string>[] FrametimeDeviationRatings = new Tuple<double, string>[]
        {
            new Tuple<double, string>(5, "Perfect"),
            new Tuple<double, string>(8, "Great"),
            new Tuple<double, string>(16, "OK"),
            new Tuple<double, string>(24, "Inconsistent"),
            new Tuple<double, string>(35, "Choppy"),
        };

        private static readonly Tuple<double, string>[] StutterPercentageRatings = new Tuple<double, string>[]
        {
            new Tuple<double, string>(0.01, "Perfect"),
            new Tuple<double, string>(0.5, "Super smooth"),
            new Tuple<double, string>(1, "Not too bad"),
            new Tuple<double, string>(3, "Small hitches"),
            new Tuple<double, string>(4, "Noticeable stutter"),
            new Tuple<double, string>(7, "Distracting stutter"),
        };

        private static string ConvertNumberToRating(double value, Tuple<double, string>[] options, string defaultResponse)
        {
            foreach (var option in options)
            {
                if (value <= option.Item1)
                {
                    return option.Item2;
                }
            }

            return defaultResponse;
        }

        private void ResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            CalculateFrameRate(FrameTypeGame);
            CalculateAveragePercent(PercentTypeFifo);
        }

        private void CalculateFrameRate(int frameType)
        {
            double frameRate = 0;

            lock (_frameLock[frameType])
            {
                if (_accumulatedFrameTime[frameType] > 0)
                {
                    frameRate = _framesRendered[frameType] / _accumulatedFrameTime[frameType];
                }

                _frameRate[frameType]            = frameRate;
                _framesRendered[frameType]       = 0;
                _accumulatedFrameTime[frameType] = 0;
            }
        }

        private void CalculateAveragePercent(int percentType)
        {
            // If start time is non-zero, a percent reading is still being measured.
            // If there aren't any readings, the default should be 100% if still being measured, or 0% if not.
            double percent = (_percentStartTime[percentType] == 0) ? 0 : 100;

            lock (_percentLock[percentType])
            {
                if (_percentTime[percentType] > 0)
                {
                    percent = (_accumulatedActiveTime[percentType] / _percentTime[percentType]) * 100;
                }

                _averagePercent[percentType]        = percent;
                _percentTime[percentType]           = 0;
                _accumulatedActiveTime[percentType] = 0;
            }
        }

        public void RecordGameFrameTime()
        {
            RecordFrameTime(FrameTypeGame);
        }

        public void RecordFifoStart()
        {
            StartPercentTime(PercentTypeFifo);
        }

        public void RecordFifoEnd()
        {
            EndPercentTime(PercentTypeFifo);
        }

        private void StartPercentTime(int percentType)
        {
            double currentTime = PerformanceCounter.ElapsedTicks * _ticksToSeconds;

            _percentStartTime[percentType] = currentTime;
        }

        private void EndPercentTime(int percentType)
        {
            double currentTime       = PerformanceCounter.ElapsedTicks * _ticksToSeconds;
            double elapsedTime       = currentTime - _percentLastEndTime[percentType];
            double elapsedActiveTime = currentTime - _percentStartTime[percentType];

            lock (_percentLock[percentType])
            {
                _accumulatedActiveTime[percentType] += elapsedActiveTime;
                _percentTime[percentType]           += elapsedTime;
            }

            _percentLastEndTime[percentType] = currentTime;
            _percentStartTime[percentType]   = 0;
        }

        private void RecordFrameTime(int frameType)
        {
            double currentFrameTime = PerformanceCounter.ElapsedTicks * _ticksToSeconds;
            double elapsedFrameTime = currentFrameTime - _previousFrameTime[frameType];

            _previousFrameTime[frameType] = currentFrameTime;

            lock (_frameLock[frameType])
            {
                if (elapsedFrameTime < 4.000) // only record frames that took less than 4 seconds to render
                {
                    _frametimePercentiles.Add(elapsedFrameTime * 1000);
                    _numPerfSamples++;
                }

                _accumulatedFrameTime[frameType] += elapsedFrameTime;

                _framesRendered[frameType]++;
            }
        }

        public double GetGameFrameRate()
        {
            return _frameRate[FrameTypeGame];
        }

        public double GetFifoPercent()
        {
            return _averagePercent[PercentTypeFifo];
        }

        public double GetGameFrameTime()
        {
            return 1000 / _frameRate[FrameTypeGame];
        }
    }
}