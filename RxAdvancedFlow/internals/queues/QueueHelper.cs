using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.queues
{
    static class QueueHelper
    {
        /*
        public static IQueue<T> CreateSpscArrayQueue<T>(int capacity)
        {

            Type t = typeof(T);

            if (t.IsValueType || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)))
            {
                return (IQueue<T>)Activator
                    .CreateInstance(typeof(SpscStructArrayQueue<>).MakeGenericType(typeof(T)));
            }

            return (IQueue<T>)Activator
                .CreateInstance(typeof(SpscRefArrayQueue<>).MakeGenericType(typeof(T)));
        }
        */

        public static int RoundPowerOf2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;

            return v;
        }

        public static void Clear<T>(IQueue<T> q)
        {
            T ignored;

            while (q.Poll(out ignored) && !q.IsEmpty()) ;
        }

        public static bool IsEmpty(ref long producerIndex, ref long consumerIndex)
        {
            return Volatile.Read(ref producerIndex) == Volatile.Read(ref consumerIndex);
        }

        public static int Size(ref long producerIndex, ref long consumerIndex)
        {
            long ci = Volatile.Read(ref consumerIndex);

            for (;;)
            {
                long pi = Volatile.Read(ref producerIndex);

                long ci2 = Volatile.Read(ref consumerIndex);

                if (ci == ci2)
                {
                    return (int)(pi - ci);
                }
                ci = ci2;
            }
        }
    }
}
