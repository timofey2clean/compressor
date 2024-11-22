using System;
using System.Collections.Generic;
using MultiThreadGzip.Pipeline;
using MultiThreadGzip.WorkloadCounter;

namespace MultiThreadGzip.MultiFileCompressor
{
    public class Notifier
    {
        private static readonly object Lck = new object();
        private static Notifier _inst;

        private Notifier()
        {
            Observers = new List<IObserver<INotificationSpec>>();
        }

        private List<IObserver<INotificationSpec>> Observers { get; set; }

        private static Notifier Inst
        {
            get
            {
                if (_inst == null)
                {
                    lock (Lck)
                    {
                        if (_inst == null)
                        {
                            _inst = new Notifier();
                        }
                    }
                }

                return _inst;
            }
        }

        public static void Message(string text, params object[] args)
        {
            Inst.Notify(new CLogNotification(string.Format(text, args)));
        }

        public static void Debug(string text, params object[] args)
        {
            if (string.IsNullOrEmpty(text))
                return;

            Inst.Notify(new CLogDebugNotification(string.Format(text, args)));
        }

        public static void Exception(Exception exc)
        {
            Inst.NotifyError(exc);
        }
        
        public static void Progress(int hundredthPrcnt, SWorkLoadSpec workLoadSpec)
        {
            Inst.Notify(new CTaskProgress(hundredthPrcnt, workLoadSpec));
        }

        public static void TaskResult(CTaskResultSpec taskResult)
        {
            Inst.Notify(new CTaskNotification(taskResult));
        }
        
        public static IDisposable Subscribe(IObserver<INotificationSpec> observer)
        {
            return new Unsubscriber<INotificationSpec>(Inst.Observers, observer);
        }

        private void Notify(INotificationSpec spec)
        {
            foreach (IObserver<INotificationSpec> observer in Observers)
            {
                observer.OnNext(spec);
            }
        }

        private void NotifyError(Exception exc)
        {
            foreach (IObserver<INotificationSpec> observer in Observers)
            {
                observer.OnError(exc);
            }
        }
    }
}
