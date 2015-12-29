using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherScan<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly Func<R, T, R> accumulator;

        ISubscription s;

        R value;

        bool done;

        long requested;

        long produced;

        public PublisherScan(ISubscriber<R> actual, R value, Func<R, T, R> accumulator)
        {
            this.actual = actual;
            this.value = value;
            this.accumulator = accumulator;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            long p = produced;
            if (p != 0L && Volatile.Read(ref requested) != long.MaxValue)
            {
                Interlocked.Add(ref requested, -p);
            }
            BackpressureHelper.ScalarPostComplete(ref requested, value, actual);
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            produced++;

            R v = value;

            actual.OnNext(v);

            try
            {
                v = accumulator(v, t);
            }
            catch (Exception e)
            {
                done = true;
                Cancel();

                actual.OnError(e);
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.ScalarPostCompleteRequest(ref requested, n, value, actual);
            }
        }
    }

    sealed class PublisherScan<T> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, T, T> accumulator;

        ISubscription s;

        bool done;

        bool hasValue;

        T value;

        public PublisherScan(ISubscriber<T> actual, Func<T, T, T> accumulator)
        {
            this.actual = actual;
            this.accumulator = accumulator;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            if (!hasValue)
            {
                value = t;
                actual.OnNext(t);
            }
            else
            {
                T v = value;
                try {
                    v = accumulator(v, t);
                }
                catch (Exception e)
                {
                    done = true;
                    s.Cancel();

                    actual.OnError(e);
                    return;
                }

                value = v;
                actual.OnNext(v);
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(s);
            }
        }

        public void Request(long n)
        {
            s.Request(n);
        }
    }

}
