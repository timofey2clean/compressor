using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.Pipeline
{
    class CPipelineManager
    {
        public static ThreadPriority THREAD_PRIORITY = ThreadPriority.Normal;

        private СProcessingParams _processingParams;
        private List<IPipelineWorker> _pipelineWorkers;
        private CReadQueue _readQueue;
        private CWriteQueue _writeQueue;
        private ManualResetEvent _abortEvent;
        private CWorkloadCounter _workloadCounter;

        public CPipelineManager(STaskSpec settings)
        {
            Init(settings);
        }
        
        public SWorkLoadSpec Workload
        {
            get
            {
                if (_workloadCounter == null)
                    throw new Exception("Work load counter was not created.");

                return _workloadCounter.GetLoadSpec();
            }
        }

        public IEnumerable<CWorkerResultSpec> WorkersResults
        {
            get
            {
                if (_pipelineWorkers == null)
                    throw new NullReferenceException("Pipeline workers list is null.");

                return _pipelineWorkers.Select(worker => worker.ResultSpec);
            }
        }

        public long CompressedObjSize { get; private set; }

        private void Init(STaskSpec settings)
        {
            int queueLengthBlocks = settings.ThreadsCount * 3;
            _abortEvent = new ManualResetEvent(false);
            _processingParams = new СProcessingParams(settings, _abortEvent);
            _readQueue = new CReadQueue(queueLengthBlocks, _abortEvent);
            _writeQueue = new CWriteQueue(queueLengthBlocks, _abortEvent);
            _pipelineWorkers = new List<IPipelineWorker>();
            _workloadCounter = new CWorkloadCounter();

            InitPipelineMembers();
        }

        public void Process()
        {
            if (_pipelineWorkers == null || !_pipelineWorkers.Any())
                throw new Exception("Pipeline worker threads list is null or empty.");

            _workloadCounter.StartCollecting();
            
            StartAllWorkers();
            WaitProcessingFinished();
            
            _workloadCounter.StopCollecting();

            if (_processingParams.TaskSettings.Mode == EWorkMode.Compress || _processingParams.TaskSettings.Mode == EWorkMode.Append)
                CompressedObjSize = GetCompressedObjectSize();
        }

        public void Abort()
        {
            if (_abortEvent != null)
                _abortEvent.Set();
        }
        
        private long GetCompressedObjectSize()
        {
            CFileWriter writer = (CFileWriter) _pipelineWorkers.Find(x => x is CFileWriter);
            if (writer != null)
                return writer.LengthTotal;

            throw new Exception("Failed to get compressed object size.");
        }

        private void InitPipelineMembers()
        {
            switch (_processingParams.TaskSettings.Mode)
            {
                case EWorkMode.Compress:
                case EWorkMode.Append:
                    _pipelineWorkers.Add(new COrigFileReader(_processingParams, _readQueue, _abortEvent, _workloadCounter.DiskReadCounter));
                    break;
                case EWorkMode.Decompress:
                    _pipelineWorkers.Add(new CCompressedFileReader(_processingParams, _readQueue, _abortEvent, _workloadCounter.DiskReadCounter));
                    break;
                case EWorkMode.Browse:
                default:
                    throw new ArgumentException(string.Format("Pipeline manager got invalid work mode \"{0}\".", _processingParams.TaskSettings.Mode.ToString()));
            }

            _pipelineWorkers.Add(
                new CCompressionManager(
                    _processingParams.TaskSettings.Mode,
                    _processingParams.TaskSettings.ThreadsCount,
                    _readQueue,
                    _writeQueue,
                    _abortEvent));
            
            CFileWriter fileWriter = new CFileWriter(_processingParams, _writeQueue, _abortEvent, _workloadCounter.DiskWriteCounter);
            fileWriter.ProgressEvent += OnWriteFileProgress; // File writer shoots progress events when written a block.
            _pipelineWorkers.Add(fileWriter);
        }
        
        private void StartAllWorkers()
        {
            foreach (IPipelineWorker worker in _pipelineWorkers)
            {
                worker.Start();
            }
        }

        private void OnWriteFileProgress(int hundredthprcnt)
        {
            try
            {
                Notifier.Progress(hundredthprcnt, _workloadCounter.GetLoadSpec());
            }
            catch (Exception ex)
            {
                Notifier.Exception(ex);
            }
        }

        private void WaitProcessingFinished()
        {
            WaitHandle.WaitAll(_pipelineWorkers.Select(worker => worker.FinishedEvent).Cast<WaitHandle>().ToArray());
        }
    }
}
