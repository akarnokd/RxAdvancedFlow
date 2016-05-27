using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherRetry<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IPublisher<T> source;

        readonly Func<Exception, bool> predicate;

        long remaining;

        MultiArbiterStruct arbiter;

        long produced;

        int wip;

        public PublisherRetry(ISubscriber<T> actual, Func<Exception, bool> predicate, long times, IPublisher<T> source)
        {
            this.actual = actual;
            this.predicate = predicate;
            this.source = source;
            this.remaining = times;
        }

        public void Cancel()
        {
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            bool b;

            try
            {
                b = predicate(e);
            }
            catch (Exception ex)
            {
                b = false;
                e = new AggregateException(e, ex);
            }

            if (!b)
            {
                actual.OnError(e);
                return;
            }

            long r = remaining;

            if (r == 0)
            {
                actual.OnError(e);
                return;
            }

            if (r != long.MaxValue)
            {
                remaining = r - 1;
            }

            long p = produced;
            if (p != 0L)
            {
                produced = 0L;
                arbiter.Produced(p);
            }

            Resubscribe();
        }

        public void OnNext(T t)
        {
            produced++;

            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        internal void Resubscribe()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                do
                {
                    source.Subscribe(this);        
                }
                while (Interlocked.Decrement(ref wip) != 0);
            }
        }
    }
}
