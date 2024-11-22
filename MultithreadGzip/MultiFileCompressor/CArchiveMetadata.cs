namespace MultiThreadGzip.MultiFileCompressor
{
    public class CArchiveMetadata
    {
        public CArchiveMetadata(int blockSize, CObjectInArchive[] objectsInArchive)
        {
            BlockSize = blockSize;
            ObjectsInArchive = objectsInArchive;
        }

        public int BlockSize { get; private set; }
        public CObjectInArchive[] ObjectsInArchive { get; private set; }
    }
}
