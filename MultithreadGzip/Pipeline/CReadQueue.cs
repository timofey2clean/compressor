using System;
using System.Collections.Generic;
using System.Threading;

namespace MultiThreadGzip.Pipeline
{
    class CReadQueue : IThreadSafeQueue
    {
        private readonly object _locker;
        private readonly int _capacity;
        private readonly Queue<CDataBlock> _blocksQueue;
        private readonly ManualResetEvent _canEnqueueEvent;
        private readonly ManualResetEvent _canDequeueEvent;
        private readonly ManualResetEvent _stoppedEvent;
        private readonly ManualResetEvent _abortedEvent;

        public CReadQueue(int capacity, ManualResetEvent abortEvent)
        {
            _locker = new object();
            _capacity = capacity;
            _blocksQueue = new Queue<CDataBlock>();
            _canEnqueueEvent = new ManualResetEvent(true);
            _canDequeueEvent = new ManualResetEvent(false);
            _stoppedEvent = new ManualResetEvent(false);
            _abortedEvent = abortEvent;
        }

        public void StopWaitingNewData()
        {
            if (_stoppedEvent != null)
                _stoppedEvent.Set();
        }

        public void Enqueue(CDataBlock block)
        {
            if (block == null)
                return;

            WaitHandle.WaitAny(new WaitHandle[] { _canEnqueueEvent, _abortedEvent });
            Monitor.Enter(_locker);

            try
            {
                if (_blocksQueue != null)
                    _blocksQueue.Enqueue(block);

                CheckQueueAvailable();
            }
            catch (Exception ex)
            {
                Monitor.Exit(_locker);
                throw new Exception("Failed to add block into read queue", ex);
            }
            Monitor.Exit(_locker);
        }

        public CDataBlock Dequeue()
        {
            CDataBlock block = null;

            while (!_abortedEvent.WaitOne(0) && !(_stoppedEvent.WaitOne(0) && IsEmpty()))
            {
                WaitHandle.WaitAny(new WaitHandle[] { _canDequeueEvent, _stoppedEvent, _abortedEvent });
                Monitor.Enter(_locker);

                if (_blocksQueue != null && _blocksQueue.Count > 0)
                {
                    try
                    {
                        block = _blocksQueue.Dequeue();
                        CheckQueueAvailable();
                    }
                    catch (Exception ex)
                    {
                        Monitor.Exit(_locker);
                        throw new Exception("Failed to remove block from read queue", ex);
                    }
                    Monitor.Exit(_locker);
                    break;
                }
                Monitor.Exit(_locker);
            }

            return block;
        }

        private void CheckQueueAvailable()
        {
            // Check if can enqueue
            if (_blocksQueue.Count < _capacity)
            {
                _canEnqueueEvent.Set();
            }
            else
            {
                _canEnqueueEvent.Reset();
            }

            // Check if can dequeue
            if (_blocksQueue.Count > 0)
            {
                _canDequeueEvent.Set();
            }
            else
            {
                _canDequeueEvent.Reset();
            }
        }

        private bool IsEmpty()
        {
            Monitor.Enter(_locker);
            bool queueIsEmpty = _blocksQueue != null && _blocksQueue.Count < 1;
            Monitor.Exit(_locker);

            return queueIsEmpty;
        }
    }
}
