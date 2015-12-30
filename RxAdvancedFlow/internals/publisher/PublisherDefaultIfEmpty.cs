using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using RxAdvancedFlow.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherDefaultIfEmpty<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ScalarDelayedSubscriptionStruct<T> sds;

        ISubscription s;

        bool hasValue;

        public PublisherDefaultIfEmpty(ISubscriber<T> actual, T value)
        {
            this.actual = actual;
            this.sds.SetValue(value);
        }

        public void OnComplete()
        {
            if (!hasValue)
            {
                sds.Complete(sds.Value(), actual);
            }
            else
            {
                actual.OnComplete();
            }
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (!hasValue)
            {
                hasValue = true;
            }

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
            s.Request(n);

            if (OnSubscribeHelper.ValidateRequest(n))
            {
                sds.Request(n, actual);
            }
        }

        public void Cancel()
        {
            s.Cancel();
            sds.Cancel();
        }
    }
}
