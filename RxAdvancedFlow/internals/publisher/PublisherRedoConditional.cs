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
    sealed class PublisherRedoConditional<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IPublisher<T> source;

        readonly bool errorMode;

        readonly Func<bool> shouldRepeat;

        MultiArbiterStruct arbiter;

        long produced;

        int wip;

        public PublisherRedoConditional(ISubscriber<T> actual, bool error, Func<bool> shouldRepeat, IPublisher<T> source)
        {
            this.actual = actual;
            this.errorMode = error;
            this.source = source;
            this.shouldRepeat = shouldRepeat;
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
                bool b = shouldRepeat();

                if (!b)
                {
                    actual.OnComplete();
                    return;
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
                bool b = shouldRepeat();

                if (!b)
                {
                    actual.OnError(e);
                    return;
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
                    if (arbiter.IsCancelled())
                    {
                        return;
                    }
                    source.Subscribe(this);        
                }
                while (Interlocked.Decrement(ref wip) != 0);
            }
        }
    }
}
