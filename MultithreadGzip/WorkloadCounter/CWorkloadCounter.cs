using System;

namespace MultiThreadGzip.WorkloadCounter
{
    public struct SWorkLoadSpec
    {
        public int CpuLoad { get; set; }
        public int ReadLoad { get; set; }
        public int WriteLoad { get; set; }
    }

    class CWorkloadCounter
    {
        private readonly CCpuUsageCounter _cpuUsageCounter;
        private readonly CDiskUsageCounter _diskReadUsageCounter;
        private readonly CDiskUsageCounter _diskWriteUsageCounter;

        public CWorkloadCounter()
        {
            _cpuUsageCounter = new CCpuUsageCounter();
            _diskReadUsageCounter = new CDiskUsageCounter();
            _diskWriteUsageCounter = new CDiskUsageCounter();
        }

        public IDiskUsageCounter DiskReadCounter
        {
            get { return _diskReadUsageCounter; }
        }

        public IDiskUsageCounter DiskWriteCounter
        {
            get { return _diskWriteUsageCounter; }
        }
        
        public SWorkLoadSpec GetLoadSpec()
        {
            SWorkLoadSpec spec = new SWorkLoadSpec()
            {
                CpuLoad = _cpuUsageCounter.AverageLoadPercent,
                ReadLoad = _diskReadUsageCounter.AverageLoadPercent,
                WriteLoad = _diskWriteUsageCounter.AverageLoadPercent
            };

            return spec;
        }

        public void StartCollecting()
        {
            _cpuUsageCounter.StartCollecting();
            _diskReadUsageCounter.StartCollecting();
            _diskWriteUsageCounter.StartCollecting();
        }

        public void StopCollecting()
        {
            StopCpuCounter();
            StopDiskCounters();
        }

        private void StopCpuCounter()
        {
            if(_cpuUsageCounter == null)
                throw new NullReferenceException("CPU counter is null.");

            if (_cpuUsageCounter.IsRunning)
                _cpuUsageCounter.StopCollecting();

            if (_cpuUsageCounter.Error != null)
                throw new Exception("CPU usage counter failed.", _cpuUsageCounter.Error);
        }

        private void StopDiskCounters()
        {
            if (_diskReadUsageCounter == null)
                throw new NullReferenceException("Disk read usage counter is null.");
            if (_diskWriteUsageCounter == null)
                throw new NullReferenceException("Disk write usage counter is null.");

            _diskReadUsageCounter.StopCollecting();
            _diskWriteUsageCounter.StopCollecting();
        }
    }
}
