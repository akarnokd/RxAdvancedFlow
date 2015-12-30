using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSwitchIfEmpty<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IPublisher<T> other;

        MultiArbiterStruct arbiter;

        bool once;

        public PublisherSwitchIfEmpty(ISubscriber<T> actual, IPublisher<T> other)
        {
            this.actual = actual;
            this.other = other;
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void OnNext(T t)
        {
            if (!once)
            {
                once = true;
            }

            actual.OnNext(t);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnComplete()
        {
            if (once)
            {
                actual.OnComplete();
            }
            else
            {
                once = true;

                other.Subscribe(this);
            }
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        public void Cancel()
        {
            arbiter.Cancel();
        }
    }
}
