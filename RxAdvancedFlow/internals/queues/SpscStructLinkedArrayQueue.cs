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
    internal struct SpscStructLinkedArrayQueue<T>
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
            int c = QueueHelper.RoundPowerOf2(capacity);
            producerArray = consumerArray = new Slot[c + 2];
            mask = c - 1;
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

            internal void SoValue(T v)
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

            internal T GetAndNull()
            {
                T v = value;
                Free();
                return v;
            }

            internal void Free()
            {
                value = default(T);
                Volatile.Write(ref used, 0);
            }
        }
    }
}
