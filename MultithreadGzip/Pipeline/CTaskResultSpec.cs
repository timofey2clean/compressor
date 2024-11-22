using System;
using System.Collections.Generic;
using MultiThreadGzip.MultiFileCompressor;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.Pipeline
{
    public class CTaskResultSpec
    {
        private DateTime _startTime;
        private DateTime _endTime;

        public CTaskResultSpec(EWorkMode mode, string origFileName, long origSize)
        {
            Mode = mode;
            OrigFileName = origFileName;
            OrigSize = origSize;
            TaskResultType = EResultType.None;
            WorkerResults = new List<CWorkerResultSpec>();
        }

        public bool IsCanceled { get; set; }
        public long CompressedSize { get; set; }
        public long ArchiveStartOffset { get; set; }
        public SWorkLoadSpec WorkLoad { get; set; }
        public EResultType TaskResultType { get; set; }
        public List<CWorkerResultSpec> WorkerResults { get; private set; }
        public long OrigSize { get; private set; }
        public string OrigFileName { get; private set; }
        public EWorkMode Mode { get; private set; }

        public TimeSpan Duration
        {
            get { return _endTime - _startTime; }
        }

        public void SetStartTimeNow()
        {
            _startTime = DateTime.Now;
        }

        public void SetEndTimeNow()
        {
            _endTime = DateTime.Now;
        }

        public void AddWorkerThreadResult(CWorkerResultSpec workerResultSpec)
        {
            if (workerResultSpec == null)
                return;

            if (WorkerResults == null)
                throw new NullReferenceException("Worker threads results array is null.");

            WorkerResults.Add(workerResultSpec);

            switch (workerResultSpec.ResultType)
            {
                case EResultType.Failed:
                case EResultType.None: // Considering None is a failure.
                    TaskResultType = EResultType.Failed;
                    break;
                case EResultType.Warning:
                    if (TaskResultType == EResultType.Success)
                        TaskResultType = EResultType.Warning;
                    break;
                case EResultType.Success:
                    if (TaskResultType != EResultType.Failed && TaskResultType != EResultType.Warning)
                        TaskResultType = EResultType.Success;
                    break;
                default:
                    throw new ArgumentException("Unexpected worker thread result type.");
            }
        }
    }
}
