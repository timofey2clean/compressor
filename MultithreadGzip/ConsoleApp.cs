using System;
using System.Threading;
using MultiThreadGzip.MultiFileCompressor;

namespace MultiThreadGzip
{
    class ConsoleApp : IObserver<INotificationSpec>, IDisposable
    {
        private const string INVALID_SPEC_TYPE_ERROR = "Invalid processing spec type.";
        
        private readonly CMultiFileCompressor _multiFileCompressor;
        private readonly DisposableList _disposables;

        public ConsoleApp()
        {
            _disposables = new DisposableList();
            _multiFileCompressor = new CMultiFileCompressor();
            _disposables.Add(_multiFileCompressor.Subscribe(this));
            Console.CancelKeyPress += OnCtrlC;
        }

        public void Dispose()
        {
            try
            {
                Console.CancelKeyPress -= OnCtrlC;
                _disposables.Dispose();
            }
            catch (Exception ex)
            {
                OutputHelper.ShowExceptionMessagesWithInner(ex);
            }
        }

        public bool Run(string[] args)
        {
            Thread.CurrentThread.Name = "Main";

            CProcessingSpec spec;
            if (!StartupArgumentsParser.TryParseArgs(args, out spec))
            {
                OutputHelper.ShowHelp();
                return false;
            }
            
            switch (spec.Mode)
            {
                case (EWorkMode.Browse):
                    return Browse(spec);
                case (EWorkMode.Decompress):
                    return Decompress(spec);
                case (EWorkMode.Compress):
                    return Compress(spec);
                case (EWorkMode.Append):
                    return Append(spec);
                default:
                    throw new ArgumentOutOfRangeException("args");
            }
        }

        private bool Compress(CProcessingSpec spec)
        {
            CCompressionSpec compressionSpec = spec as CCompressionSpec;
            if (compressionSpec == null)
                throw new Exception(INVALID_SPEC_TYPE_ERROR);

            CProcessingResultSpec resultSpec = _multiFileCompressor.Compress(compressionSpec);
            if (resultSpec != null)
            {
                OutputHelper.ShowProcessingRestul(resultSpec);
                return (resultSpec.OverallResultType == EResultType.Success || resultSpec.OverallResultType == EResultType.Warning);
            }

            return false;
        }

        private bool Append(CProcessingSpec spec)
        {
            CCompressionSpec compressionSpec = spec as CCompressionSpec;
            if (compressionSpec == null)
                throw new Exception(INVALID_SPEC_TYPE_ERROR);

            CProcessingResultSpec resultSpec = _multiFileCompressor.Append(compressionSpec);
            if (resultSpec != null)
            {
                OutputHelper.ShowProcessingRestul(resultSpec);
                return (resultSpec.OverallResultType == EResultType.Success || resultSpec.OverallResultType == EResultType.Warning);
            }

            return false;
        }

        private bool Decompress(CProcessingSpec spec)
        {
            CDecompressionSpec decompressionSpec = spec as CDecompressionSpec;
            if (decompressionSpec == null)
                throw new Exception(INVALID_SPEC_TYPE_ERROR);

            CProcessingResultSpec resultSpec = _multiFileCompressor.Decompress(decompressionSpec);
            if (resultSpec != null)
            {
                OutputHelper.ShowProcessingRestul(resultSpec);
                return (resultSpec.OverallResultType == EResultType.Success || resultSpec.OverallResultType == EResultType.Warning);
            }

            return false;
        }

        private bool Browse(CProcessingSpec spec)
        {
            CBrowseSpec browseSpec = spec as CBrowseSpec;
            if (browseSpec == null)
                throw new Exception(INVALID_SPEC_TYPE_ERROR);

            CArchiveMetadata archiveSpec = _multiFileCompressor.Browse(browseSpec);
            if (archiveSpec != null)
            {
                OutputHelper.ShowMultiFileArchive(archiveSpec);
                return true;
            }

            return false;
        }

        private void OnCtrlC(object sender, ConsoleCancelEventArgs e)
        {
            try
            {
                e.Cancel = true;

                if (_multiFileCompressor != null)
                    _multiFileCompressor.Cancel();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to cancel processing:\n{0}", ex);
            }
        }

        #region Observer
        public void OnNext(INotificationSpec value)
        {
            OutputHelper.ShowNotification(value);
        }

        public void OnError(Exception error)
        {
            OutputHelper.ShowExceptionMessagesWithInner(error);
        }

        public void OnCompleted()
        {
            // Ignore
        }
        #endregion
    }
}
