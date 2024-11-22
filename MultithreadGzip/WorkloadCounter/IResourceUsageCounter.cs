namespace MultiThreadGzip.WorkloadCounter
{
    interface IResourceUsageCounter
    {
        void StartCollecting();
        void StopCollecting();

        int AverageLoadPercent { get; }
    }
}
