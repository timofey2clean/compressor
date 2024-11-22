using System;
using System.Collections.Generic;
using System.Threading;

namespace MultiThreadGzip.Pipeline
{
    class CWriteQueue : IThreadSafeQueue
    {
        private readonly object _locker;
        private readonly int _capacity;
        private readonly Dictionary<long, CDataBlock> _blocksDictionary;
        private readonly ManualResetEvent _canEnqueueEvent;
        private readonly ManualResetEvent _canDequeueEvent;
        private readonly ManualResetEvent _stoppedEvent;
        private readonly ManualResetEvent _abortedEvent;
        private long _blockCounter;

        public CWriteQueue(int capacity, ManualResetEvent abortedEvent)
        {
            _locker = new object();
            _capacity = capacity;
            _blocksDictionary = new Dictionary<long, CDataBlock>();
            _canEnqueueEvent = new ManualResetEvent(true);
            _canDequeueEvent = new ManualResetEvent(false);
            _stoppedEvent = new ManualResetEvent(false);
            _abortedEvent = abortedEvent;
            _blockCounter = 0;
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

            while (!_abortedEvent.WaitOne(0))
            {
                WaitHandle.WaitAny(new WaitHandle[] { _canEnqueueEvent, _abortedEvent });
                Monitor.Enter(_locker); // Prefer Monitor.Enter-Exit instead of lock.

                if (_blocksDictionary != null && (_blocksDictionary.Count < _capacity || block.Number == _blockCounter))
                {
                    try
                    {
                        _blocksDictionary.Add(block.Number, block);
                        CheckQueueAvailable();
                    }
                    catch (Exception ex)
                    {
                        Monitor.Exit(_locker);
                        throw new Exception("Failed to add block into write queue", ex);
                    }
                    Monitor.Exit(_locker);
                    break;
                }
                Monitor.Exit(_locker);
            }
        }

        public CDataBlock Dequeue()
        {
            CDataBlock block = null;

            while (!_abortedEvent.WaitOne(0) && !(_stoppedEvent.WaitOne(0) && IsEmpty()))
            {
                WaitHandle.WaitAny(new WaitHandle[] { _canDequeueEvent, _stoppedEvent, _abortedEvent });
                Monitor.Enter(_locker);

                if (_blocksDictionary != null && _blocksDictionary.ContainsKey(_blockCounter))
                {
                    try
                    {
                        block = _blocksDictionary[_blockCounter];
                        _blocksDictionary.Remove(_blockCounter);
                        _blockCounter++;
                        CheckQueueAvailable();
                    }
                    catch (Exception ex)
                    {
                        Monitor.Exit(_locker);
                        throw new Exception("Failed to remove block from write queue", ex);
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
            // Check can enqueue
            if (_blocksDictionary.Count < _capacity)
            {
                _canEnqueueEvent.Set();
            }
            else
            {
                _canEnqueueEvent.Reset();
            }

            // Check can enqueue and dequeue if dict. contains next block number
            if (!_blocksDictionary.ContainsKey(_blockCounter))
            {
                _canDequeueEvent.Reset();
                _canEnqueueEvent.Set();
            }
            else
            {
                _canDequeueEvent.Set();
            }
        }

        private bool IsEmpty()
        {
            Monitor.Enter(_locker);
            bool queueIsEmpty = _blocksDictionary != null && _blocksDictionary.Count < 1;
            Monitor.Exit(_locker);

            return queueIsEmpty;
        }
    }
}