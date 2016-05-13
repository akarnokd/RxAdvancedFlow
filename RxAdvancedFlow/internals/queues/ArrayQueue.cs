using System;

namespace RxAdvancedFlow.internals.queues
{

    /// <summary>
    /// An array-backed non-concurrent IQueue implementation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    sealed class ArrayQueue<T> : IQueue<T>
    {
        T[] array;

        int mask;

        long consumerIndex;

        long producerIndex;

        public void Clear()
        {
            QueueHelper.Clear(this);
        }

        public bool IsEmpty()
        {
            return producerIndex == consumerIndex;
        }

        public bool Offer(T value)
        {
            T[] a = array;
            int m = mask;
            long pi = producerIndex;

            if (a == null)
            {
                a = new T[8];
                m = 7;
                mask = m;
                array = a;
                a[0] = value;
            }
            else
            if (consumerIndex + m + 1 == pi)
            {
                int oldLen = a.Length;
                int offset = (int)pi & m;

                int newLen = oldLen << 1;
                m = newLen - 1;

                T[] b = new T[newLen];

                int n = oldLen - offset;
                Array.Copy(a, offset, b, offset, n);
                Array.Copy(a, 0, b, oldLen, offset);

                mask = m;
                a = b;
                array = b;
                producerIndex = pi + 1;
                b[(int)pi & m] = value;
            }
            else
            {
                int offset = (int)pi & m;
                producerIndex = pi + 1;
                a[offset] = value;
            }
            return true;
        }

        public bool Peek(out T value)
        {
            long ci = consumerIndex;
            if (ci != producerIndex)
            {
                value = array[(int)ci & mask];
                return true;
            }
            value = default(T);
            return false;
        }

        public bool Poll(out T value)
        {
            long ci = consumerIndex;
            if (ci != producerIndex)
            {
                int offset = (int)ci & mask;
                value = array[offset];

                consumerIndex = ci + 1;
                array[offset] = default(T);
                return true;
            }
            value = default(T);
            return false;
        }

        public int Size()
        {
            return (int)(producerIndex - consumerIndex);
        }

        public void Drop()
        {
            long ci = consumerIndex;
            if (ci != producerIndex)
            {
                consumerIndex = ci + 1;
                array[(int)ci & mask] = default(T);
            }
        }

        internal void ForEach(Action<T> action)
        {
            int m = mask;
            T[] a = array;

            for (long i = consumerIndex; i != producerIndex; i++)
            {
                int offset = (int)i & m;
                action(a[offset]);
            }
        }
    }
}
