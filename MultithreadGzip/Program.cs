using System;

namespace MultiThreadGzip
{
    class Program
    {
        private enum EReturnCode { Success = 0, Fail = 1 }

        private static int Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
                
                using (ConsoleApp app = new ConsoleApp())
                {
                    return app.Run(args) ? (int)EReturnCode.Success : (int)EReturnCode.Fail;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (int)EReturnCode.Fail;
            }
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Console.WriteLine(ex != null ? string.Format("Unhandled error exception:\n{0}", ex) : "Unhandled error exception");
            Environment.Exit((int)EReturnCode.Fail);
        }
    }
}