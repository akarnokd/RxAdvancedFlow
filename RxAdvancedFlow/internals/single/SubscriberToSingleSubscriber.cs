using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.single
{

    sealed class SubscriberToSingleSubscriber<T> : ISingleSubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        IDisposable d;

        int state;

        T value;

        public SubscriberToSingleSubscriber(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            BackpressureHelper.SetTerminated(ref state);
            d.Dispose();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (OnSubscribeHelper.SetDisposable(ref this.d, d))
            {
                actual.OnSubscribe(this);
            }
        }

        public void OnSuccess(T t)
        {
            if (BackpressureHelper.SetValue(ref state, ref value, t))
            {
                actual.OnNext(t);
                actual.OnComplete();
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (BackpressureHelper.SetRequest(ref state, ref value))
                {
                    actual.OnNext(value);
                    actual.OnComplete();
                }
            }
        }
    }
}
