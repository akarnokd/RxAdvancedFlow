using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherRange : ISubscription
    {
        readonly ISubscriber<int> actual;

        readonly long end;

        long index;

        bool cancelled;

        long requested;

        public PublisherRange(long start, long end, ISubscriber<int> actual)
        {
            this.actual = actual;
            this.index = start;
            this.end = end;
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
                    long idx = index;
                    if (n >= idx)
                    {
                        FastPath(idx);
                    }
                    else
                    {
                        SlowPath(idx, n);
                    }
                }
            }
        }

        bool IsCancelled()
        {
            return Volatile.Read(ref cancelled);
        }

        void FastPath(long idx)
        {
            ISubscriber<int> a = actual;
            long f = end;

            for (; idx != f; idx++)
            {
                if (IsCancelled())
                {
                    return;
                }

                a.OnNext((int)idx);
            }

            if (IsCancelled())
            {
                return;
            }
            a.OnComplete();
        }

        void SlowPath(long idx, long n)
        {
            ISubscriber<int> a = actual;
            long f = end;
            long e = 0L;

            for (;;)
            {
                if (IsCancelled())
                {
                    return;
                }

                while (n != e && idx != f)
                {
                    a.OnNext((int)idx);

                    if (IsCancelled())
                    {
                        return;
                    }

                    idx++;
                    e++;
                }

                if (idx == f) {
                    a.OnComplete();
                    return;
                }

                n = Volatile.Read(ref requested);

                if (n == e)
                {
                    index = idx;
                    n = Interlocked.Add(ref requested, -e);
                    if (n == 0)
                    {
                        return;
                    }
                    e = 0L;
                }
            }
        }
    }
}
