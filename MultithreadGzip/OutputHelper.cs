using System;
using System.IO;
using System.Linq;
using System.Text;
using MultiThreadGzip.MultiFileCompressor;
using MultiThreadGzip.Pipeline;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip
{
    static class OutputHelper
    {
        private const char PROGRESSBAR_COMPLETED_CHAR = '█';
        private const char PROGRESSBAR_REST_CHAR = ' ';
        
        #region Public methods
        
        public static void ShowNotification(INotificationSpec spec)
        {
            switch (spec.Type)
            {
                case ENotificationType.Messsage:
                    WriteLine(((CLogNotification)spec).Message);
                    break;
                case ENotificationType.DebugMessage:
                    WriteLine(((CLogDebugNotification)spec).Message);
                    break;
                case ENotificationType.TaskProgress:
                    ShowPercentCompleted(((CTaskProgress)spec).PrcntCompleted);
                    ShowWorkload(((CTaskProgress)spec).LoadSpec);
                    break;
                case ENotificationType.TaskResult:
                    ShowTaskResult(((CTaskNotification)spec).TaskResult);
                    break;
                case ENotificationType.ProcessingResult:
                    ShowProcessingRestul(((CProcessingNotification)spec).ProcessingResult);
                    break;
                case ENotificationType.Warning:
                    WriteLine(string.Format("\nWarning: {0}", ((CWarningNotification)spec).Warning));
                    break;
                default:
                    throw new ArgumentOutOfRangeException("spec");
            }
        }

        public static void ShowExceptionMessagesWithInner(Exception exc)
        {
            WriteLine(exc.Message);
            Exception innerException = exc.InnerException;

            while (innerException != null)
            {
                WriteLine(innerException.Message);
                innerException = innerException.InnerException;
            }
        }

        public static void ShowMultiFileArchive(CArchiveMetadata archiveMetadata)
        {
            int filesCount = archiveMetadata.ObjectsInArchive.Length;
            WriteLine("Got {0} {1} in archive:", archiveMetadata.ObjectsInArchive.Length, filesCount > 1 ? "files" : "file");

            if (archiveMetadata.ObjectsInArchive == null || !archiveMetadata.ObjectsInArchive.Any())
                WriteLine("No objects.");

            if (archiveMetadata.ObjectsInArchive == null)
                return;

            foreach (CObjectInArchive obj in archiveMetadata.ObjectsInArchive)
            {
                WriteLine("\n\"{0}\"\n[ orig. size {1} bytes  |  compressed size {2} bytes ]",
                    obj.InsidePath,
                    obj.OriginalSize.ToString("##,##0"),
                    obj.CompressedSize.ToString("##,##0"));
            }
        }

        public static void ShowProcessingRestul(CProcessingResultSpec resultSpec)
        {
            const string separator = "_________________________";

            WriteLine(Environment.NewLine);
            WriteLine(separator);
            WriteLine("Results");
            WriteLine(separator);

            switch (resultSpec.Mode)
            {
                case EWorkMode.Compress:
                case EWorkMode.Append:
                case EWorkMode.Decompress:
                    foreach (CTaskResultSpec taskResult in resultSpec.TaskResults)
                    {
                        ShowTaskResult(taskResult);
                        WriteLine(separator);
                    }
                    break;
                case EWorkMode.Browse:
                    break;
                default:
                    throw new NotSupportedException("Unsupported work mode.");
            }

            WriteLine(separator);
            ShowOverallResult(resultSpec);

            WriteLine(separator);
        }

        public static void ShowHelp()
        {
            StringBuilder helpMsgBuilder = new StringBuilder();
            helpMsgBuilder.AppendLine("\nStart with arguments:");
            helpMsgBuilder.AppendLine("Compress single file: -c [file] [archive file]");
            helpMsgBuilder.AppendLine("Compress multiple files: -c [file or directory 1] [file or directory 2] .. [file or directory N] [archive file]");
            helpMsgBuilder.AppendLine("Add files to archive: -a [file or directory 1] [file or directory 2] .. [file or directory N] [existing archive file]");
            helpMsgBuilder.AppendLine("Decompress single-file archive: -d [archive file] [destination file or directory]");
            helpMsgBuilder.AppendLine("Decompress multi-file archive: -d [archive file] [internal path 1] [internal path 2] ..  [internal path N] [destination directory]");
            helpMsgBuilder.AppendLine("Decompress all files: -d [archive file] [destination directory]");
            helpMsgBuilder.AppendLine("Browse archive: -b [archive file]");

            WriteLine(helpMsgBuilder.ToString());
        }

        #endregion


        #region Private methods

        private static void WriteLine(string text, params object[] args)
        {
            Console.WriteLine(text, args);
        }

        private static void Write(string text, params object[] args)
        {
            Console.Write(text, args);
        }

        private static void ShowOverallResult(CProcessingResultSpec resultSpec)
        {
            WriteLine("Overall result: {0}", resultSpec.OverallResultType.ToString());
            string totalDurationString = GetDurationHumanString(TimeSpan.FromSeconds(resultSpec.TaskResults.Sum(_ => _.Duration.TotalSeconds)));
            WriteLine("Duration: {0}", totalDurationString);

            if (resultSpec.OverallResultType != EResultType.Failed && resultSpec.OverallResultType != EResultType.None)
            {
                SWorkLoadSpec workloadAverage = resultSpec.GetAverageWorkload();
                WriteLine("Workload: CPU {0}% Read {1}% Write {2}%", workloadAverage.CpuLoad, workloadAverage.ReadLoad, workloadAverage.WriteLoad);
            }
        }

        private static void ShowPercentCompleted(int hundredthPrcnt)
        {
            long currentValue = hundredthPrcnt;

            if (currentValue < 0 || currentValue > 100 * 100)
                return;

            long symbolsToShowCompletedCount = currentValue / 5 / 100;
            string symbolsCompleted = string.Empty, symbolsRest = string.Empty;

            string progressMessageLine = string.Format("Completed: {0}% ", ((float)currentValue / 100).ToString("0.00"));

            for (int i = 0; i < symbolsToShowCompletedCount; i++)
            {
                symbolsCompleted += PROGRESSBAR_COMPLETED_CHAR;
            }

            for (long y = symbolsToShowCompletedCount + 1; y < 21; y++)
            {
                symbolsRest += PROGRESSBAR_REST_CHAR;
            }

            if (currentValue < 10 * 100)
            {
                progressMessageLine += " ";
            }
            else if (currentValue >= 100 * 100)
            {
                progressMessageLine = progressMessageLine.Substring(0, progressMessageLine.Length - 1);
            }

            Write("\r{0} {1}{2} ", progressMessageLine, symbolsCompleted, symbolsRest);
        }

        private static void ShowWorkload(SWorkLoadSpec workloadSpec)
        {
            Write("Busy: CPU {0}% Read {1}% Write {2}%   "
                , workloadSpec.CpuLoad.ToString("#0")
                , workloadSpec.ReadLoad.ToString("#0")
                , workloadSpec.WriteLoad.ToString("#0"));
        }

        private static void ShowTaskResult(CTaskResultSpec taskResultSpec)
        {
            if (taskResultSpec == null || string.IsNullOrEmpty(taskResultSpec.OrigFileName))
                throw new NullReferenceException("Task result not set.");

            ShowOverallTaskResult(taskResultSpec.Mode, Path.GetFileName(taskResultSpec.OrigFileName), taskResultSpec.TaskResultType);
            
            if (taskResultSpec.IsCanceled)
            {
                WriteLine("Task was canceled.");
                return;
            }

            if (taskResultSpec.TaskResultType == EResultType.Success)
            {
                ShowDetailedTaskStats(taskResultSpec);
            }
            else
            {
                ShowTaskErrorsAndWarnings(taskResultSpec);
            }
        }

        private static void ShowOverallTaskResult(EWorkMode mode, string fileName, EResultType taskResultType)
        {
            WriteLine(string.Empty);
            
            string processingModeCaption;
            switch (mode)
            {
                case EWorkMode.Compress:
                case EWorkMode.Append:
                    processingModeCaption = "Compression";
                    break;
                case EWorkMode.Decompress:
                    processingModeCaption = "Decompression";
                    break;
                case EWorkMode.Browse:
                    processingModeCaption = "Browse";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }

            switch (taskResultType)
            {
                case EResultType.Warning:
                    WriteLine("{0} \"{1}\" finished with warning.", processingModeCaption, fileName);
                    break;
                case EResultType.Failed:
                case EResultType.None:
                    WriteLine("{0} \"{1}\" failed.", processingModeCaption, fileName);
                    break;
                case EResultType.Success:
                    WriteLine("{0} \"{1}\" finished successfully.", processingModeCaption, fileName);
                    break;
                default:
                    throw new NotSupportedException("Unsupported processing result value.");
            }
        }

        private static void ShowDetailedTaskStats(CTaskResultSpec taskResult)
        {
            TimeSpan duration = taskResult.Duration;

            long compressedObjectSize = taskResult.CompressedSize;
            long origObjectSize = taskResult.OrigSize;

            double ratio;
            if (compressedObjectSize > 0 && origObjectSize > 0)
            {
                ratio = (double) origObjectSize/compressedObjectSize;
            }
            else
            {
                ratio = 1;
            }

            double ratioPercent = 0;
            if (ratio > 0)
            {
                ratioPercent = 100 / ratio;
            }

            double speedMbSec = (double)origObjectSize / 1024 / 1024 / duration.TotalSeconds;
            string durationHumanString = GetDurationHumanString(duration);

            StringBuilder msgBuilder = new StringBuilder();
            msgBuilder.AppendFormat("[ orig. size {0} mb | ", ((double) origObjectSize/1024/1024).ToString("#,##0.##"));
            msgBuilder.AppendFormat("compressed size {0} mb | ", ((double) compressedObjectSize/1024/1024).ToString("#,##0.##"));
            msgBuilder.AppendFormat("ratio {0}% ]\n", ratioPercent.ToString("0.##"));
            msgBuilder.AppendFormat("[ duration {0} | speed {1} mb/s ]", durationHumanString, speedMbSec.ToString("#0.##"));
            
            WriteLine(msgBuilder.ToString());
        }

        private static void ShowTaskErrorsAndWarnings(CTaskResultSpec resultSpec)
        {
            foreach (CWorkerResultSpec workerResult in resultSpec.WorkerResults.Where
                (workerResult => workerResult.ResultType != EResultType.Success).Where
                (workerResult => workerResult.GetWarnings().Any() || workerResult.GetErrors().Any()))
            {
                WriteLine("{0}:", workerResult.Name);
                foreach (string warning in workerResult.GetWarnings())
                {
                    WriteLine("Warning: {0}", warning);
                }
                foreach (Exception exc in workerResult.GetErrors())
                {
                    ShowExceptionMessagesWithInner(exc);
                }
            }
        }

        private static string GetDurationHumanString(TimeSpan duration)
        {
            if (duration.TotalMilliseconds < 1)
                return "0";
            
            StringBuilder durationStrBuilder = new StringBuilder();

            if (duration.TotalHours >= 1)
            {
                durationStrBuilder.AppendFormat("{0:D2} h ", (int)duration.TotalHours);
            }

            if (duration.TotalMinutes >= 1)
            {
                durationStrBuilder.AppendFormat("{0:D2} m ", duration.Minutes);
            }

            if (duration.TotalSeconds < 2)
            {
                durationStrBuilder.AppendFormat("{0}.{1:D3} s", duration.Seconds.ToString("#0"), duration.Milliseconds);
            }
            else
            {
                durationStrBuilder.AppendFormat("{0} s", duration.Seconds.ToString("#0"));
            }
            
            return durationStrBuilder.ToString();
        }

        #endregion
    }
}

