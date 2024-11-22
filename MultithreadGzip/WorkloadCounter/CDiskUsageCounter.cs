using System.Diagnostics;
using System.Threading;

namespace MultiThreadGzip.WorkloadCounter
{
    interface IDiskUsageCounter : IResourceUsageCounter
    {
        void EnterIOOperation();
        void LeaveIOOperation();
    }

    class CDiskUsageCounter : IDiskUsageCounter
    {
        private readonly Stopwatch _actualWorkStopwatch;
        private readonly Stopwatch _totalWorkStopwatch;
        private long _averageLoadPercent;

        public CDiskUsageCounter()
        {
            _actualWorkStopwatch = new Stopwatch();
            _totalWorkStopwatch = new Stopwatch();
        }

        public int AverageLoadPercent
        {
            get { return (int)Interlocked.Read(ref _averageLoadPercent); }
        }

        public void StartCollecting()
        {
            _totalWorkStopwatch.Start();
        }

        public void StopCollecting()
        {
            _totalWorkStopwatch.Stop();
        }

        public void EnterIOOperation()
        {
            _actualWorkStopwatch.Start();
        }

        public void LeaveIOOperation()
        {
            _actualWorkStopwatch.Stop();
            CalcAverageLoad();
        }

        private void CalcAverageLoad()
        {
            long newValue = (long)(100 * ((double)_actualWorkStopwatch.ElapsedMilliseconds / _totalWorkStopwatch.ElapsedMilliseconds));
            Interlocked.Exchange(ref _averageLoadPercent, newValue);
        }
    }
}
