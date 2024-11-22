namespace MultiThreadGzip.MultiFileCompressor
{
    public class CObjectInArchive
    {
        private readonly string _insidePath;
        private string _origPath;
        private long _origSize;
        private long _compressedSize;
        private long _headerOffset;
        private long _dataOffset;
        
        public CObjectInArchive(string insidePath)
        {
            _insidePath = insidePath;
        }

        public string InsidePath
        {
            get { return _insidePath; }
        }

        public string OriginalPath
        {
            get { return _origPath; }
        }

        public long OriginalSize
        {
            get { return _origSize; }
        }

        public long CompressedSize
        {
            get { return _compressedSize; }
        }

        public long HeaderOffset
        {
            get { return _headerOffset; }
        }

        public long DataOffset
        {
            get { return _dataOffset; }
        }

        public void SetOrigPath(string path)
        {
            _origPath = path;
        }

        public void SetOrigSize(long size)
        {
            _origSize = size;
        }

        public void SetCompressedSize(long size)
        {
            _compressedSize = size;
        }
        
        public void SetHeaderOffset(long offset)
        {
            _headerOffset = offset;
        }

        public void SetDataOffset(long offset)
        {
            _dataOffset = offset;
        }
    }
}
