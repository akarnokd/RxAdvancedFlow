using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.queues
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct SpscLinkedArrayQueueStruct<T>
    {
        internal int mask;

        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p08, p09, p0A, p0B, p0C, p0D, p0E;

        internal long producerIndex;
        internal Slot[] producerArray;

        long p10, p11, p12, p13, p14, p15, p16, p17;
        long p18, p19, p1A, p1B, p1C, p1D;

        internal long consumerIndex;
        internal Slot[] consumerArray;

        long p20, p21, p22, p23, p24, p25, p26, p27;
        long p28, p29, p2A, p2B, p2C, p2D;

        internal void Init(int capacity)
        {
            int c = QueueHelper.RoundPowerOf2(Math.Max(2, capacity));
            producerArray = consumerArray = new Slot[c + 2];
            mask = c - 1;
        }

        /// <summary>
        /// Initializes the arrays once.
        /// This should be run from the main emission thread and it
        /// flushes the consumer array via a volatile write
        /// </summary>
        /// <param name="capacity"></param>
        internal void InitOnce(int capacity)
        {
            if (producerArray == null)
            {
                InitVolatile(capacity);
            }
        }

        internal void InitVolatile(int capacity)
        {
            int c = QueueHelper.RoundPowerOf2(Math.Max(2, capacity));
            Slot[] a = new Slot[c + 2];
            mask = c - 1;
            producerArray = a;
            Volatile.Write(ref consumerArray, a);
        }

        /// <summary>
        /// Returns true if the queue can be consumed.
        /// </summary>
        /// <returns></returns>
        internal bool IsConsumable()
        {
            return Volatile.Read(ref consumerArray) != null;
        }

        internal int Size()
        {
            return QueueHelper.Size(ref producerIndex, ref consumerIndex);
        }

        internal bool IsEmpty()
        {
            return QueueHelper.IsEmpty(ref producerIndex, ref consumerIndex);
        }

        internal void Clear()
        {
            T ignored;

            while (Poll(out ignored) && !IsEmpty()) ;
        }

        int CalcOffset(long index, int m)
        {
            return 1 + ((int)index & m);
        }

        internal bool OfferRef(ref T value)
        {
            Slot[] a = producerArray;
            int m = mask;
            long pi = Volatile.Read(ref producerIndex);

            int offset1 = CalcOffset(pi + 1, m);

            if (a[offset1].Used() != 0)
            {
                int offset0 = CalcOffset(pi, m);

                Slot[] b = new Slot[m + 3];

                producerArray = b;
                b[offset0].SpValueRef(ref value);
                Volatile.Write(ref producerIndex, pi + 1);
                a[offset0].SoNext(b);
            }
            else
            {
                int offset0 = CalcOffset(pi, m);

                Volatile.Write(ref producerIndex, pi + 1);
                a[offset0].SoValueRef(ref value);
            }

            return true;
        }

        internal bool Offer(T value)
        {
            Slot[] a = producerArray;
            int m = mask;
            long pi = Volatile.Read(ref producerIndex);

            int offset1 = CalcOffset(pi + 1, m);

            if (a[offset1].Used() != 0)
            {
                int offset0 = CalcOffset(pi, m);

                Slot[] b = new Slot[m + 3];

                producerArray = b;
                b[offset0].SpValue(value);
                Volatile.Write(ref producerIndex, pi + 1);
                a[offset0].SoNext(b);
            }
            else
            {
                int offset0 = CalcOffset(pi, m);

                Volatile.Write(ref producerIndex, pi + 1);
                a[offset0].SoValue(value);
            }

            return true;
        }

        internal bool Peek(out T value)
        {
            Slot[] a = consumerArray;
            int m = mask;
            long ci = Volatile.Read(ref consumerIndex);

            int offset = CalcOffset(ci, m);

            byte t = a[offset].Used();

            if (t == 0)
            {
                value = default(T);
                return false;
            }
            else
            if (t == 1)
            {
                value = a[offset].LpValue();
                return true;
            }

            Slot[] b = a[offset].LpNext();

            value = b[offset].LpValue();
            return true;
        }


        internal bool Poll(out T value)
        {
            Slot[] a = consumerArray;
            int m = mask;
            long ci = Volatile.Read(ref consumerIndex);

            int offset = CalcOffset(ci, m);

            byte t = a[offset].Used();

            if (t == 0)
            {
                value = default(T);
                return false;
            }
            else
            if (t == 1)
            {
                value = a[offset].LpValue();
                Volatile.Write(ref consumerIndex, ci + 1);
                a[offset].Free();
                return true;
            }

            Slot[] b = a[offset].LpNext();

            consumerArray = b;

            value = b[offset].LpValue();
            Volatile.Write(ref consumerIndex, ci + 1);
            b[offset].Free();

            return true;
        }

        /// <summary>
        /// Drops the current value available via Peek.
        /// </summary>
        public void Drop()
        {
            Slot[] a = consumerArray;
            int m = mask;
            long ci = Volatile.Read(ref consumerIndex);

            int offset = CalcOffset(ci, m);

            byte t = a[offset].Used();

            if (t == 0)
            {
                return;
            }
            else
            if (t == 1)
            {
                Volatile.Write(ref consumerIndex, ci + 1);
                a[offset].Free();
                return;
            }

            Slot[] b = a[offset].LpNext();

            consumerArray = b;

            Volatile.Write(ref consumerIndex, ci + 1);
            b[offset].Free();
        }


        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Slot
        {
            long p0, p1, p2, p3, p4, p5, p6, p7;

            T value;
            byte used;

            Slot[] next;

            long p10, p11, p12, p13, p14, p15;

            internal byte Used()
            {
                return Volatile.Read(ref used);
            }

            internal void SpValue(T v)
            {
                value = v;
                used = 1;
            }

            internal void SpValueRef(ref T v)
            {
                value = v;
                used = 1;
            }

            internal void SoValue(T v)
            {
                value = v;
                Volatile.Write(ref used, 1);
            }

            internal void SoValueRef(ref T v)
            {
                value = v;
                Volatile.Write(ref used, 1);
            }

            internal void SoNext(Slot[] n)
            {
                next = n;
                Volatile.Write(ref used, 2);
            }

            internal T LpValue()
            {
                return value;
            }

            internal Slot[] LpNext()
            {
                return next;
            }

            internal void Free()
            {
                value = default(T);
                Volatile.Write(ref used, 0);
            }
        }
    }
}
