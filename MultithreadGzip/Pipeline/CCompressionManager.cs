using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip.Pipeline
{
    class CCompressionManager : IPipelineWorker
    {
        private const string WORKER_NAME = "Compression manager";

        private readonly EWorkMode _mode;
        private readonly int _threadCount;
        private readonly CReadQueue _readQueue;
        private readonly CWriteQueue _writeQueue;
        private readonly Thread _thread;
        private readonly ManualResetEvent _abortEvent;
        private readonly List<CCompressor> _compressors;

        public CCompressionManager(EWorkMode mode, int threadCount, CReadQueue readQueue, CWriteQueue writeQueue, ManualResetEvent abortEvent)
        {
            _mode = mode;
            _threadCount = threadCount;
            _readQueue = readQueue;
            _writeQueue = writeQueue;
            _abortEvent = abortEvent;

            ResultSpec = new CWorkerResultSpec(WORKER_NAME);
            FinishedEvent = new ManualResetEvent(false);

            _compressors = new List<CCompressor>();
            _thread = new Thread(Do) { Name = WORKER_NAME, IsBackground = true };
        }

        public ManualResetEvent FinishedEvent { get; private set; }
        
        public CWorkerResultSpec ResultSpec { get; private set; }

        public void Start()
        {
            _thread.Start();
        }

        private void Do()
        {
            try
            {
                StartCompressionThreads();
                WaitCompressorsFinished();
                SaveCompressorsErrors();
                ResultSpec.UpdateResultOptionally(EResultType.Success);
            }
            catch (Exception ex)
            {
                ResultSpec.AddError(ex);
                _abortEvent.Set();
            }
            finally
            {
                _writeQueue.StopWaitingNewData();
                FinishedEvent.Set();
            }
        }

        private void StartCompressionThreads()
        {
            for (int i = 0; i < _threadCount; i++)
            {
                CCompressor compressor =
                    new CCompressor(string.Format("Compressor #{0}", i), _mode, _readQueue, _writeQueue, _abortEvent);
                _compressors.Add(compressor);
                compressor.Start();
            }
        }

        private void WaitCompressorsFinished()
        {
            WaitHandle[] compressorsFinishedEvents = new WaitHandle[_compressors.Count];

            for(int i = 0; i < _compressors.Count; i++)
            {
                compressorsFinishedEvents[i] = _compressors[i].FinishedEvent;
            }

            if (compressorsFinishedEvents.Length > 0)
            {
                WaitHandle.WaitAll(compressorsFinishedEvents);
            }
        }

        private void SaveCompressorsErrors()
        {
            foreach (CCompressor compressor in _compressors.Where(compressor => compressor.ErrorException != null))
            {
                ResultSpec.AddError(
                    new Exception(string.Format("Exception thrown in thread \"{0}\" .", compressor.Name), compressor.ErrorException));
            }
        }
    }
}
