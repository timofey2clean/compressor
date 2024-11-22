using System;
using System.Collections.Generic;
using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip.Pipeline
{
    public class CWorkerResultSpec
    {
        private readonly List<string> _warnings;
        private readonly List<Exception> _exceptions;

        public CWorkerResultSpec(string workerName)
        {
            Name = workerName;
            ResultType = EResultType.None;
            _exceptions = new List<Exception>();
            _warnings = new List<string>();
        }

        public string Name { get; set; }
        public EResultType ResultType { get; set; }
        
        public void UpdateResultOptionally(EResultType resultType)
        {
            switch (resultType)
            {
                case EResultType.None:
                case EResultType.Failed:
                    ResultType = resultType;
                    break;
                case EResultType.Warning:
                    if (ResultType != EResultType.Failed)
                        ResultType = EResultType.Warning;
                    break;
                case EResultType.Success:
                    if (ResultType != EResultType.Warning && ResultType != EResultType.Failed)
                        ResultType = resultType;
                    break;
                default:
                    throw new ArgumentException(string.Format("Unknown result \"{0}\".", resultType));
            }
        }

        public void AddError(Exception ex)
        {
            _exceptions.Add(ex);
            UpdateResultOptionally(EResultType.Failed);
        }

        public void AddWarning(string text)
        {
            _warnings.Add(text);
            UpdateResultOptionally(EResultType.Warning);
        }

        public Exception[] GetErrors()
        {
            return _exceptions.ToArray();
        }

        public string[] GetWarnings()
        {
            return _warnings.ToArray();
        }
    }
}
