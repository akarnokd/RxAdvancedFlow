using System.Runtime.InteropServices;
using System.Threading;

namespace RxAdvancedFlow.internals.queues
{
    /// <summary>
    /// A structure-based single producer single consumer queue
    /// that can be inlined into other structures or classes for
    /// more efficient memory layout.
    /// 
    /// You may want to pre-pad the struct (post pad is present).
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct SpscArrayQueueStruct<T>
    {
        internal Slot[] array;
        internal int mask;

        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p08, p09, p0A, p0B, p0C, p0D;

        internal long producerIndex;

        long p10, p11, p12, p13, p14, p15, p16, p17;
        long p18, p19, p1A, p1B, p1C, p1D, p1E;

        internal long consumerIndex;

        long p20, p21, p22, p23, p24, p25, p26, p27;
        long p28, p29, p2A, p2B, p2C, p2D, p2E;

        internal void Init(int capacity)
        {
            int c = QueueHelper.RoundPowerOf2(capacity);
            array = new Slot[c + 2];
            mask = c - 1;
        }

        internal void InitOnce(int capacity)
        {
            if (array == null)
            {
                InitVolatile(capacity);
            }
        }

        internal void InitVolatile(int capacity)
        {
            int c = QueueHelper.RoundPowerOf2(capacity);
            mask = c - 1;
            Volatile.Write(ref array, new Slot[c + 2]);
        }

        internal bool IsConsumable()
        {
            return Volatile.Read(ref array) != null;
        }

        int CalcOffset(long index, int m)
        {
            return 1 + ((int)index & m);
        }

        internal bool Offer(T value)
        {
            Slot[] a = array;
            int m = mask;
            long pi = LvProducerIndex();

            int offset = CalcOffset(pi, m);

            if (a[offset].IsUsed())
            {
                return false;
            }

            SoProducerIndex(pi + 1);
            a[offset].SoValue(value);

            return true;
        }

        internal bool Peek(out T value)
        {
            Slot[] a = array;
            int m = mask;
            long ci = LvConsumerIndex();

            int offset = CalcOffset(ci, m);

            if (a[offset].IsUsed())
            {
                value = a[offset].LpValue();
                return true;
            }
            value = default(T);
            return false;
        }

        internal bool Poll(out T value)
        {
            Slot[] a = array;
            int m = mask;
            long ci = LvConsumerIndex();

            int offset = CalcOffset(ci, m);

            if (a[offset].IsUsed())
            {

                value = a[offset].LpValue();
                SoConsumerIndex(ci + 1);
                a[offset].Free();
                return true;
            }
            value = default(T);
            return false;
        }

        internal void Drop()
        {
            Slot[] a = array;
            int m = mask;
            long ci = LvConsumerIndex();

            int offset = CalcOffset(ci, m);

            if (a[offset].IsUsed())
            {
                SoConsumerIndex(ci + 1);
                a[offset].Free();
            }
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

        internal long LvProducerIndex()
        {
            return Volatile.Read(ref producerIndex);
        }

        internal long LvConsumerIndex()
        {
            return Volatile.Read(ref consumerIndex);
        }

        internal void SoProducerIndex(long newValue)
        {
            Volatile.Write(ref producerIndex, newValue);
        }

        internal void SoConsumerIndex(long newValue)
        {
            Volatile.Write(ref consumerIndex, newValue);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Slot
        {
            long p0, p1, p2, p3, p4, p5, p6, p7;

            T value;
            bool used;

            long p9, p10, p11, p12, p13, p14, p15;

            internal bool IsUsed()
            {
                return Volatile.Read(ref used);
            }

            internal void SoValue(T v)
            {
                value = v;
                Volatile.Write(ref used, true);
            }

            internal T LpValue()
            {
                return value;
            }

            internal void Free()
            {
                value = default(T);
                Volatile.Write(ref used, false);
            }
        }

    }
}
