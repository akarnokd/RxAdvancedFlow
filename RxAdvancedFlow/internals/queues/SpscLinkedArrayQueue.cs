namespace RxAdvancedFlow.internals.queues
{
    sealed class SpscLinkedArrayQueue<T> : IQueue<T>
    {
        SpscLinkedArrayQueueStruct<T> q;

        public SpscLinkedArrayQueue(int islandSize)
        {
            q.Init(islandSize);
        }

        public void Clear()
        {
            q.Clear();
        }

        public bool IsEmpty()
        {
            return q.IsEmpty();
        }

        public bool Offer(T value)
        {
            return q.Offer(value);
        }

        public bool Peek(out T value)
        {
            return q.Peek(out value);
        }

        public bool Poll(out T value)
        {
            return q.Poll(out value);
        }

        public int Size()
        {
            return q.Size();
        }
    }
}
