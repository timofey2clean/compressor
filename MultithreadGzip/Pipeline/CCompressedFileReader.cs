using System;
using System.IO;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.Pipeline
{
    class CCompressedFileReader : IPipelineWorker
    {
        private const string WORKER_NAME = "Compressed file reader";
        private const int BLOCKSIZE_BYTES_LENGTH = sizeof(int);
        private const int BLOCKSCOUNT_BYTES_LENGTH = sizeof(long);

        private readonly СProcessingParams _archiveParams;
        private readonly IThreadSafeQueue _queue;
        private readonly Thread _thread;
        private readonly ManualResetEvent _abortEvent;
        private readonly IDiskUsageCounter _diskUsageCounter;
        private long _blockCounter;

        public CCompressedFileReader(СProcessingParams archiveParams, IThreadSafeQueue queue, ManualResetEvent abortEvent, IDiskUsageCounter diskUsageCounter)
        {
            _archiveParams = archiveParams;
            _queue = queue;
            _abortEvent = abortEvent;
            _blockCounter = 0;
            _diskUsageCounter = diskUsageCounter;

            ResultSpec = new CWorkerResultSpec(WORKER_NAME);
            FinishedEvent = new ManualResetEvent(false);
            _thread = new Thread(Do) { Name = WORKER_NAME, IsBackground = true, Priority = CPipelineManager.THREAD_PRIORITY};
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
                long objStartOffset = _archiveParams.TaskSettings.StartOffset;
                if(objStartOffset <= 0)
                    throw new ArgumentException("Object in archive start offset cannot be less or equal to zero.");

                using (FileStream fileStream = new FileStream(_archiveParams.TaskSettings.ArchiveFileName, FileMode.Open, FileAccess.Read))
                {
                    fileStream.Seek(objStartOffset, SeekOrigin.Begin);
                    ReadBlocksCount(fileStream);
                    
                    long blocksTotal = _archiveParams.BlocksTotalCount;
                    
                    long blockCounter = 0;
                    while (!_abortEvent.WaitOne(0))
                    {
                        bool EOF = (++blockCounter >= blocksTotal);
                        CDataBlock block = ReadBlock(fileStream, EOF);
                        _queue.Enqueue(block);

                        if (!EOF)
                            continue;

                        _archiveParams.Update(_archiveParams.BlockOrigSize, block.Size, _archiveParams.BlocksTotalCount);
                        break;
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

        private CDataBlock ReadBlock(FileStream fileStream, bool EOF)
        {
            int compressedBlockSize = ReadBlockSize(fileStream);
            if (compressedBlockSize < 0)
                throw new Exception("Compressed block size value is less than zero.");

            if(compressedBlockSize < 1)
                return new CDataBlock(0, 0, new byte[] { });

            byte[] data = new byte[compressedBlockSize];

            _diskUsageCounter.EnterIOOperation();
            fileStream.Read(data, 0, compressedBlockSize);
            _diskUsageCounter.LeaveIOOperation();

            int lastBlockSize = 0;

            if (EOF)
            {
                lastBlockSize = ReadBlockSize(fileStream);
            }
            
            int blockOrigSize = EOF ? lastBlockSize : _archiveParams.BlockOrigSize;

            return new CDataBlock(_blockCounter++, blockOrigSize, data);
        }

        private void ReadBlocksCount(FileStream fileStream)
        {
            byte[] blockNumberBytes = new byte[BLOCKSCOUNT_BYTES_LENGTH];

            _diskUsageCounter.EnterIOOperation();
            fileStream.Read(blockNumberBytes, 0, blockNumberBytes.Length);
            _diskUsageCounter.LeaveIOOperation();

            long blockCount = BitConverter.ToInt64(blockNumberBytes, 0);
            _archiveParams.Update(_archiveParams.BlockOrigSize, 0, blockCount);
        }

        private int ReadBlockSize(FileStream fileStream)
        {
            byte[] blockSizeBytes = new byte[BLOCKSIZE_BYTES_LENGTH];

            _diskUsageCounter.EnterIOOperation();
            fileStream.Read(blockSizeBytes, 0, blockSizeBytes.Length);
            _diskUsageCounter.EnterIOOperation();

            int convetredBlockSize = BitConverter.ToInt32(blockSizeBytes, 0);

            return convetredBlockSize;
        }
    }
}
