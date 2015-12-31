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
    sealed class PublisherRedo<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IPublisher<T> source;

        readonly bool errorMode;

        long remaining;

        MultiArbiterStruct arbiter;

        long produced;

        int wip;

        public PublisherRedo(ISubscriber<T> actual, bool error, long times, IPublisher<T> source)
        {
            this.actual = actual;
            this.errorMode = error;
            this.source = source;
            this.remaining = times;
        }

        public void Cancel()
        {
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            if (errorMode)
            {
                actual.OnComplete();
            }
            else
            {
                long r = remaining;

                if (r == 0)
                {
                    actual.OnComplete();
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
        }

        public void OnError(Exception e)
        {
            if (errorMode)
            {
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
            else
            {
                actual.OnError(e);
            }
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
