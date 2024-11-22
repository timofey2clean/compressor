using System;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip.Pipeline
{
    class CCompressor
    {
        private readonly EWorkMode _mode;
        private readonly CReadQueue _readQueue;
        private readonly CWriteQueue _writeQueue;
        private readonly ManualResetEvent _abortEvent;
        private readonly Thread _thread;

        public CCompressor(string name, EWorkMode mode, CReadQueue readQueue, CWriteQueue writeQueue, ManualResetEvent abortEvent)
        {
            Name = name;
            _mode = mode;
            _readQueue = readQueue;
            _writeQueue = writeQueue;
            _abortEvent = abortEvent;
            
            FinishedEvent = new ManualResetEvent(false);
            _thread = new Thread(Do) { Name = name, IsBackground = true, Priority = CPipelineManager.THREAD_PRIORITY };
        }

        public string Name { get; private set; }
        public Exception ErrorException { get; private set; }
        public ManualResetEvent FinishedEvent { get; private set; }

        public void Start()
        {
            _thread.Start();
        }

        public void Do()
        {
            try
            {
                ICompressionProcessing compressionProcessing = SelectProcessingMode(_mode);

                while (true)
                {
                    CDataBlock block = _readQueue.Dequeue();
                    if (block == null)
                        break;

                    _writeQueue.Enqueue(compressionProcessing.ProcessBlock(block));
                }
            }
            catch (Exception ex)
            {
                ErrorException = ex;
                _abortEvent.Set();
            }
            finally
            {
                FinishedEvent.Set();
            }
        }

        private static ICompressionProcessing SelectProcessingMode(EWorkMode mode)
        {
            switch (mode)
            {
                case EWorkMode.Compress:
                case EWorkMode.Append:
                    return new CCompress();
                case EWorkMode.Decompress:
                    return new CDecompress();
                case EWorkMode.Browse:
                default:
                    throw new ArgumentException(string.Format("Compressor got invalid work mode \"{0}\".", mode.ToString()));
            }
        }
    }
}
