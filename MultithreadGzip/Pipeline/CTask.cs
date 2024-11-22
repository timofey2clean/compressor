using System;
using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip.Pipeline
{
    interface ITask
    {
        CTaskResultSpec Execute();
        void Cancel();
    }

    class CTask : ITask
    {
        private readonly STaskSpec _taskSpec; 
        private readonly CTaskResultSpec _taskResult;
        private readonly CPipelineManager _pipelineManager;

        public CTask(STaskSpec taskSpec)
        {
            _taskSpec = taskSpec;
            _taskResult = new CTaskResultSpec(taskSpec.Mode, _taskSpec.ObjInArchive.OriginalPath, _taskSpec.ObjInArchive.OriginalSize);
            _pipelineManager = new CPipelineManager(_taskSpec);
        }

        public CTaskResultSpec Execute()
        {
            _taskResult.SetStartTimeNow();
            _pipelineManager.Process();
            _taskResult.SetEndTimeNow();
            
            foreach (CWorkerResultSpec workerResult in _pipelineManager.WorkersResults)
            {
                _taskResult.AddWorkerThreadResult(workerResult);
            }

            switch (_taskSpec.Mode)
            {
                case EWorkMode.Compress:
                case EWorkMode.Append:
                    _taskResult.CompressedSize = _pipelineManager.CompressedObjSize;
                    _taskResult.ArchiveStartOffset = _taskSpec.ObjInArchive.DataOffset;
                    break;
                case EWorkMode.Decompress:
                    _taskResult.CompressedSize = _taskSpec.ObjInArchive.CompressedSize;
                    break;
                case EWorkMode.Browse:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _taskResult.WorkLoad = _pipelineManager.Workload;

            return _taskResult;
        }

        public void Cancel()
        {
            _taskResult.IsCanceled = true;
            _taskResult.TaskResultType = EResultType.Failed;

            if (_pipelineManager != null)
                _pipelineManager.Abort();
        }
    }
}
