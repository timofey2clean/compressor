using System;
using System.Collections;
using System.Collections.Generic;

namespace MultiThreadGzip
{
    public class DisposableList : IDisposable, IEnumerable
    {
        private readonly List<IDisposable> _disposables;
        private readonly object _lck = new object();

        public DisposableList()
        {
            _disposables = new List<IDisposable>(); ;
        }

        public void Dispose()
        {
            lock (_lck)
            {
                foreach (IDisposable obj in _disposables)
                {
                    try
                    {
                        obj.Dispose();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Failed to dispose object in the list", ex);
                    }
                }
                _disposables.Clear();
            }
        }

        public T Add<T>(T obj) where T : IDisposable
        {
            lock (_lck)
            {
                _disposables.Add(obj);
                return obj;
            }
        }

        public void AddRange(IEnumerable<IDisposable> objs)
        {
            lock (_lck)
            {
                _disposables.AddRange(objs);
            }
        }

        public void Remove(IDisposable obj)
        {
            lock (_lck)
            {
                _disposables.Remove(obj);
            }
        }

        public void Clear()
        {
            lock (_lck)
            {
                _disposables.Clear();
            }
        }

        public IEnumerator GetEnumerator()
        {
            lock (_lck)
            {
                return _disposables.GetEnumerator();
            }
        }
    }
}
