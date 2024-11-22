using System;
using System.IO;
using System.Text;

namespace MultiThreadGzip.MultiFileCompressor.MetadataHandler
{
    static class CReadFileHelper
    {
        public static byte[] ReadBytes(FileStream fileStream, int count)
        {
            byte[] buffer = new byte[count];
            int readBytesCount = fileStream.Read(buffer, 0, buffer.Length);

            if(readBytesCount < count)
                throw new Exception(string.Format("Read {0} bytes is less than requested {1}.", readBytesCount, count));

            return buffer;
        }

        public static string ReadString(int strLength, FileStream fileStream, Encoding encoding)
        {
            byte[] strBytes = new byte[strLength];
            int readBytesCount = fileStream.Read(strBytes, 0, strBytes.Length);

            if (readBytesCount < strLength)
                throw new Exception(string.Format("Read {0} bytes is less than requested {1}.", readBytesCount, strLength));

            return encoding.GetString(strBytes);
        }

        public static long ReadLongValue(FileStream fileStream)
        {
            const int longSize = sizeof(long);

            byte[] longValueBytes = new byte[longSize];
            int readBytesCount = fileStream.Read(longValueBytes, 0, longValueBytes.Length);

            if (readBytesCount < longSize)
                throw new Exception(string.Format("Read {0} bytes is less than requested {1}.", readBytesCount, longSize));

            return BitConverter.ToInt64(longValueBytes, 0);
        }

        public static int ReadIntValue(FileStream fileStream)
        {
            const int intSize = sizeof(int);

            byte[] intValueBytes = new byte[sizeof(int)];
            int readBytesCount = fileStream.Read(intValueBytes, 0, intValueBytes.Length);

            if (readBytesCount < intSize)
                throw new Exception(string.Format("Read {0} bytes is less than requested {1}.", readBytesCount, intSize));

            return BitConverter.ToInt32(intValueBytes, 0);
        }
    }
}
