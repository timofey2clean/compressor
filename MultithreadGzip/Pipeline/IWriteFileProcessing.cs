using System;
using System.IO;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.Pipeline
{
    interface IWriteFileProcessing
    {
        void WriteBlock(CDataBlock block);
        void WriteHeader(СProcessingParams options);
        void WriteTail(int blockSize);
    }

    class CWriteCompressedFile : IWriteFileProcessing
    {
        private readonly FileStream _fileStream;
        private readonly IDiskUsageCounter _diskUsageCounter;

        public CWriteCompressedFile(FileStream fileStream, IDiskUsageCounter diskUsageCounter)
        {
            _fileStream = fileStream;
            _diskUsageCounter = diskUsageCounter;
        }

        public void WriteBlock(CDataBlock block)
        {
            byte[] sizeBytes = BitConverter.GetBytes(block.Size);
            _diskUsageCounter.EnterIOOperation();
            _fileStream.Write(sizeBytes, 0, sizeBytes.Length);
            _fileStream.Write(block.Buffer, 0, block.Buffer.Length);
            _diskUsageCounter.LeaveIOOperation();
        }

        public void WriteHeader(СProcessingParams archiveParams)
        {
            archiveParams.WaitParamsSet();

            byte[] blocksTotalByteArray = BitConverter.GetBytes(archiveParams.BlocksTotalCount);

            _diskUsageCounter.EnterIOOperation();
            _fileStream.Write(blocksTotalByteArray, 0, blocksTotalByteArray.Length);
            _diskUsageCounter.LeaveIOOperation();
        }

        public void WriteTail(int lastBlockSize)
        {
            byte[] lastBlockSizeConverted = BitConverter.GetBytes(lastBlockSize);

            _diskUsageCounter.EnterIOOperation();
            _fileStream.Write(lastBlockSizeConverted, 0, lastBlockSizeConverted.Length);
            _diskUsageCounter.LeaveIOOperation();
        }
    }

    class CWriteOrigFile : IWriteFileProcessing
    {
        private readonly FileStream _fileStream;
        private readonly IDiskUsageCounter _diskUsageCounter;

        public CWriteOrigFile(FileStream fileStream, IDiskUsageCounter diskUsageCounter)
        {
            _fileStream = fileStream;
            _diskUsageCounter = diskUsageCounter;
        }

        public void WriteBlock(CDataBlock block)
        {
            _diskUsageCounter.EnterIOOperation();
            _fileStream.Write(block.Buffer, 0, block.Buffer.Length);
            _diskUsageCounter.LeaveIOOperation();
        }
        
        public void WriteHeader(СProcessingParams options)
        {
            options.WaitParamsSet();
        }

        public void WriteTail(int blockSize)
        {
            //Do nothing when decompressing
        }
    }
}
