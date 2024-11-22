using System;
using MultiThreadGzip.Pipeline;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.MultiFileCompressor
{
    public enum ENotificationType
    {
        Messsage,
        DebugMessage,
        TaskProgress,
        TaskResult,
        ProcessingResult,
        Warning
    }

    public interface INotificationSpec
    {
        ENotificationType Type { get; }
    }

    public class CLogNotification : INotificationSpec
    {
        public CLogNotification(String message)
        {
            Type = ENotificationType.Messsage;
            Message = message;
        }

        public String Message { get; private set; }
        public ENotificationType Type { get; private set; }
    }

    public class CLogDebugNotification : INotificationSpec
    {
        public CLogDebugNotification(String message)
        {
            Type = ENotificationType.DebugMessage;
            Message = message;
        }

        public String Message { get; private set; }
        public ENotificationType Type { get; private set; }
    }
    
    public class CTaskNotification : INotificationSpec
    {
        public CTaskNotification(CTaskResultSpec taskResult)
        {
            Type = ENotificationType.TaskResult;
            TaskResult = taskResult;
        }

        public CTaskResultSpec TaskResult { get; private set; }
        public ENotificationType Type { get; private set; }
    }

    public class CWarningNotification : INotificationSpec
    {
        public CWarningNotification(String text)
        {
            Type = ENotificationType.Warning;
            Warning = text;
        }

        public String Warning { get; private set; }
        public ENotificationType Type { get; private set; }
    }

    public class CProcessingNotification : INotificationSpec
    {
        public CProcessingNotification(CProcessingResultSpec processingResult)
        {
            Type = ENotificationType.TaskResult;
            ProcessingResult = processingResult;
        }

        public CProcessingResultSpec ProcessingResult { get; private set; }
        public ENotificationType Type { get; private set; }
    }

    public class CTaskProgress : INotificationSpec
    {
        public CTaskProgress(Int32 prcntCompleted, SWorkLoadSpec loadSpec)
        {
            Type = ENotificationType.TaskProgress;
            PrcntCompleted = prcntCompleted;
            LoadSpec = loadSpec;
        }

        public Int32 PrcntCompleted { get; private set; }
        public SWorkLoadSpec LoadSpec { get; private set; }
        public ENotificationType Type { get; private set; }
    }
}
