using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals
{
    /// <summary>
    /// A structure holding onto a long value and padding
    /// on both sides.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    struct PaddedLong
    {
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p10, p11, p12, p13, p14, p15, p16, p17;

        long value;

        long p20, p21, p22, p23, p24, p25, p26, p27;
        long p30, p31, p32, p33, p34, p35, p36;

        internal long Get()
        {
            return value;
        }

        internal long GetVolatile()
        {
            return Volatile.Read(ref value);
        }

        internal void Set(long newValue)
        {
            this.value = newValue;
        }

        internal void SetVolatile(long newValue)
        {
            Volatile.Write(ref value, newValue);
        }

        internal long AddAndGet(long toAdd)
        {
            return Interlocked.Add(ref value, toAdd);
        }

        internal bool CompareAndSet(long expected, long update)
        {
            return Interlocked.CompareExchange(ref value, update, expected) == expected;
        }

        internal long AddCap(long n)
        {
            return BackpressureHelper.Add(ref value, n);
        }
    }
}
