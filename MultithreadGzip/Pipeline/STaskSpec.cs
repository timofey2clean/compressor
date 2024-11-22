using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip.Pipeline
{
    struct STaskSpec
    {
        public STaskSpec(
            EWorkMode workMode,
            string archiveFileName,
            CObjectInArchive objInArchive,
            int compressionThreadCount,
            int blockSizeBytes)
            : this()
        {
            Mode = workMode;
            ArchiveFileName = archiveFileName;
            ObjInArchive = objInArchive;
            StartOffset = objInArchive.DataOffset;
            ThreadsCount = compressionThreadCount;
            BlockSize = blockSizeBytes;
        }

        public EWorkMode Mode { get; private set; }
        public string ArchiveFileName { get; private set; }
        public long StartOffset { get; private set; }
        public CObjectInArchive ObjInArchive { get; private set; }
        public int ThreadsCount { get; private set; }
        public int BlockSize { get; private set; }
    }
}
