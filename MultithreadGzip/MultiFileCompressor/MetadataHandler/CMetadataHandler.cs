using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MultiThreadGzip.Pipeline;

namespace MultiThreadGzip.MultiFileCompressor.MetadataHandler
{
    class CMetadataHandler
    {
        private const string META_CRC_MISMATCH_ERROR = "Metadata checksum CRC32 mismatch. Archive format unsupported either file damaged.";

        private static readonly Encoding PATH_ENCODING = Encoding.Unicode;
        private static readonly byte[] META_SEPARATOR = { 0, 0, 255, 255 }; // x00FF

        private readonly string _fileName;
        private readonly FileAccess _fileAccess;
        private readonly CCrc32Calc _crcCalc;

        public CMetadataHandler(EWorkMode mode, string archiveFileName)
        {
            _fileName = archiveFileName;
            _crcCalc = new CCrc32Calc();

            switch (mode)
            {
                case (EWorkMode.Browse):
                case (EWorkMode.Decompress):
                    _fileAccess = FileAccess.Read;
                    break;
                case (EWorkMode.Compress):
                case (EWorkMode.Append):
                    _fileAccess = FileAccess.Write;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode", mode, null);
            }
        }

        #region Public methods

        public CArchiveMetadata BrowseArchive()
        {
            try
            {
                using (FileStream fileStream = new FileStream(_fileName, FileMode.Open, _fileAccess))
                {
                    ValidateHeaderCRC(fileStream);
                    fileStream.Seek(0, SeekOrigin.Begin);

                    int blockSize;
                    ReadFileHeader(fileStream, out blockSize);

                    long metaTailOffset = GetTailStartOffset(fileStream);
                    ValidateTailCRC(fileStream, metaTailOffset);

                    Dictionary<long, long> objOffsetSizeDict;
                    ReadFileTail(fileStream, metaTailOffset, out objOffsetSizeDict);

                    if (objOffsetSizeDict == null)
                        throw new Exception("Failed to get object offsets and sizes.");

                    CObjectInArchive[] objsInArchive = GetObjectsInArchive(fileStream, objOffsetSizeDict);

                    return new CArchiveMetadata(blockSize, objsInArchive);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read metadata.", ex);
            }
        }

        public void SaveArchiveHeader()
        {
            try
            {
                using (FileStream fileStream = new FileStream(_fileName, FileMode.CreateNew, _fileAccess))
                {
                    _crcCalc.Accumulate(CWriteFileHelper.WriteString(fileStream, CProcessingSpec.ARCHIVE_VERSION, Encoding.ASCII));
                    _crcCalc.Accumulate(CWriteFileHelper.WriteIntValue(fileStream, CProcessingSpec.DEFAULT_BLOCK_SIZE));
                    WriteMetaSeparator(fileStream);

                    int crc = (int)_crcCalc.GetAccumulatedDataCrc();
                    CWriteFileHelper.WriteIntValue(fileStream, crc);
                    WriteMetaSeparator(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save archive metadata header.", ex);
            }
        }

        public void SaveObjectHeader(CObjectInArchive objectInside, out long objHeaderStartOffset)
        {
            try
            {
                using (FileStream fileStream = new FileStream(_fileName, FileMode.Open, _fileAccess))
                {
                    fileStream.Seek(0, SeekOrigin.End); // Subsequent objects are added to the end of file.
                    WriteMetaSeparator(fileStream);
                    objHeaderStartOffset = fileStream.Position;

                    int internalPathLength = PATH_ENCODING.GetByteCount(objectInside.InsidePath);
                    _crcCalc.Accumulate(CWriteFileHelper.WriteIntValue(fileStream, internalPathLength));
                    _crcCalc.Accumulate(CWriteFileHelper.WriteString(fileStream, objectInside.InsidePath, PATH_ENCODING));
                    _crcCalc.Accumulate(CWriteFileHelper.WriteLongValue(fileStream, objectInside.OriginalSize));
                    WriteMetaSeparator(fileStream);

                    CWriteFileHelper.WriteIntValue(fileStream, (int)_crcCalc.GetAccumulatedDataCrc());
                    WriteMetaSeparator(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Failed to save object \"{0}\" header.", objectInside.InsidePath), ex);
            }
        }

        public void SaveArchiveTail(CProcessingResultSpec result)
        {
            try
            {
                using (FileStream fileStream = new FileStream(_fileName, FileMode.Open, _fileAccess))
                {
                    long tailStartOffset = fileStream.Length;
                    fileStream.Seek(0, SeekOrigin.End);
                    WriteMetaSeparator(fileStream);
                    
                    _crcCalc.Accumulate(CWriteFileHelper.WriteIntValue(fileStream, result.TaskResults.Count));
                    _crcCalc.Accumulate(WriteCompressedObjParams(fileStream, result));
                    _crcCalc.Accumulate(CWriteFileHelper.WriteLongValue(fileStream, tailStartOffset));
                    WriteMetaSeparator(fileStream);

                    CWriteFileHelper.WriteIntValue(fileStream, (int)_crcCalc.GetAccumulatedDataCrc());
                    WriteMetaSeparator(fileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save archive metadata tail.", ex);
            }
        }

        public void SaveArchiveTail(CArchiveMetadata meta, CProcessingResultSpec resultSpec)
        {
            SaveArchiveTail(MergeAppendResult(meta, resultSpec));
        }

        #endregion


        #region Private methods

        private void ReadFileHeader(FileStream fileStream, out int blockSize)
        {
            try
            {
                ReadAndCheckVersion(fileStream);
                blockSize = CReadFileHelper.ReadIntValue(fileStream);
                if (blockSize <= 0)
                    throw new Exception("Block size value is less or equal to zero.");
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read archive header.", ex);
            }
        }

        private void ReadSeparator(FileStream fileStream)
        {
            byte[] separatorReadBytes = CReadFileHelper.ReadBytes(fileStream, META_SEPARATOR.Length);

            if (separatorReadBytes.Where((t, i) => t != META_SEPARATOR[i]).Any())
                Notifier.Message("Unexpected format of metadata separator. Possibly file is damaged.");
        }

        private void ReadAndCheckVersion(FileStream fileStream)
        {
            string readVersion = CReadFileHelper.ReadString(CProcessingSpec.ARCHIVE_VERSION.Length, fileStream, Encoding.ASCII);

            if (!string.Equals(readVersion, CProcessingSpec.ARCHIVE_VERSION))
                throw new Exception(string.Format("Archive version \"{0}\" is not supported.", readVersion));
        }

        private void ReadFileTail(FileStream fileStream, long metaTailStartOffset, out Dictionary<long, long> offsetSizeDict)
        {
            try
            {
                fileStream.Seek(metaTailStartOffset, SeekOrigin.Begin);
                ReadSeparator(fileStream);

                int objsCount = CReadFileHelper.ReadIntValue(fileStream);
                if (objsCount < 0)
                    throw new Exception("Objects in archive count value is less than zero.");

                offsetSizeDict = new Dictionary<long, long>();
                for (int i = 0; i < objsCount; i++)
                {
                    long startOffset = CReadFileHelper.ReadLongValue(fileStream);
                    if (startOffset < 1)
                        throw new Exception("Object start file offset cannot be less or equal to zero.");

                    long compressedSize = CReadFileHelper.ReadLongValue(fileStream);
                    if (compressedSize < 0)
                        throw new Exception("Compressed object size cannot be less than zero.");

                    offsetSizeDict.Add(startOffset, compressedSize);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read archive metadata tail.", ex);
            }
        }
        
        private long GetTailStartOffset(FileStream fileStream)
        {
            const int seekFromEnd2TailPositionSaved = 3 * sizeof(int) + sizeof(long); // Standard position where tail offset long value is saved.

            if (fileStream.Length < seekFromEnd2TailPositionSaved + 1)
                throw new Exception(string.Format("File is too short({0} bytes). Cannot read metadata tail offset.", fileStream.Length));

            fileStream.Seek((-seekFromEnd2TailPositionSaved), SeekOrigin.End);
            long metaTailStartOffset = CReadFileHelper.ReadLongValue(fileStream);

            if (metaTailStartOffset < 1)
                throw new Exception("Tail offset cannot be less or equal to zero.");

            return metaTailStartOffset;
        }

        private void WriteMetaSeparator(FileStream fileStream)
        {
            if (fileStream == null)
                throw new ArgumentNullException("fileStream");

            CWriteFileHelper.WriteBytes(fileStream, META_SEPARATOR);
        }

        private IEnumerable<byte> WriteCompressedObjParams(FileStream fileStream, CProcessingResultSpec resultSpec)
        {
            long[] startOffsets = resultSpec.TaskResults.Select(x => x.ArchiveStartOffset).ToArray();
            long[] compressedSizes = resultSpec.TaskResults.Select(x => x.CompressedSize).ToArray();
            
            List<byte> bytesWritten = new List<byte>();
            for (int i = 0; i < resultSpec.TaskResults.Count; i++)
            {
                bytesWritten.AddRange(CWriteFileHelper.WriteLongValue(fileStream, startOffsets[i]));
                bytesWritten.AddRange(CWriteFileHelper.WriteLongValue(fileStream, compressedSizes[i]));
            }

            return bytesWritten;
        }

        private CProcessingResultSpec MergeAppendResult(CArchiveMetadata meta, CProcessingResultSpec resultSpec)
        {
            CProcessingResultSpec mergedResultSpec = new CProcessingResultSpec(EWorkMode.Append);
            foreach (CObjectInArchive obj in meta.ObjectsInArchive)
            {
                CTaskResultSpec taskRes = new CTaskResultSpec(EWorkMode.Compress, obj.OriginalPath, obj.OriginalSize);
                taskRes.ArchiveStartOffset = obj.HeaderOffset;
                taskRes.CompressedSize = obj.CompressedSize;
                mergedResultSpec.AddTaskResult(taskRes);
            }

            foreach (CTaskResultSpec taskResult in resultSpec.TaskResults)
            {
                mergedResultSpec.AddTaskResult(taskResult);
            }

            return mergedResultSpec;
        }
        
        private CObjectInArchive[] GetObjectsInArchive(FileStream fileStream, Dictionary<long, long> offsetSizeDict)
        {
            const int sizeOfSeparatorsAndCrc = 3 * sizeof(int);

            try
            {
                List<CObjectInArchive> objs = new List<CObjectInArchive>();

                foreach (long startOffset in offsetSizeDict.Keys)
                {
                    fileStream.Seek(startOffset, SeekOrigin.Begin);
                    ValidateObjectHeaderCRC(fileStream);

                    fileStream.Seek(startOffset, SeekOrigin.Begin);

                    int strLength = CReadFileHelper.ReadIntValue(fileStream);
                    string objInsidePath = CReadFileHelper.ReadString(strLength, fileStream, PATH_ENCODING);
                    long objSize = CReadFileHelper.ReadLongValue(fileStream);
                    long objDataStartOffset = fileStream.Position + sizeOfSeparatorsAndCrc;

                    CObjectInArchive obj = new CObjectInArchive(objInsidePath);
                    obj.SetOrigSize(objSize);
                    obj.SetHeaderOffset(startOffset);
                    obj.SetDataOffset(objDataStartOffset);
                    obj.SetCompressedSize(offsetSizeDict[startOffset]);

                    objs.Add(obj);
                }

                return objs.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read archived objects parameters.", ex);
            }
        }

        private void ValidateHeaderCRC(FileStream fileStream)
        {
            fileStream.Seek(0, SeekOrigin.Begin);

            int headerLength = 0;
            headerLength += CProcessingSpec.ARCHIVE_VERSION.Length;
            headerLength += sizeof(int); // Length of Int32 allocated for blockSize

            int currentHeaderCrc = GetCRCNextBytes(fileStream, headerLength);

            ReadSeparator(fileStream);
            int crcFromFile = CReadFileHelper.ReadIntValue(fileStream);

            if (crcFromFile != currentHeaderCrc)
                throw new Exception(META_CRC_MISMATCH_ERROR);
        }

        private void ValidateObjectHeaderCRC(FileStream fileStream)
        {
            long startOffset = fileStream.Position;
            int objNameLength = CReadFileHelper.ReadIntValue(fileStream);
            fileStream.Seek(startOffset, SeekOrigin.Begin);

            int objHeaderLength = objNameLength + sizeof(int) + sizeof(long); // name + name length + orig size
            int objHeaderCrcCurrent = GetCRCNextBytes(fileStream, objHeaderLength);

            ReadSeparator(fileStream);
            int crcInFile = CReadFileHelper.ReadIntValue(fileStream);
            ReadSeparator(fileStream);

            if (objHeaderCrcCurrent != crcInFile)
                throw new Exception(META_CRC_MISMATCH_ERROR);
        }

        private void ValidateTailCRC(FileStream fileStream, long metaTailStartOffset)
        {
            const int endBytesCount = 3 * sizeof(int);

            if (fileStream == null)
                throw new ArgumentNullException("fileStream");

            if (fileStream.Length < metaTailStartOffset)
                throw new Exception("Metadata tail start offset is outside of the file size.");

            fileStream.Seek(metaTailStartOffset, SeekOrigin.Begin);

            if (fileStream.Length < fileStream.Position + endBytesCount)
                throw new Exception("Metadata tail legth is less than expected.");

            ReadSeparator(fileStream);

            int tailLenth = (int)(fileStream.Length - fileStream.Position - endBytesCount);
            int crcCalculated = GetCRCNextBytes(fileStream, tailLenth);

            ReadSeparator(fileStream);

            int crcInFile = CReadFileHelper.ReadIntValue(fileStream);
            if (crcCalculated != crcInFile)
                throw new Exception(META_CRC_MISMATCH_ERROR);
        }
        
        private int GetCRCNextBytes(FileStream fileStream, int bytesCount)
        {
            byte[] data = CReadFileHelper.ReadBytes(fileStream, bytesCount);

            if (_crcCalc == null)
                throw new NullReferenceException("CRC32 calculator is not created.");

            int crc = (int)_crcCalc.Compute(data);

            return crc;
        }

        #endregion
    }
}
