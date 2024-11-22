using System;
using System.IO;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.Pipeline
{
    class COrigFileReader : IPipelineWorker
    {
        const string WORKER_NAME = "Original file reader";

        private readonly СProcessingParams _processingParams;
        private readonly IThreadSafeQueue _queue;
        private readonly Thread _thread;
        private readonly ManualResetEvent _abortEvent;
        private readonly IDiskUsageCounter _diskUsageCounter;

        public COrigFileReader(СProcessingParams options, IThreadSafeQueue queue, ManualResetEvent abortEvent, IDiskUsageCounter diskUsageCounter)
        {
            _processingParams = options;
            _queue = queue;
            _abortEvent = abortEvent;
            _diskUsageCounter = diskUsageCounter;

            ResultSpec = new CWorkerResultSpec(WORKER_NAME);
            FinishedEvent = new ManualResetEvent(false);
            _thread = new Thread(Do) { Name = WORKER_NAME, IsBackground = true, Priority = CPipelineManager.THREAD_PRIORITY };
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
                using (FileStream fileStream = new FileStream(_processingParams.TaskSettings.ObjInArchive.OriginalPath, FileMode.Open, FileAccess.Read))
                {
                    int blockSize = _processingParams.BlockOrigSize;
                    SetFileOptions(blockSize);

                    long blockCounter = 0;
                    while (!_abortEvent.WaitOne(0))
                    {
                        CDataBlock block = ReadBlock(fileStream, blockSize, blockCounter);

                        if (block.Size < 1 && blockCounter > 0) // If file size is 0 do not exit from loop first time.
                            break;

                        _queue.Enqueue(block);
                        blockCounter++;
                    }
                }

                ResultSpec.UpdateResultOptionally(EResultType.Success);
            }
            catch (Exception ex)
            {
                ResultSpec.AddError(ex);
                _abortEvent.Set();
            }
            finally
            {
                _queue.StopWaitingNewData();
                FinishedEvent.Set();
            }
        }

        private CDataBlock ReadBlock(FileStream fileStream, int blockSize, long blockCounter)
        {
            byte[] buffer = new byte[blockSize];
            
            _diskUsageCounter.EnterIOOperation();
            int readBytesCount = fileStream.Read(buffer, 0, buffer.Length);
            _diskUsageCounter.LeaveIOOperation();

            return new CDataBlock(blockCounter, readBytesCount, buffer);
        }

        private void SetFileOptions(int blockSize)
        {
            FileInfo srcFileInfo = new FileInfo(_processingParams.TaskSettings.ObjInArchive.OriginalPath);
            long origFileBlocksCount = srcFileInfo.Length / blockSize;
            int lastBlockOrigSize = (int)(srcFileInfo.Length % blockSize);

            if (lastBlockOrigSize != 0)
                origFileBlocksCount++;
            else if (lastBlockOrigSize == 0 && srcFileInfo.Length > 0)
                lastBlockOrigSize = _processingParams.BlockOrigSize;

            _processingParams.Update(_processingParams.BlockOrigSize, lastBlockOrigSize, origFileBlocksCount);
        }
    }
}
