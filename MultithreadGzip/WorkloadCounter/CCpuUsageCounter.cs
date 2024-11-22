using System;
using System.Diagnostics;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip.WorkloadCounter
{
    class CCpuUsageCounter : IResourceUsageCounter
    {
        private const int CPU_COUNTER_PERIOD_MS = 1000; // Recommended timeout
        private const int MAX_STOP_TIMEOUT_MS = 60 * 1000;

        private readonly Thread _counterThread;
        private readonly ManualResetEvent _isStoppedEvent;
        private long _measuresCounter;
        private long _averageLoadPercent;

        public CCpuUsageCounter()
        {
            _isStoppedEvent = new ManualResetEvent(true);
            _counterThread = new Thread(CountAverageValuePeriodically) { Name = "CPU load counter", IsBackground = true };
        }

        public Exception Error { get; private set; }

        public int AverageLoadPercent
        {
            get { return (int)Interlocked.Read(ref _averageLoadPercent); }
        }

        public bool IsRunning
        {
            get { return !_isStoppedEvent.WaitOne(0); }
        }

        public void StartCollecting()
        {
            _isStoppedEvent.Reset();
            _counterThread.Start();
        }

        public void StopCollecting()
        {
            const int stopPeriodMs = 3000;
            
            _isStoppedEvent.Set();

            int stopTimeCounter = 0;
            while (!_counterThread.Join(TimeSpan.FromMilliseconds(stopPeriodMs)))
            {
                stopTimeCounter += stopPeriodMs;
                Notifier.Message(string.Format("CPU load counter thread did not finish within {0} sec.", (double)stopTimeCounter / 1000));

                if(MAX_STOP_TIMEOUT_MS <= stopTimeCounter)
                    throw new TimeoutException("Timed out waiting CPU load counter thread to stop.");
            }
        }

        private void CountAverageValuePeriodically()
        {
            const int dividedCount = 10;// To minimize stopping time
            const int smallTimeout = CPU_COUNTER_PERIOD_MS / dividedCount;

            CSystemCpuCounter.Inst.PassStopEvent(_isStoppedEvent);

            try
            {
                bool stopped = false;

                while (!stopped)
                {
                    UpdateAverageValue(CSystemCpuCounter.Inst.GetValue());

                    int tickTock = 0;
                    while (tickTock++ < dividedCount)
                    {
                        stopped = _isStoppedEvent.WaitOne(TimeSpan.FromMilliseconds(smallTimeout));
                    }
                }
            }
            catch (Exception ex)
            {
                Error = ex;
                Notifier.Exception(ex);
            }
        }

        private void UpdateAverageValue(float value)
        {
            long newValue = (long)((Interlocked.Read(ref _averageLoadPercent) * _measuresCounter + value) / ++_measuresCounter);
            Interlocked.Exchange(ref _averageLoadPercent, newValue);
        }

        class CSystemCpuCounter
        {
            private static readonly object Lck = new object();
            private static CSystemCpuCounter _inst;
            private readonly PerformanceCounter _perfCounter;
            private ManualResetEvent _stopEvent;

            private CSystemCpuCounter()
            {
                _perfCounter = new PerformanceCounter
                {
                    CategoryName = "Processor",
                    CounterName = "% Processor Time",
                    InstanceName = "_Total"
                };

                PrepareCpuCounter();
            }

            public static CSystemCpuCounter Inst // Using single not to initialize for every file.
            {
                get
                {
                    if (_inst == null)
                    {
                        lock (Lck)
                        {
                            if (_inst == null)
                            {
                                _inst = new CSystemCpuCounter();
                            }
                        }
                    }
                    return _inst;
                }
            }

            public float GetValue()
            {
                return _perfCounter.NextValue();
            }

            public void PassStopEvent(ManualResetEvent stopEvent)
            {
                _stopEvent = stopEvent;
            }

            private void PrepareCpuCounter()
            {// Workaround for strange behavior on first returned values(advised on MSDN)
                const int skipCount = 2;

                for (int i = 0; i < skipCount; i++)
                {
                    if (_stopEvent != null && _stopEvent.WaitOne(0)) // Stop event helps not to wait on small files.
                        break;

                    _perfCounter.NextValue();
                }
            }
        }
    }
}
