using System;
using System.IO;
using System.Text;

namespace MultiThreadGzip.MultiFileCompressor.MetadataHandler
{
    static class CWriteFileHelper
    {
        public static byte[] WriteString(FileStream fileStream, string text, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(text);
            fileStream.Write(bytes, 0, bytes.Length);

            return bytes;
        }

        public static byte[] WriteIntValue(FileStream fileStream, int intValue)
        {
            byte[] bytes = BitConverter.GetBytes(intValue);
            fileStream.Write(bytes, 0, bytes.Length);

            return bytes;
        }

        public static byte[] WriteLongValue(FileStream fileStream, long longValue)
        {
            byte[] bytes = BitConverter.GetBytes(longValue);
            fileStream.Write(bytes, 0, bytes.Length);

            return bytes;
        }

        public static void WriteBytes(FileStream fileStream, byte[] bytes)
        {
            fileStream.Write(bytes, 0, bytes.Length);
        }
    }
}
