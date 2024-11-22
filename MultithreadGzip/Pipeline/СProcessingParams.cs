using System.Threading;

namespace MultiThreadGzip.Pipeline
{
    class СProcessingParams
    {
        private readonly ManualResetEvent _abortEvent;
		private readonly AutoResetEvent _archiveParamsSetEvent;
        
        public СProcessingParams(STaskSpec taskSpec, ManualResetEvent abortEvent)
        {
            TaskSettings = taskSpec;
            BlockOrigSize = taskSpec.BlockSize;
            _abortEvent = abortEvent;
            _archiveParamsSetEvent = new AutoResetEvent(false);
        }

        public STaskSpec TaskSettings { get; private set; }
        public int BlockOrigSize { get; private set; }
        public int LastBlockOrigSize { get; private set; }
        public long BlocksTotalCount { get; private set; }

        public void Update(int blockSize, int lastOrigBlockSize, long sourceFileBlocksCount)
        {
            BlockOrigSize = blockSize;
            LastBlockOrigSize = lastOrigBlockSize;
            BlocksTotalCount = sourceFileBlocksCount;
            _archiveParamsSetEvent.Set();
        }

        public void WaitParamsSet()
        {
            WaitHandle.WaitAny(new WaitHandle[] { _abortEvent, _archiveParamsSetEvent });
        }
    }
}
