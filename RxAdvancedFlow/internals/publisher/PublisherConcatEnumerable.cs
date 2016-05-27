using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherConcatEnumerable<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IEnumerator<IPublisher<T>> enumerator;

        MultiArbiterStruct arbiter;

        int wip;

        long produced;

        public PublisherConcatEnumerable(ISubscriber<T> actual, IEnumerator<IPublisher<T>> enumerator)
        {
            this.actual = actual;
            this.enumerator = enumerator;
        }

        public void Cancel()
        {
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                do
                {
                    if (arbiter.IsCancelled())
                    {
                        return;
                    }

                    bool b;

                    try
                    {
                        b = enumerator.MoveNext();
                    }
                    catch (Exception e)
                    {
                        actual.OnError(e);
                        return;
                    }

                    if (b)
                    {
                        IPublisher<T> p = enumerator.Current;

                        if (p == null)
                        {
                            actual.OnError(new NullReferenceException("The enumerator returned a null value"));
                            return;
                        }

                        long c = produced;
                        if (c != 0L)
                        {
                            produced = 0L;
                            arbiter.Produced(c);
                        }

                        p.Subscribe(this);
                    }
                    else
                    {
                        actual.OnComplete();
                        return;
                    }
                } while (Interlocked.Decrement(ref wip) != 0);
            }
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
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
    }
}
