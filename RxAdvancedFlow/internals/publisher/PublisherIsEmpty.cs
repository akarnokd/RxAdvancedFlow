using Reactive.Streams;
using RxAdvancedFlow.subscriptions;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherIsEmpty<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<bool> actual;

        ISubscription s;

        ScalarDelayedSubscriptionStruct<bool> sds;

        public PublisherIsEmpty(ISubscriber<bool> actual)
        {
            this.actual = actual;
            sds.SetValue(true);
        }

        public void OnComplete()
        {
            if (!sds.Value())
            {
                return;
            }
            sds.Complete(true, actual);
        }

        public void OnError(Exception e)
        {
            if (!sds.Value())
            {
                return;
            }
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (!sds.Value())
            {
                return;
            }

            s.Cancel();

            sds.Complete(false, actual);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void Request(long n)
        {
            sds.Request(n, actual);
        }

        public void Cancel()
        {
            sds.Cancel();
            s.Cancel();
        }
    }
}
