using Reactive.Streams;
using RxAdvancedFlow.subscriptions;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherCount<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<long> actual;

        ISubscription s;

        ScalarDelayedSubscriptionStruct<long> sds;

        public PublisherCount(ISubscriber<long> actual)
        {
            this.actual = actual;
        }

        public void OnComplete()
        {
            sds.Complete(sds.Value(), actual);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            sds.SetValue(sds.Value() + 1);
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
            sds.Request(n, actual);
        }

        public void Cancel()
        {
            sds.Cancel();
            s.Cancel();
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
