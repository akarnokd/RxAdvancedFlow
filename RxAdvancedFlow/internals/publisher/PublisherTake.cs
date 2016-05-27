using Reactive.Streams;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherTake<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly long limit;

        ISubscription s;

        long remaining;

        int once;

        bool done;

        public PublisherTake(ISubscriber<T> actual, long n)
        {
            this.actual = actual;
            remaining = n;
            this.limit = n;
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

            long r = remaining;

            if (r == 0)
            {
                done = true;
                s.Cancel();

                actual.OnComplete();
                return;
            }
            else
            if (r == 1)
            {
                done = true;
                s.Cancel();

                actual.OnNext(t);
                actual.OnComplete();
                return;
            }

            remaining = r - 1;

            actual.OnNext(t);
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
                if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    if (n >= limit)
                    {
                        s.Request(long.MaxValue);
                    }
                    else
                    {
                        s.Request(n);
                    }
                }
                else
                {
                    s.Request(n);
                }
            }
        }
    }
}
