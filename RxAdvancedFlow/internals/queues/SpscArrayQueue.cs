using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RxAdvancedFlow.internals.queues.QueueHelper;

namespace RxAdvancedFlow.internals.queues
{
    sealed class SpscArrayQueue<T> : IQueue<T>
    {

        SpscStructArrayQueue<T> q;

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
