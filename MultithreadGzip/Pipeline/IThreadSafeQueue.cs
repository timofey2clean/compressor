namespace MultiThreadGzip.Pipeline
{
    interface IThreadSafeQueue
    {
        void Enqueue(CDataBlock block);
        CDataBlock Dequeue();
        void StopWaitingNewData();
    }
}
