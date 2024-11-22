using System;
using System.Collections.Generic;

namespace MultiThreadGzip.MultiFileCompressor
{
    public enum EWorkMode
    {
        Compress,
        Decompress,
        Browse,
        Append
    }

    public abstract class CProcessingSpec
    {
        public const string ARCHIVE_VERSION = "myGZv1.1";
        public const int DEFAULT_BLOCK_SIZE = 4 * 1024 * 1024;
        
        public static readonly int COMPRESSION_THREADS_COUNT = Environment.ProcessorCount;

        protected CProcessingSpec(EWorkMode mode, string archiveFileName)
        {
            Mode = mode;
            ArchiveFileName = archiveFileName;
        }

        public EWorkMode Mode { get; private set; }
        public string ArchiveFileName { get; private set; }
    }

    public class CBrowseSpec : CProcessingSpec
    {
        public CBrowseSpec(string archiveFileName) :
            base(EWorkMode.Browse, archiveFileName)
        {
        }
    }

    public class CCompressionSpec : CProcessingSpec
    {
        public CCompressionSpec(EWorkMode mode, string arhiveFileName, CObjectInArchive[] objectsInArchive, int blockSize = DEFAULT_BLOCK_SIZE)
            : base(mode, arhiveFileName)
        {
            ArchiveObjects = objectsInArchive;
            BlockSize = blockSize;
            ThreadCount = COMPRESSION_THREADS_COUNT;
        }

        public int BlockSize { get; private set; }
        public int ThreadCount { get; private set; }
        public CObjectInArchive[] ArchiveObjects { get; private set; }
    }

    public class CDecompressionSpec : CProcessingSpec
    {
        public CDecompressionSpec(string arhiveFileName, string targetPath, EDecompressionMode decompressionMode, IEnumerable<string> insidePaths2Decompress)
            : base(EWorkMode.Decompress, arhiveFileName)
        {
            TargetPath = targetPath;
            DecompressionMode = decompressionMode;
            ThreadCount = COMPRESSION_THREADS_COUNT;

            InsidePaths2Decompress = new List<string>();
            if (insidePaths2Decompress != null)
                InsidePaths2Decompress.AddRange(insidePaths2Decompress);
        }

        public enum EDecompressionMode { AllFiles, MultipleFiles, SingleFile };

        public EDecompressionMode DecompressionMode { get; private set; }
        public string TargetPath { get; private set; }
        public List<string> InsidePaths2Decompress { get; private set; }
        public int ThreadCount { get; private set; }
    }
}
