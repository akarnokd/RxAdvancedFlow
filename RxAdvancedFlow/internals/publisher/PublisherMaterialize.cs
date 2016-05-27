using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherMaterialize<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<Signal<T>> actual;

        ISubscription s;

        long requested;

        long produced;

        Signal<T> value;

        public PublisherMaterialize(ISubscriber<Signal<T>> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {

            BackpressureHelper.Produced(ref requested, produced);

            Signal<T> v = Signal<T>.CreateOnComplete();
            value = v;
            BackpressureHelper.ScalarPostComplete(ref requested, v, actual);
        }

        public void OnError(Exception e)
        {
            BackpressureHelper.Produced(ref requested, produced);

            Signal<T> v = Signal<T>.CreateOnError(e);
            value = v;
            BackpressureHelper.ScalarPostComplete(ref requested, v, actual);
        }

        public void OnNext(T t)
        {
            produced++;

            actual.OnNext(Signal<T>.CreateOnNext(t));
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
                if (BackpressureHelper.ScalarPostCompleteRequest(ref requested, n, ref value, actual))
                {
                    s.Request(n);
                }
            }
        }
    }
}
