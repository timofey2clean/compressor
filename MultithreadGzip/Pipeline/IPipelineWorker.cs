using System.Threading;

namespace MultiThreadGzip.Pipeline
{
    interface IPipelineWorker
    {
        CWorkerResultSpec ResultSpec { get; } 
        ManualResetEvent FinishedEvent { get; }
        void Start();
    }
}