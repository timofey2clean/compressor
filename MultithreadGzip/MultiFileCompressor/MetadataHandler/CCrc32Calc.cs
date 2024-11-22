using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MultiThreadGzip.MultiFileCompressor.MetadataHandler
{
    sealed class CCrc32Calc : HashAlgorithm
    {
        private const uint DEFAULT_POLYNOMIAL = 0xedb88320u;
        private const uint DEFAULT_SEED = 0xffffffffu;

        private uint[] _defaultTable;

        private readonly uint _seed;
        private readonly uint[] _table;
        private uint _hash;
        private readonly List<byte> _dataAccumulator;

        public CCrc32Calc()
            : this(DEFAULT_POLYNOMIAL, DEFAULT_SEED)
        {
            _dataAccumulator = new List<byte>();
        }

        public CCrc32Calc(uint polynomial, uint seed)
        {
            _table = InitializeTable(polynomial);
            _seed = _hash = seed;
        }

        public override void Initialize()
        {
            _hash = _seed;
        }

        protected override void HashCore(byte[] buffer, int start, int length)
        {
            _hash = CalculateHash(_table, _hash, buffer, start, length);
        }

        protected override byte[] HashFinal()
        {
            byte[] hashBuffer = UInt32ToBigEndianBytes(~_hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize { get { return 32; } }

        public void Accumulate(IEnumerable<byte> data)
        {
            _dataAccumulator.AddRange(data);
        }

        public uint GetAccumulatedDataCrc(bool clear = true)
        {
            uint crc = Compute(_dataAccumulator.ToArray());
            
            if(clear)
                _dataAccumulator.Clear();

            return crc;
        }

        public uint Compute(byte[] buffer)
        {
            return Compute(DEFAULT_SEED, buffer);
        }

        public uint Compute(uint seed, byte[] buffer)
        {
            return Compute(DEFAULT_POLYNOMIAL, seed, buffer);
        }

        public uint Compute(uint polynomial, uint seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
        }

        private uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DEFAULT_POLYNOMIAL && _defaultTable != null)
                return _defaultTable;

            uint[] createTable = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                uint entry = (uint)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry = entry >> 1;
                }
                createTable[i] = entry;
            }

            if (polynomial == DEFAULT_POLYNOMIAL)
                _defaultTable = createTable;

            return createTable;
        }

        private uint CalculateHash(uint[] table, uint seed, IList<byte> buffer, int start, int size)
        {
            uint crc = seed;
            for (int i = start; i < size - start; i++)
            {
                crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
            }

            return crc;
        }

        private byte[] UInt32ToBigEndianBytes(uint uint32)
        {
            byte[] result = BitConverter.GetBytes(uint32);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }
    }
}
