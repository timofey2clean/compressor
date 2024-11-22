using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MultiThreadGzip.MultiFileCompressor.MetadataHandler;
using MultiThreadGzip.Pipeline;

namespace MultiThreadGzip.MultiFileCompressor
{
    public class CMultiFileCompressor : IObservable<INotificationSpec>
    {
        private volatile bool _isCanceled;
        private ITask _currentTask;
        
        public CMultiFileCompressor()
        {
        }

        public IDisposable Subscribe(IObserver<INotificationSpec> observer)
        {
            return Notifier.Subscribe(observer);
        }

        public CProcessingResultSpec Compress(CCompressionSpec spec)
        {
            try
            {
                ValidateProcessingSpec(spec);
                return ProcessCompression(spec);
            }
            catch (Exception ex)
            {
                Notifier.Exception(ex);
                return null;
            }
        }

        public CProcessingResultSpec Decompress(CDecompressionSpec spec)
        {
            try
            {
                ValidateProcessingSpec(spec);
                CArchiveMetadata meta = Browse(new CBrowseSpec(spec.ArchiveFileName));
                return meta != null ? ProcessDecompression(spec, meta) : null;
            }
            catch (Exception ex)
            {
                Notifier.Exception(ex);
                return null;
            }
        }

        public CArchiveMetadata Browse(CBrowseSpec spec)
        {
            try
            {
                ValidateProcessingSpec(spec);
                CMetadataHandler metaHandler = new CMetadataHandler(EWorkMode.Browse, spec.ArchiveFileName);
                return metaHandler.BrowseArchive();
            }
            catch (Exception ex)
            {
                Notifier.Exception(ex);
                return null;
            }
        }

        public CProcessingResultSpec Append(CCompressionSpec processingSpec)
        {
            try
            {
                ValidateProcessingSpec(processingSpec);
                CMetadataHandler metaHandler = new CMetadataHandler(EWorkMode.Browse, processingSpec.ArchiveFileName);
                CArchiveMetadata meta = metaHandler.BrowseArchive();

                ValidateAppendBlockSize(meta.BlockSize, processingSpec.BlockSize);
                processingSpec = RemoveExistingFromProcessing(meta, processingSpec);
                return ProcessAppend(processingSpec, meta);
            }
            catch (Exception ex)
            {
                Notifier.Exception(ex);
                return null;
            }
        }

        public void Cancel()
        {
            _isCanceled = true;

            if (_currentTask != null)
                _currentTask.Cancel();
        }

        private static void ValidateProcessingSpec(CProcessingSpec spec)
        {
            if (spec == null)
                throw new ArgumentNullException("spec");

            switch (spec.Mode)
            {
                case EWorkMode.Compress:
                    if (File.Exists(spec.ArchiveFileName))
                        throw new Exception(string.Format("File \"{0}\" already exists.", spec.ArchiveFileName));
                    break;
                case EWorkMode.Append:
                    if (!File.Exists(spec.ArchiveFileName))
                        throw new Exception(string.Format("File \"{0}\" not found.", spec.ArchiveFileName));
                    break;
                case EWorkMode.Decompress:
                case EWorkMode.Browse:
                    if (!File.Exists(spec.ArchiveFileName))
                        throw new FileNotFoundException(string.Format("File \"{0}\" not found.", spec.ArchiveFileName));
                    break;
                default:
                    throw new ArgumentOutOfRangeException("spec");
            }
        }
        
        private static void ValidateAppendBlockSize(int metaBlockSize, int appendBlockSize)
        {
            if (appendBlockSize != metaBlockSize)
                throw new Exception(
                    string.Format("Blocksize in existing archive {0} is different from append specification {1}.",
                    metaBlockSize,
                    appendBlockSize));
        }

        private static CCompressionSpec RemoveExistingFromProcessing(CArchiveMetadata meta, CCompressionSpec processingSpec)
        {
            List<CObjectInArchive> objsNoExisting = new List<CObjectInArchive>();

            foreach (CObjectInArchive obj in processingSpec.ArchiveObjects)
            {
                if (meta.ObjectsInArchive.All(_ => _.InsidePath != obj.InsidePath))
                {
                    objsNoExisting.Add(obj);
                }
                else
                {
                    Notifier.Message("File \"{0}\" already exists in archive. Will be skipped.", obj.InsidePath);
                }
            }

            return new CCompressionSpec(processingSpec.Mode, processingSpec.ArchiveFileName, objsNoExisting.ToArray());
        }

        private CProcessingResultSpec ProcessCompression(CCompressionSpec processingSpec)
        {
            int filesCount = processingSpec.ArchiveObjects.Length;
            Notifier.Message("Got {0} {1} to compress.", filesCount, filesCount > 1 ? "files" : "file");
            
            CMetadataHandler metaHandler = new CMetadataHandler(EWorkMode.Compress, processingSpec.ArchiveFileName);
            metaHandler.SaveArchiveHeader();

            CProcessingResultSpec resultSpec = new CProcessingResultSpec(EWorkMode.Compress);
            foreach (CObjectInArchive objInArchive in processingSpec.ArchiveObjects)
            {
                Notifier.Message("\nCompressing {0} ({1} bytes)", objInArchive.OriginalPath, objInArchive.OriginalSize.ToString("##,##0"));

                long objHeaderStartOffset;
                metaHandler.SaveObjectHeader(objInArchive, out objHeaderStartOffset);
                objInArchive.SetDataOffset(objHeaderStartOffset);

                STaskSpec taskSpec = new STaskSpec(processingSpec.Mode, processingSpec.ArchiveFileName, objInArchive, processingSpec.ThreadCount, processingSpec.BlockSize);
                CTaskResultSpec taskResult = RunTask(taskSpec);
                resultSpec.AddTaskResult(taskResult);

                if (taskResult.TaskResultType != EResultType.Success)
                    Notifier.TaskResult(taskResult);

                if (!_isCanceled)
                    continue;

                resultSpec.UpdateOverallResultOptionally(EResultType.Failed);
                break;
            }

            if (!_isCanceled)
                metaHandler.SaveArchiveTail(resultSpec);

            return resultSpec;
        }

        private CProcessingResultSpec ProcessDecompression(CDecompressionSpec processingSpec, CArchiveMetadata meta)
        {
            CObjectInArchive[] objects2Decompress = CObjectParser.GetExistingObjects(processingSpec, meta);
            Notifier.Message("Got {0} {1} to decompress.", objects2Decompress.Length, objects2Decompress.Length > 1 ? "files" : "file");
            
            CProcessingResultSpec resultSpec = new CProcessingResultSpec(EWorkMode.Decompress);
            foreach (CObjectInArchive objInArchive in objects2Decompress)
            {
                Notifier.Message("\nDecompressing {0} ({1} bytes) to {2}", objInArchive.InsidePath, objInArchive.OriginalSize.ToString("##,##0"), objInArchive.OriginalPath);
                STaskSpec taskSpec = new STaskSpec(processingSpec.Mode, processingSpec.ArchiveFileName, objInArchive, processingSpec.ThreadCount, meta.BlockSize);
                CTaskResultSpec taskResult = RunTask(taskSpec);
                resultSpec.AddTaskResult(taskResult);

                if (taskResult.TaskResultType != EResultType.Success)
                    Notifier.TaskResult(taskResult);

                if (!_isCanceled)
                    continue;

                resultSpec.UpdateOverallResultOptionally(EResultType.Failed);
                break;
            }

            return resultSpec;
        }

        private CProcessingResultSpec ProcessAppend(CCompressionSpec processingSpec, CArchiveMetadata oldMeta)
        {
            Notifier.Message("Got {0} {1} to compress.", processingSpec.ArchiveObjects.Length, processingSpec.ArchiveObjects.Length > 1 ? "files" : "file");
            CMetadataHandler metaHandler = new CMetadataHandler(EWorkMode.Append, processingSpec.ArchiveFileName);
            
            CProcessingResultSpec resultSpec = new CProcessingResultSpec(EWorkMode.Append);
            foreach (CObjectInArchive objInArchive in processingSpec.ArchiveObjects)
            {
                Notifier.Message("\nCompressing {0} ({1} bytes)", objInArchive.OriginalPath, objInArchive.OriginalSize.ToString("##,##0"));

                long objHeaderStartOffset;
                metaHandler.SaveObjectHeader(objInArchive, out objHeaderStartOffset);
                objInArchive.SetDataOffset(objHeaderStartOffset);

                STaskSpec taskSpec = new STaskSpec(processingSpec.Mode, processingSpec.ArchiveFileName, objInArchive, processingSpec.ThreadCount, processingSpec.BlockSize);
                CTaskResultSpec taskResult = RunTask(taskSpec);
                resultSpec.AddTaskResult(taskResult);

                if (taskResult.TaskResultType != EResultType.Success)
                    Notifier.TaskResult(taskResult);

                if (!_isCanceled)
                    continue;

                resultSpec.UpdateOverallResultOptionally(EResultType.Failed);
                break;
            }

            if (!_isCanceled)
                metaHandler.SaveArchiveTail(oldMeta, resultSpec);

            return resultSpec;
        }

        private CTaskResultSpec RunTask(STaskSpec taskSpec)
        {
            _currentTask = new CTask(taskSpec);

            return _currentTask.Execute();
        }
    }
}
