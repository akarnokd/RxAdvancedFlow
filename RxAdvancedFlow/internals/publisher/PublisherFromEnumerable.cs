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
    sealed class PublisherFromEnumerable<T> : ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IEnumerator<T> enumerator;

        bool cancelled;

        long requested;

        public PublisherFromEnumerable(ISubscriber<T> actual, IEnumerator<T> enumerator)
        {
            this.actual = actual;
            this.enumerator = enumerator;
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
            if (Volatile.Read(ref cancelled))
            {
                return;
            }

            ISubscriber<T> s = actual;
            IEnumerator<T> et = enumerator;

            long e = 0L;

            for (;;)
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }

                while (e != n)
                {
                    T t = et.Current;

                    s.OnNext(t);

                    bool b;
                    try
                    {
                        b = et.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        s.OnError(ex);
                        return;    
                    }

                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    if (!b)
                    {
                        s.OnComplete();
                        return;
                    }

                    e++;
                }

                n = Volatile.Read(ref requested);
                if (n == e)
                {
                    n = Interlocked.Add(ref requested, -e);
                    if (n == 0L)
                    {
                        break;
                    }
                }
            }
        }

        void FastPath()
        {
            if (Volatile.Read(ref cancelled))
            {
                return;
            }

            ISubscriber<T> s = actual;
            IEnumerator<T> et = enumerator;

            for (;;)
            {

                T t = et.Current;

                s.OnNext(t);

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }

                bool b;
                try
                {
                    b = et.MoveNext();
                }
                catch (Exception ex)
                {
                    s.OnError(ex);
                    return;
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }

                if (!b)
                {
                    s.OnComplete();
                    return;
                }
            }
        }
    }
}
