﻿namespace RxAdvancedFlow.internals.queues
{
    sealed class SpscArrayQueue<T> : IQueue<T>
    {

        SpscArrayQueueStruct<T> q;

        public SpscArrayQueue(int capacity)
        {
            q.Init(capacity);
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
