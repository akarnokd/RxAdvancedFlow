using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                int oldOffset = (int)pi & m;

                int newLen = oldLen << 1;
                m = newLen - 1;
                int newOffset = (int)pi & m;

                T[] b = new T[newLen];

                int n = oldLen - oldOffset;
                Array.Copy(a, oldOffset, b, oldOffset, n);
                Array.Copy(a, 0, b, oldLen, oldOffset);

                mask = m;
                a = b;
                array = b;
                producerIndex = pi + 1;
                b[newOffset] = value;
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
    }
}
