using System;
using System.Collections.Generic;

namespace MultiThreadGzip.MultiFileCompressor
{
    public class Unsubscriber<T> : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly IObserver<T> _newObserver;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> newObserver)
        {
            _observers = observers;
            _newObserver = newObserver;

            if (!_observers.Contains(newObserver))
                _observers.Add(newObserver);
        }

        public void Dispose()
        {
            if (_observers.Contains(_newObserver))
                _observers.Remove(_newObserver);
        }
    }
}