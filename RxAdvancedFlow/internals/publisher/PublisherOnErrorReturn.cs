using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherOnErrorReturn<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Func<Exception, T> resumeValue;

        ISubscription s;

        long requested;

        long produced;

        T value;

        public PublisherOnErrorReturn(ISubscriber<T> actual, Func<Exception, T> resumeValue)
        {
            this.actual = actual;
            this.resumeValue = resumeValue;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            T v;
            try
            {
                v = resumeValue(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            BackpressureHelper.Produced(ref requested, produced);

            v = value;
            BackpressureHelper.ScalarPostComplete(ref requested, v, actual);
        }

        public void OnNext(T t)
        {
            produced++;

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
