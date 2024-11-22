namespace MultiThreadGzip.Pipeline
{
    class CDataBlock
    {
        public CDataBlock(long number, int size, byte[] buffer)
        {
            Number = number;
            Size = size;
            Buffer = buffer;
        }

        public long Number { get; private set; }

        public int Size { get; private set; }
        
        public byte[] Buffer { get; private set; }
    }
}
