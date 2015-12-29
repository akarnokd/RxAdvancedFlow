using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherFromArray<T> : ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly T[] array;

        bool cancelled;

        int index;

        long requested;

        public PublisherFromArray(ISubscriber<T> actual, T[] array)
        {
            this.actual = actual;
            this.array = array;
        }

        public void Cancel()
        {
            Volatile.Write(ref cancelled, true);
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (BackpressureHelper.Add(ref requested, n) == 0)
                {
                    if (n == long.MaxValue)
                    {
                        FastPath();
                    }
                    else
                    {
                        SlowPath(n);
                    }
                }
            }
        }

        void SlowPath(long n)
        {
            T[] a = array;
            int len = a.Length;
            ISubscriber<T> s = actual;
            long e = 0L;
            int i = index;

            for (;;)
            {
                while (e != n && i != len)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }
                    T t = a[i];

                    s.OnNext(t);

                    e++;
                    i++;
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }

                if (i == len)
                {
                    s.OnComplete();
                    return;
                }

                n = Volatile.Read(ref requested);
                if (n == e)
                {
                    index = i;
                    n = Interlocked.Add(ref requested, -e);
                    if (n == 0L)
                    {
                        break;
                    }
                    e = 0L;
                }
            }
        }

        void FastPath()
        {
            T[] a = array;
            int n = a.Length;
            ISubscriber<T> s = actual;

            for (int i = index; i != n; i++)
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                T t = a[i];

                s.OnNext(t);
            }
            if (Volatile.Read(ref cancelled))
            {
                return;
            }
            s.OnComplete();
        }
    }
}
