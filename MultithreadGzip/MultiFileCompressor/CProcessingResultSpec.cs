using System;
using System.Collections.Generic;
using System.Linq;
using MultiThreadGzip.Pipeline;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.MultiFileCompressor
{
    public enum EResultType
    {
        Success,
        Failed,
        Warning,
        None
    }

    public class CProcessingResultSpec
    {
        public CProcessingResultSpec(EWorkMode mode)
        {
            Mode = mode;
            OverallResultType = EResultType.None;
            TaskResults = new List<CTaskResultSpec>();
        }

        private enum EWorkLoadParam { CPU, Read, Write };

        public EWorkMode Mode { get; private set; }
        public EResultType OverallResultType { get; private set; }
        
        public IList<CTaskResultSpec> TaskResults { get; private set; }

        public SWorkLoadSpec GetAverageWorkload()
        {
            return new SWorkLoadSpec
            {
                CpuLoad = GetAverageWorkload(EWorkLoadParam.CPU),
                ReadLoad = GetAverageWorkload(EWorkLoadParam.Read),
                WriteLoad = GetAverageWorkload(EWorkLoadParam.Write)
            };
        }
        
        public void AddTaskResult(CTaskResultSpec taskResult)
        {
            TaskResults.Add(taskResult);
            UpdateOverallResultOptionally(taskResult.TaskResultType);
        }

        public void UpdateOverallResultOptionally(EResultType resultType)
        {
            switch (resultType)
            {
                case EResultType.None:
                case EResultType.Failed:
                    OverallResultType = resultType;
                    break;
                case EResultType.Warning:
                    if (OverallResultType != EResultType.Failed)
                        OverallResultType = EResultType.Warning;
                    break;
                case EResultType.Success:
                    if (OverallResultType != EResultType.Warning && OverallResultType != EResultType.Failed)
                        OverallResultType = EResultType.Success;
                    break;
                default:
                    throw new ArgumentException(string.Format("Unknown result \"{0}\".", resultType));
            }
        }

        private int GetAverageWorkload(EWorkLoadParam workLoadParam)
        {
            double averageWorkload = 0;
            long totalSize = TaskResults.Sum(taskResult => taskResult.OrigSize);
            
            if (totalSize == 0)
                return 0;

            foreach (CTaskResultSpec taskResult in TaskResults)
            {
                long taskSize = taskResult.OrigSize;
                
                int taskWorkload;
                switch (workLoadParam)
                {
                    case EWorkLoadParam.CPU:
                        taskWorkload = taskResult.WorkLoad.CpuLoad;
                        break;
                    case EWorkLoadParam.Read:
                        taskWorkload = taskResult.WorkLoad.ReadLoad;
                        break;
                    case EWorkLoadParam.Write:
                        taskWorkload = taskResult.WorkLoad.WriteLoad;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("workLoadParam", workLoadParam, null);
                }

                averageWorkload += taskWorkload * ((double)taskSize / totalSize);
            }

            return (int)averageWorkload;
        }
    }
}
