using System;
using System.IO;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.Pipeline
{
    class CFileWriter : IPipelineWorker
    {
        const string WORKER_NAME = "File writer";

        private readonly СProcessingParams _processingParams;
        private readonly IThreadSafeQueue _queue;
        private readonly Thread _thread;
        private readonly ManualResetEvent _abortEvent;
        private readonly IDiskUsageCounter _diskUsageCounter;
        private readonly FileMode _fileMode;
        private FileStream _fileStream;
        
        public CFileWriter(СProcessingParams options, IThreadSafeQueue queue, ManualResetEvent abortEvent, IDiskUsageCounter diskUsageCounter)
        {
            _processingParams = options;
            _queue = queue;
            _abortEvent = abortEvent;
            _diskUsageCounter = diskUsageCounter;

            switch (_processingParams.TaskSettings.Mode)
            {
                case EWorkMode.Compress:
                    _fileMode = FileMode.OpenOrCreate;
                    break;
                case EWorkMode.Append:
                    _fileMode = FileMode.Open;
                    break;
                case EWorkMode.Decompress:
                    _fileMode = FileMode.CreateNew;
                    break;
                case EWorkMode.Browse:
                    break;
                default:
                    throw new ArgumentException("Unexpected work mode.");
            }

            ResultSpec = new CWorkerResultSpec(WORKER_NAME);
            FinishedEvent = new ManualResetEvent(false);
            _thread = new Thread(Do) {Name = WORKER_NAME, IsBackground = true, Priority = CPipelineManager.THREAD_PRIORITY};
        }

        public long StartOffset { get; private set; }
        public long LengthTotal { get; private set; }
        public CWorkerResultSpec ResultSpec { get; private set; }
        public ManualResetEvent FinishedEvent { get; private set; }

        public delegate void ProgressDelegate(int hundredthPrcnt);
        public event ProgressDelegate ProgressEvent;

        public void Start()
        {
            _thread.Start();
        }

        private static void PrepareTargetDirectory(string writeFileName)
        {
            if (string.IsNullOrEmpty(writeFileName))
                throw new ArgumentException("File name is null or empty.");

            string dir = Path.GetDirectoryName(writeFileName);
            if (string.IsNullOrEmpty(dir))
                throw new DirectoryNotFoundException("Failed to determine target directory path.");

            if (Directory.Exists(dir))
                return;
            
            Directory.CreateDirectory(dir);
            if(!Directory.Exists(dir))
                throw new DirectoryNotFoundException(string.Format("Failed to create directory \"{0}\".", dir));
        }

        private void Do()
        {
            try
            {
                string writeFileName = GetTargetFileName();

                if(_processingParams.TaskSettings.Mode == EWorkMode.Decompress)
                    PrepareTargetDirectory(writeFileName);

                LengthTotal = 0;
                using (_fileStream = new FileStream(writeFileName, _fileMode, FileAccess.Write))
                {
                    if (_processingParams.TaskSettings.Mode == EWorkMode.Compress || _processingParams.TaskSettings.Mode == EWorkMode.Append)
                        SeekToEndAndSaveOffset();

                    IWriteFileProcessing writeFileProcessing = SelectProcessingMode(_processingParams.TaskSettings.Mode);

                    writeFileProcessing.WriteHeader(_processingParams);

                    long blocksTotal = _processingParams.BlocksTotalCount;
                    long progressBlockCounter = 0;

                    TrackProgress(0, blocksTotal);

                    while (true)
                    {
                        CDataBlock block = _queue.Dequeue();
                        if (block == null) // Finished processing
                            break;

                        writeFileProcessing.WriteBlock(block);
                        LengthTotal += block.Buffer.Length;
                        TrackProgress(++progressBlockCounter, blocksTotal);
                    }

                    if (!_abortEvent.WaitOne(0))
                        writeFileProcessing.WriteTail(_processingParams.LastBlockOrigSize);
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
                FinishedEvent.Set();
            }
        }

        private void SeekToEndAndSaveOffset()
        {
            _fileStream.Seek(0, SeekOrigin.End); // Jump to the end of multi-file archive to write next compressed file.
            StartOffset = _fileStream.Position; // Remember the start offset for multi-file header.
        }

        private string GetTargetFileName()
        {
            switch (_processingParams.TaskSettings.Mode)
            {
                case EWorkMode.Compress:
                case EWorkMode.Append:
                    return _processingParams.TaskSettings.ArchiveFileName;
                case EWorkMode.Decompress:
                    return _processingParams.TaskSettings.ObjInArchive.OriginalPath;
                case EWorkMode.Browse:
                default:
                    throw new NotSupportedException("Not supported work mode.");
            }
        }

        private IWriteFileProcessing SelectProcessingMode(EWorkMode mode)
        {
            IWriteFileProcessing processing;
            switch (mode)
            {
                case EWorkMode.Compress:
                case EWorkMode.Append:
                    processing = new CWriteCompressedFile(_fileStream, _diskUsageCounter);
                    break;
                case EWorkMode.Decompress:
                    processing = new CWriteOrigFile(_fileStream, _diskUsageCounter);
                    break;
                case EWorkMode.Browse:
                default:
                    throw new ArgumentException(string.Format("File writer got invalid work mode \"{0}\".", mode.ToString()));
            }

            return processing;
        }

        private void TrackProgress(long counter, long total)
        {
            if (ProgressEvent == null || _abortEvent.WaitOne(0))
                return;

            if (total != 0)
                ProgressEvent((int) (100*100*counter/total));
            else
                ProgressEvent(100*100);
        }
    }
}