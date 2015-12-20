using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals
{
    static class BackpressureHelper
    {
        /// <summary>
        /// Adds two long values and caps the sum at long.MaxValue.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static long AddCap(long a, long b)
        {
            long c = a + b;
            if (c < 0)
            {
                return long.MaxValue;
            }
            return c;
        }

        /// <summary>
        /// Multiplies two long values and caps the result at long.MaxValue.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static long MultiplyCap(long a, long b)
        {
            long c = a * b;

            if (((a | b) >> 31) != 0)
            {
                if (b != 0 && c / b != a)
                {
                    return long.MaxValue;
                }
            }

            return c;
        }

        /// <summary>
        /// Atomically adds the value to the target field and caps the sum
        /// at long.MaxValue.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long Add(ref long field, long value)
        {
            for (;;)
            {
                long a = Volatile.Read(ref field);

                long u = AddCap(a, value);

                if (Interlocked.CompareExchange(ref field, u, a) == a)
                {
                    return a;
                }
            }
        }
    }
}
