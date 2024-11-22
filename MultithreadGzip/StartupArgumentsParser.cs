using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip
{
    static class StartupArgumentsParser
    {
        private const string INVALID_ARGS_ERROR = "Invalid arguments.";

        public static bool TryParseArgs(string[] args, out CProcessingSpec processingSpec)
        {
            processingSpec = null;

            try
            {
                processingSpec = ParseStartupArguments(args);
                return true;
            }
            catch (ArgumentException ex)
            {
                OutputHelper.ShowExceptionMessagesWithInner(ex);
                return false;
            }
        }
        
        private static CProcessingSpec ParseStartupArguments(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            if (args.Length < 2)
                throw new ArgumentException(INVALID_ARGS_ERROR);

            EWorkMode mode = ParseMode(args);
            switch (mode)
            {
                case EWorkMode.Browse:
                    return ParseBrowseArgs(args);
                case EWorkMode.Decompress:
                    return ParseDecompressionArgs(args);
                case EWorkMode.Compress:
                    return ParseCompressionArgs(EWorkMode.Compress, args);
                case EWorkMode.Append:
                    return ParseCompressionArgs(EWorkMode.Append, args);
                default:
                    throw new ArgumentException(INVALID_ARGS_ERROR);
            }
        }

        private static EWorkMode ParseMode(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            if (args.Length < 1)
                throw new ArgumentException(INVALID_ARGS_ERROR);

            string modeArgString = args[0].ToLower();
            switch (modeArgString)
            {
                case "append":
                case "-append":
                case "/append":
                case "add":
                case "-add":
                case "/add":
                case "-a":
                case "/a":
                case "a":
                    return EWorkMode.Append;
                case "compress":
                case "-compress":
                case "/compress":
                case "/c":
                case "-c":
                case "c":
                    return EWorkMode.Compress;
                case "decompress":
                case "-decompress":
                case "/decompress":
                case "/d":
                case "-d":
                case "d":
                    return EWorkMode.Decompress;
                case "browse":
                case "-browse":
                case "/browse":
                case "/b":
                case "-b":
                case "b":
                case "open":
                case "-open":
                case "/open":
                case "-o":
                case "/o":
                case "o":
                    return EWorkMode.Browse;
                default:
                    throw new ArgumentException(INVALID_ARGS_ERROR);
            }
        }

        private static CBrowseSpec ParseBrowseArgs(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            if (args.Length != 2)
                throw new ArgumentException(INVALID_ARGS_ERROR);

            string archiveFileName = args[1];

            return new CBrowseSpec(archiveFileName);
        }

        private static CCompressionSpec ParseCompressionArgs(EWorkMode mode, string[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            if (args.Length < 3)
                throw new ArgumentException(INVALID_ARGS_ERROR);

            string archiveFileName = args[args.Length - 1]; // Last argument should be the archive file name.

            string[] sourceFiles = new string[args.Length - 2];

            for (int i = 1; i < args.Length - 1; i++)
            {
                sourceFiles[i - 1] = args[i];
            }

            IEnumerable<CObjectInArchive> objsInArchive = ParseArchiveObjs(sourceFiles);

            return new CCompressionSpec(mode, archiveFileName, objsInArchive.ToArray());
        }

        private static CDecompressionSpec ParseDecompressionArgs(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            if (args.Length < 3)
                throw new ArgumentException(INVALID_ARGS_ERROR);

            string archiveFileName = args[1]; // First argument should be the archive file name.
            string targetPath = args[args.Length - 1]; // Last arg should be target path.

            List<string> insidePaths2Decompress = new List<string>();
            for (int i = 2; i < args.Length - 1; i++)
            {
                insidePaths2Decompress.Add(args[i]);
            }

            CDecompressionSpec.EDecompressionMode decompressionMode;
            if (Directory.Exists(targetPath) && !insidePaths2Decompress.Any())
            {
                decompressionMode = CDecompressionSpec.EDecompressionMode.AllFiles;
            }
            else if (Directory.Exists(targetPath) && insidePaths2Decompress.Count > 1)
            {
                decompressionMode = CDecompressionSpec.EDecompressionMode.MultipleFiles;
            }
            else if (insidePaths2Decompress.Count <= 1)
            {
                decompressionMode = CDecompressionSpec.EDecompressionMode.SingleFile;
            }
            else
            {
                throw new ArgumentException(INVALID_ARGS_ERROR);
            }

            return new CDecompressionSpec(archiveFileName, targetPath, decompressionMode, insidePaths2Decompress);
        }

        private static CObjectInArchive[] ParseArchiveObjs(string[] sourcePaths)
        {
            if (sourcePaths == null)
                throw new ArgumentNullException("sourcePaths");

            Dictionary<string, CObjectInArchive> filesInArchiveDict = new Dictionary<string, CObjectInArchive>();
            foreach (string fileOrDirName in sourcePaths)
            {
                if (File.Exists(fileOrDirName))
                {
                    FileInfo fileInfo = new FileInfo(fileOrDirName);
                    string insidePath = Path.GetFileName(fileOrDirName);

                    if (!filesInArchiveDict.ContainsKey(fileOrDirName))
                    {
                        CObjectInArchive objInArchive = new CObjectInArchive(insidePath);
                        objInArchive.SetOrigPath(fileInfo.FullName);
                        objInArchive.SetOrigSize(fileInfo.Length);
                        filesInArchiveDict.Add(fileOrDirName, objInArchive);
                    }

                    continue;
                }

                if (Directory.Exists(fileOrDirName))
                {
                    CObjectInArchive[] archObjs = ParseDir(fileOrDirName);

                    foreach (CObjectInArchive objInArchive in archObjs.Where(_ => !filesInArchiveDict.ContainsKey(_.OriginalPath)))
                    {
                        filesInArchiveDict.Add(objInArchive.OriginalPath, objInArchive);
                    }

                    continue;
                }

                throw new ArgumentException(string.Format("Path \"{0}\" does not exist.", fileOrDirName));
            }

            return filesInArchiveDict.Values.ToArray();
        }

        private static CObjectInArchive[] ParseDir(string dir)
        {
            if (dir == null)
                throw new ArgumentNullException("dir");

            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException(string.Format("Directory \"{0}\" not found.", dir));

            string[] fileNames = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);

            List<CObjectInArchive> filesInArchive = new List<CObjectInArchive>();
            foreach (string fileName in fileNames)
            {
                FileInfo fileInfo = new FileInfo(fileName);
                string insidePath = GetRelativePath(dir, fileName);
                
                CObjectInArchive obj = new CObjectInArchive(insidePath);
                obj.SetOrigSize(fileInfo.Length);
                obj.SetOrigPath(fileName);

                filesInArchive.Add(obj);
            }

            return filesInArchive.ToArray();
        }

        private static string GetRelativePath(string dir, string fileName)
        {
            if (dir == null)
                throw new ArgumentNullException("dir");
            if (fileName == null)
                throw new ArgumentNullException("fileName");

            if (dir.Length > 0 && dir.EndsWith("\\"))
                dir = dir.Substring(0, dir.Length - 1);

            if (!fileName.StartsWith(dir))
                throw new Exception(string.Format("Directory \"{0}\" does not contain file \"{1}\".", dir, fileName));

            string dirShortName = Path.GetFileName(dir);
            if(string.IsNullOrEmpty(dirShortName))
                throw new Exception("Failed to get directory short name.");

            string insidePath = Path.Combine(dirShortName, fileName.Replace(dir, string.Empty));

            if (insidePath.Length > 0 && insidePath.StartsWith("\\"))
                insidePath = insidePath.Substring(1, insidePath.Length - 1);

            return insidePath;
        }
    }
}
