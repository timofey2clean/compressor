using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MultiThreadGzip.MultiFileCompressor
{
    class CObjectParser
    {
        public static CObjectInArchive[] GetExistingObjects(CDecompressionSpec processingSpec, CArchiveMetadata meta)
        {
            string[] objPaths2Decompress = GetExistingObjectPaths(processingSpec.DecompressionMode, processingSpec.InsidePaths2Decompress, meta.ObjectsInArchive.Select(x => x.InsidePath));

            List<CObjectInArchive> objects2Decompress = new List<CObjectInArchive>();
            foreach (string objInsidePath in objPaths2Decompress)
            {
                CObjectInArchive objInArchive = meta.ObjectsInArchive.FirstOrDefault(x => x.InsidePath == objInsidePath);
                if (objInArchive == null)
                    continue;

                string origPath = GetDecompressionFileName(
                    processingSpec.DecompressionMode,
                    processingSpec.TargetPath,
                    objInArchive.InsidePath);
                objInArchive.SetOrigPath(origPath);
                objects2Decompress.Add(objInArchive);
            }

            return objects2Decompress.ToArray();
        }

        private static string GetDecompressionFileName(CDecompressionSpec.EDecompressionMode decompressionMode, string targetPath, string insidePath)
        {
            // Normalize internal path.
            if (insidePath.Length > 0 && insidePath.StartsWith("\\"))
                insidePath = insidePath.Substring(1, insidePath.Length - 1);

            switch (decompressionMode)
            {
                case CDecompressionSpec.EDecompressionMode.AllFiles:
                case CDecompressionSpec.EDecompressionMode.MultipleFiles:
                    return Path.Combine(targetPath, insidePath);
                case CDecompressionSpec.EDecompressionMode.SingleFile:
                    return Directory.Exists(targetPath) ? Path.Combine(targetPath, insidePath) : targetPath;
                default:
                    throw new ArgumentOutOfRangeException("decompressionMode", decompressionMode, null);
            }
        }

        private static string[] GetExistingObjectPaths(CDecompressionSpec.EDecompressionMode decompressionMode, IEnumerable<string> selectedPaths2Decompress, IEnumerable<string> existingPathsInArchive)
        {
            try
            {
                if (selectedPaths2Decompress == null)
                    throw new ArgumentNullException("selectedPaths2Decompress");
                if (existingPathsInArchive == null)
                    throw new ArgumentNullException("existingPathsInArchive");

                string[] selectedPaths = selectedPaths2Decompress as string[] ?? selectedPaths2Decompress.ToArray();
                string[] existingPaths = existingPathsInArchive as string[] ?? existingPathsInArchive.ToArray();

                if (!existingPaths.Any())
                    throw new NotSupportedException("There are no files in archive.");

                List<string> obj2DecompressPaths = new List<string>();
                switch (decompressionMode)
                {
                    case (CDecompressionSpec.EDecompressionMode.AllFiles):
                        obj2DecompressPaths.AddRange(existingPaths);
                        break;
                    case (CDecompressionSpec.EDecompressionMode.MultipleFiles):
                        obj2DecompressPaths.AddRange(selectedPaths.Where(existingPaths.Contains));
                        break;
                    case (CDecompressionSpec.EDecompressionMode.SingleFile):
                        if (selectedPaths.Length == 0)
                        {
                            if (existingPaths.Count() == 1)
                                obj2DecompressPaths.Add(existingPaths.ToArray()[0]);
                            else
                                throw new ArgumentException("Archive contains multiple files. Specify internal file path.");
                        }
                        else
                        {
                            obj2DecompressPaths.AddRange(selectedPaths.Where(existingPaths.Contains));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("decompressionMode", decompressionMode, null);
                }

                return obj2DecompressPaths.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get objects list for decompression.", ex);
            }
        }
    }
}
