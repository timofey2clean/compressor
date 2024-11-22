using System.IO;
using System.IO.Compression;

namespace MultiThreadGzip.Pipeline
{
    interface ICompressionProcessing
    {
        CDataBlock ProcessBlock(CDataBlock block);
    }

    class CCompress : ICompressionProcessing
    {
        public CDataBlock ProcessBlock(CDataBlock origBlock)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                using (GZipStream compressStream = new GZipStream(memStream, CompressionMode.Compress))
                {
                    compressStream.Write(origBlock.Buffer, 0, origBlock.Buffer.Length);
                }

                return new CDataBlock(origBlock.Number, memStream.ToArray().Length, memStream.ToArray());
            }
        }
    }

    class CDecompress : ICompressionProcessing
    {
        public CDataBlock ProcessBlock(CDataBlock compressedBlock)
        {
            int blockSize = compressedBlock.Size;
            long compressedBlockNumber = compressedBlock.Number;
            
            byte[] compressedBlockBuffer = compressedBlock.Buffer;
            byte[] decompressedBuffer = new byte[blockSize];
            
            using (MemoryStream memStream = new MemoryStream())
            {
                memStream.Write(compressedBlockBuffer, 0, compressedBlockBuffer.Length);
                memStream.Seek(0, SeekOrigin.Begin);

                using (GZipStream decompressStream = new GZipStream(memStream, CompressionMode.Decompress))
                {
                    decompressStream.Read(decompressedBuffer, 0, blockSize);
                }
            }
            
            return new CDataBlock(compressedBlockNumber, decompressedBuffer.Length, decompressedBuffer);
        }
    }
}
